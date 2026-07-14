using System.Text.Json;
using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class QuestionAiAssistService : IQuestionAiAssistService
{
    private readonly IQuestionGenerationJobRepository _repository;
    private readonly IQuestionSetRepository _questionSetRepository;
    private readonly IRagService _ragService;
    private readonly QuestionAiContextBuilder _contextBuilder;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public QuestionAiAssistService(
        IQuestionGenerationJobRepository repository,
        IQuestionSetRepository questionSetRepository,
        IRagService ragService,
        QuestionAiContextBuilder contextBuilder)
    {
        _repository = repository;
        _questionSetRepository = questionSetRepository;
        _ragService = ragService;
        _contextBuilder = contextBuilder;
    }

    public async Task<AskQuestionAiResponseDto> AskAsync(
        Guid jobId, Guid questionId, Guid ownerId, AskQuestionAiRequestDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
            throw new BadRequestException("message không được để trống.");

        var job = await GetOwnedJobForAskAiAsync(jobId, questionId, ownerId);
        var question = job.Questions.FirstOrDefault(q => q.Id == questionId)
            ?? throw new NotFoundException("Câu hỏi không tồn tại.");

        EnsureJobAllowsAskAi(job);

        var questionSet = await _questionSetRepository.GetBySourceJobIdWithQuestionsAsync(jobId);
        var baseSnapshot = QuestionAiContextBuilder.ResolveBaseSnapshot(question, questionSet);
        var mergedSnapshot = baseSnapshot.MergeWith(dto.CurrentQuestion);

        var priorHistory = await _repository.GetQuestionAiChatMessagesAsync(
            jobId, questionId, QuestionAiContextBuilder.MaxChatHistoryMessages);

        var ragRequest = _contextBuilder.Build(
            job,
            questionSet,
            question,
            mergedSnapshot,
            priorHistory,
            dto.Message.Trim());
        var ragResult = await _ragService.AskQuestionAssistAsync(ragRequest, ct);

        if (!ragResult.Success)
            throw new BadRequestException(ragResult.Error ?? "Không thể xử lý yêu cầu Ask AI.");

        var suggestionJson = ragResult.Suggestion is null
            ? null
            : JsonSerializer.Serialize(ragResult.Suggestion, JsonOptions);

        var hrMessage = new QuestionAiChatMessage
        {
            JobId = jobId,
            QuestionId = questionId,
            Role = QuestionAiChatRole.Hr,
            Content = dto.Message.Trim()
        };

        var aiMessage = new QuestionAiChatMessage
        {
            JobId = jobId,
            QuestionId = questionId,
            Role = QuestionAiChatRole.Ai,
            Content = ragResult.Reply,
            SuggestionJson = suggestionJson
        };

        await _repository.AddQuestionAiChatMessagesAsync(new[] { hrMessage, aiMessage });

        return new AskQuestionAiResponseDto
        {
            Reply = ragResult.Reply,
            Suggestion = MapSuggestion(ragResult.Suggestion)
        };
    }

    public async Task<QuestionAiChatHistoryResponseDto> GetChatHistoryAsync(
        Guid jobId, Guid questionId, Guid ownerId, CancellationToken ct = default)
    {
        _ = await GetOwnedJobForAskAiAsync(jobId, questionId, ownerId);

        var messages = await _repository.GetQuestionAiChatMessagesAsync(
            jobId, questionId, QuestionAiContextBuilder.MaxChatHistoryMessages);

        return new QuestionAiChatHistoryResponseDto
        {
            JobId = jobId,
            QuestionId = questionId,
            Messages = messages.Select(m => MapChatMessage(m)).ToList()
        };
    }

    private async Task<QuestionGenerationJob> GetOwnedJobForAskAiAsync(
        Guid jobId, Guid questionId, Guid ownerId)
    {
        var job = await _repository.GetOwnedJobWithQuestionAsync(jobId, questionId, ownerId)
            ?? throw new NotFoundException("Job hoặc câu hỏi không tồn tại.");

        if (job.OwnerId != ownerId)
            throw new ForbiddenException("Bạn không có quyền truy cập job này.");

        return job;
    }

    private static void EnsureJobAllowsAskAi(QuestionGenerationJob job)
    {
        if (job.Status != QuestionGenerationJobStatus.Completed)
            throw new BadRequestException("Chỉ hỏi AI khi session ở trạng thái COMPLETED.");

        if (job.Questions.Count == 0)
            throw new BadRequestException("Session chưa có câu hỏi.");
    }

    private static QuestionAiChatMessageDto MapChatMessage(
        QuestionAiChatMessage message,
        ApplicationLayer.DTOs.Rag.QuestionAssistSuggestionDto? suggestionOverride = null)
    {
        ApplicationLayer.DTOs.Rag.QuestionAssistSuggestionDto? suggestion = suggestionOverride;

        if (suggestion is null && !string.IsNullOrWhiteSpace(message.SuggestionJson))
        {
            try
            {
                suggestion = JsonSerializer.Deserialize<ApplicationLayer.DTOs.Rag.QuestionAssistSuggestionDto>(
                    message.SuggestionJson, JsonOptions);
            }
            catch
            {
                // Bỏ qua JSON lỗi — vẫn trả content chat
            }
        }

        return new QuestionAiChatMessageDto
        {
            Id = message.Id,
            Role = message.Role,
            Content = message.Content,
            Suggestion = MapSuggestion(suggestion),
            CreatedAt = message.CreatedAt
        };
    }

    private static QuestionAiSuggestionDto? MapSuggestion(
        ApplicationLayer.DTOs.Rag.QuestionAssistSuggestionDto? suggestion)
    {
        if (suggestion is null)
            return null;

        if (string.IsNullOrWhiteSpace(suggestion.Question)
            && string.IsNullOrWhiteSpace(suggestion.Rationale)
            && string.IsNullOrWhiteSpace(suggestion.SampleAnswer)
            && string.IsNullOrWhiteSpace(suggestion.Difficulty)
            && string.IsNullOrWhiteSpace(suggestion.QuestionType))
        {
            return null;
        }

        return new QuestionAiSuggestionDto
        {
            Question = suggestion.Question,
            Rationale = suggestion.Rationale,
            SampleAnswer = suggestion.SampleAnswer,
            Difficulty = suggestion.Difficulty,
            QuestionType = suggestion.QuestionType
        };
    }
}
