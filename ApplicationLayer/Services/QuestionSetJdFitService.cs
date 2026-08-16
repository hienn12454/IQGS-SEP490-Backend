using System.Text.Json;
using System.Text.Json.Serialization;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class QuestionSetJdFitService : IQuestionSetJdFitService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions PersistJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IQuestionSetRepository _questionSetRepository;
    private readonly IQuestionSetJdFitReviewRepository _reviewRepository;
    private readonly IRagService _ragService;
    private readonly ISubscriptionGateService _subscriptionGate;
    private readonly IUsageMeteringService _usageMetering;

    public QuestionSetJdFitService(
        IQuestionSetRepository questionSetRepository,
        IQuestionSetJdFitReviewRepository reviewRepository,
        IRagService ragService,
        ISubscriptionGateService subscriptionGate,
        IUsageMeteringService usageMetering)
    {
        _questionSetRepository = questionSetRepository;
        _reviewRepository = reviewRepository;
        _ragService = ragService;
        _subscriptionGate = subscriptionGate;
        _usageMetering = usageMetering;
    }

    public async Task<JdFitReviewResponse> GetAsync(
        Guid questionSetId, Guid ownerId, CancellationToken ct = default)
    {
        var (questionSet, questions) = await LoadOwnedSetAsync(questionSetId, ownerId);
        var currentHash = JdFitContentHash.Compute(questionSet.JobDescription, questions);
        var stored = await _reviewRepository.GetByQuestionSetIdAsync(questionSetId);
        if (stored is null)
        {
            return new JdFitReviewResponse
            {
                Review = null,
                ReviewedAt = null,
                ContentHash = currentHash,
                IsStale = false,
                HasJobDescription = !string.IsNullOrWhiteSpace(questionSet.JobDescription)
            };
        }

        var review = DeserializeStored(stored.ReviewJson);
        return new JdFitReviewResponse
        {
            Review = review,
            ReviewedAt = stored.ReviewedAt,
            ContentHash = stored.ContentHash,
            IsStale = !string.Equals(stored.ContentHash, currentHash, StringComparison.OrdinalIgnoreCase),
            HasJobDescription = !string.IsNullOrWhiteSpace(questionSet.JobDescription)
        };
    }

    public async Task<JdFitReviewResponse> ReviewAsync(
        Guid questionSetId, Guid ownerId, CancellationToken ct = default)
    {
        var (questionSet, questions) = await LoadOwnedSetAsync(questionSetId, ownerId);

        if (string.IsNullOrWhiteSpace(questionSet.JobDescription))
            throw new BadRequestException("Bộ câu hỏi chưa có job description. Dán hoặc tải JD trước khi đánh giá.");

        if (questions.Count == 0)
            throw new BadRequestException("Bộ câu hỏi chưa có câu hỏi để đánh giá.");

        await _subscriptionGate.CheckAskAiAsync(ownerId);

        object? planObj = null;
        if (!string.IsNullOrWhiteSpace(questionSet.PlanJson) && questionSet.PlanJson.Trim() != "{}")
        {
            try
            {
                planObj = JsonSerializer.Deserialize<object>(questionSet.PlanJson, JsonOptions);
            }
            catch (JsonException)
            {
                planObj = null;
            }
        }

        var ragRequest = new EvaluateQuestionSetRequest
        {
            OwnerId = ownerId,
            JobDescription = questionSet.JobDescription.Trim(),
            HrNote = questionSet.HrNote,
            SetTitle = questionSet.Title,
            Plan = planObj,
            Questions = questions.Select(q => new EvaluateQuestionSetItemDto
            {
                QuestionId = q.Id.ToString(),
                Order = q.Order,
                Question = q.Question,
                QuestionType = q.QuestionType,
                Difficulty = q.Difficulty,
                Skill = q.Skill,
                FocusArea = q.FocusArea,
                Rationale = q.Rationale
            }).ToList()
        };

        var ragResult = await _ragService.EvaluateQuestionSetAsync(ragRequest, ct);
        if (!ragResult.Success)
            throw new BadRequestException(ragResult.Error ?? "Không thể đánh giá bộ câu hỏi với JD.");

        var mapped = JdFitReviewMapper.Map(ragResult);
        if (string.IsNullOrWhiteSpace(mapped.Verdict) || string.IsNullOrWhiteSpace(mapped.SummaryVi))
            throw new BadRequestException("Kết quả đánh giá JD không hợp lệ.");

        var hash = JdFitContentHash.Compute(questionSet.JobDescription, questions);
        var payload = SerializeStored(mapped);
        var now = DateTime.UtcNow;

        var existing = await _reviewRepository.GetByQuestionSetIdAsync(questionSetId);
        if (existing is null)
        {
            await _reviewRepository.AddAsync(new QuestionSetJdFitReview
            {
                QuestionSetId = questionSetId,
                ReviewJson = payload,
                ContentHash = hash,
                ReviewedAt = now
            });
        }
        else
        {
            existing.ReviewJson = payload;
            existing.ContentHash = hash;
            existing.ReviewedAt = now;
            await _reviewRepository.UpdateAsync(existing);
        }

        await _usageMetering.IncrementAsync(ownerId, UsageType.HrAskAi);

        return new JdFitReviewResponse
        {
            Review = mapped,
            ReviewedAt = now,
            ContentHash = hash,
            IsStale = false,
            HasJobDescription = true
        };
    }

    private async Task<(QuestionSet Set, List<QuestionSetQuestion> Questions)> LoadOwnedSetAsync(
        Guid questionSetId, Guid ownerId)
    {
        var questionSet = await _questionSetRepository.GetByIdWithQuestionsAsync(questionSetId)
            ?? throw new NotFoundException("Question set không tồn tại.");

        if (questionSet.OwnerId != ownerId)
            throw new ForbiddenException("Bạn không có quyền truy cập question set này.");

        var questions = questionSet.Questions
            .Where(q => q.IsActive)
            .OrderBy(q => q.Order)
            .ToList();

        return (questionSet, questions);
    }

    private static string SerializeStored(EvaluateQuestionSetResult mapped)
    {
        var stored = new
        {
            mapped.Verdict,
            mapped.SummaryVi,
            mapped.SummaryEn,
            mapped.QuestionFlags,
            mapped.MissingTopics,
            mapped.SuggestedActions,
            mapped.JdSources
        };
        return JsonSerializer.Serialize(stored, PersistJsonOptions);
    }

    private static EvaluateQuestionSetResult? DeserializeStored(string json)
    {
        try
        {
            var mapped = JsonSerializer.Deserialize<EvaluateQuestionSetResult>(json, PersistJsonOptions);
            if (mapped is null || string.IsNullOrWhiteSpace(mapped.Verdict))
                return null;
            mapped.Success = true;
            return mapped;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
