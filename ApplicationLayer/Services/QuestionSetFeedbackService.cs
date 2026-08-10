using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class QuestionSetFeedbackService : IQuestionSetFeedbackService
{
    private readonly IQuestionSetFeedbackRepository _feedbackRepo;
    private readonly IQuestionSetRepository _questionSetRepo;
    private readonly ICandidateMarketplaceRepository _marketplaceRepo;
    private readonly IPracticeSessionRepository _practiceSessionRepo;
    private readonly IUserRepository _userRepo;
    private readonly ICandidateProfileRepository _candidateProfileRepo;

    public QuestionSetFeedbackService(
        IQuestionSetFeedbackRepository feedbackRepo,
        IQuestionSetRepository questionSetRepo,
        ICandidateMarketplaceRepository marketplaceRepo,
        IPracticeSessionRepository practiceSessionRepo,
        IUserRepository userRepo,
        ICandidateProfileRepository candidateProfileRepo)
    {
        _feedbackRepo = feedbackRepo;
        _questionSetRepo = questionSetRepo;
        _marketplaceRepo = marketplaceRepo;
        _practiceSessionRepo = practiceSessionRepo;
        _userRepo = userRepo;
        _candidateProfileRepo = candidateProfileRepo;
    }

    public async Task<QuestionSetFeedbackDto> SubmitAsync(
        Guid candidateUserId, Guid questionSetId, SubmitQuestionSetFeedbackDto dto)
    {
        if (dto.Rating is < 0 or > 5)
            throw new BadRequestException("Rating phải từ 0 đến 5 sao.");

        if (await _questionSetRepo.GetOwnerIdAsync(questionSetId) is null)
            throw new NotFoundException("Bộ câu hỏi không tồn tại.");

        if (!await _practiceSessionRepo.HasCompletedSessionAsync(candidateUserId, questionSetId))
            throw new ForbiddenException("Bạn cần hoàn thành ít nhất 1 lần luyện tập bộ câu hỏi này trước khi gửi feedback.");

        var comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim();
        var existing = await _feedbackRepo.GetAsync(candidateUserId, questionSetId);

        QuestionSetFeedback feedback;
        if (existing is not null)
        {
            existing.Rating = dto.Rating;
            existing.Comment = comment;
            await _feedbackRepo.UpdateAsync(existing);
            feedback = existing;
        }
        else
        {
            feedback = new QuestionSetFeedback
            {
                QuestionSetId = questionSetId,
                CandidateUserId = candidateUserId,
                Rating = dto.Rating,
                Comment = comment
            };
            await _feedbackRepo.AddAsync(feedback);
        }

        return await MapOwnFeedbackToDtoAsync(feedback);
    }

    public async Task<QuestionSetFeedbackDto?> GetMineAsync(Guid candidateUserId, Guid questionSetId)
    {
        var feedback = await _feedbackRepo.GetAsync(candidateUserId, questionSetId);
        return feedback is null ? null : await MapOwnFeedbackToDtoAsync(feedback);
    }

    public async Task<QuestionSetFeedbackSummaryDto> GetPublicAsync(Guid questionSetId, QuestionSetFeedbackQueryDto query)
    {
        if (!await _marketplaceRepo.IsPublishedAsync(questionSetId))
            throw new NotFoundException("Bộ câu hỏi không tồn tại hoặc chưa được publish.");

        return await BuildSummaryAsync(questionSetId, query);
    }

    public async Task<QuestionSetFeedbackSummaryDto> GetForOwnerAsync(
        Guid questionSetId, Guid hrOwnerId, QuestionSetFeedbackQueryDto query)
    {
        var ownerId = await _questionSetRepo.GetOwnerIdAsync(questionSetId)
            ?? throw new NotFoundException("Bộ câu hỏi không tồn tại.");

        if (ownerId != hrOwnerId)
            throw new ForbiddenException("Bạn không có quyền xem feedback của bộ câu hỏi này.");

        return await BuildSummaryAsync(questionSetId, query);
    }

    /// <summary>Map feedback vừa submit/lấy — CandidateUser navigation chưa chắc được load nên tra riêng qua IUserRepository.</summary>
    private async Task<QuestionSetFeedbackDto> MapOwnFeedbackToDtoAsync(QuestionSetFeedback feedback)
    {
        var user = await _userRepo.GetByIdAsync(feedback.CandidateUserId);
        var candidateProfile = await _candidateProfileRepo.GetByUserIdAsync(feedback.CandidateUserId);

        return new QuestionSetFeedbackDto
        {
            Id = feedback.Id,
            CandidateUserId = feedback.CandidateUserId,
            CandidateName = user?.FullName ?? string.Empty,
            CandidateAvatarUrl = user?.AvatarUrl,
            CandidateTargetRole = candidateProfile?.TargetRole,
            Rating = feedback.Rating,
            Comment = feedback.Comment,
            CreatedAt = feedback.CreatedAt,
            UpdatedAt = feedback.UpdatedAt
        };
    }

    private async Task<QuestionSetFeedbackSummaryDto> BuildSummaryAsync(Guid questionSetId, QuestionSetFeedbackQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await _feedbackRepo.GetPagedByQuestionSetAsync(questionSetId, page, pageSize);
        var (average, _) = await _feedbackRepo.GetSummaryAsync(questionSetId);

        var mapped = new List<QuestionSetFeedbackDto>(items.Count);
        foreach (var f in items)
        {
            var candidateProfile = await _candidateProfileRepo.GetByUserIdAsync(f.CandidateUserId);
            mapped.Add(new QuestionSetFeedbackDto
            {
                Id = f.Id,
                CandidateUserId = f.CandidateUserId,
                CandidateName = f.CandidateUser.FullName,
                CandidateAvatarUrl = f.CandidateUser.AvatarUrl,
                CandidateTargetRole = candidateProfile?.TargetRole,
                Rating = f.Rating,
                Comment = f.Comment,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            });
        }

        return new QuestionSetFeedbackSummaryDto
        {
            QuestionSetId = questionSetId,
            AverageRating = average.HasValue ? Math.Round(average.Value, 1) : null,
            TotalCount = totalCount,
            Items = mapped,
            Page = page,
            PageSize = pageSize
        };
    }
}
