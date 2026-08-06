using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.Feedback;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class FeedbackService : IFeedbackService
{
    private readonly IFeedbackRepository _feedbackRepo;
    private readonly IUserRepository _userRepo;
    private readonly IHRProfileRepository _hrProfileRepo;
    private readonly ICandidateProfileRepository _candidateProfileRepo;
    private readonly ICompanyRepository _companyRepo;

    public FeedbackService(
        IFeedbackRepository feedbackRepo,
        IUserRepository userRepo,
        IHRProfileRepository hrProfileRepo,
        ICandidateProfileRepository candidateProfileRepo,
        ICompanyRepository companyRepo)
    {
        _feedbackRepo = feedbackRepo;
        _userRepo = userRepo;
        _hrProfileRepo = hrProfileRepo;
        _candidateProfileRepo = candidateProfileRepo;
        _companyRepo = companyRepo;
    }

    public async Task<FeedbackDto> CreateAsync(Guid userId, CreateFeedbackDto dto)
    {
        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");
        await _userRepo.LoadRoleAsync(user);

        var feedback = new Feedback
        {
            UserId = userId,
            Content = dto.Content.Trim(),
            Rating = dto.Rating,
            Status = FeedbackStatus.Pending
        };
        await _feedbackRepo.AddAsync(feedback);

        feedback.User = user;
        return await MapToDtoAsync(feedback);
    }

    public async Task<List<FeedbackDto>> GetPublicApprovedAsync(int limit)
    {
        var feedbacks = await _feedbackRepo.GetApprovedAsync(Math.Clamp(limit, 1, 100));
        var result = new List<FeedbackDto>(feedbacks.Count);
        foreach (var f in feedbacks)
            result.Add(await MapToDtoAsync(f));
        return result;
    }

    public async Task<PagedResultDto<FeedbackDto>> GetPagedForAdminAsync(FeedbackQueryDto query)
    {
        var (items, totalCount) = await _feedbackRepo.GetPagedAsync(query);

        var mapped = new List<FeedbackDto>(items.Count);
        foreach (var f in items)
            mapped.Add(await MapToDtoAsync(f));

        return new PagedResultDto<FeedbackDto>
        {
            Items = mapped,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<FeedbackDto> UpdateAsync(Guid id, UpdateFeedbackDto dto)
    {
        var feedback = await GetOwnedFeedbackAsync(id);

        feedback.Content = dto.Content.Trim();
        feedback.Rating = dto.Rating;
        await _feedbackRepo.UpdateAsync(feedback);

        return await MapToDtoAsync(feedback);
    }

    public async Task<FeedbackDto> UpdateStatusAsync(Guid id, string status)
    {
        if (!FeedbackStatus.IsValid(status))
            throw new BadRequestException("Trạng thái không hợp lệ. Chỉ chấp nhận Pending, Approved hoặc Rejected.");

        var feedback = await GetOwnedFeedbackAsync(id);
        feedback.Status = status;
        await _feedbackRepo.UpdateAsync(feedback);

        return await MapToDtoAsync(feedback);
    }

    public async Task DeleteAsync(Guid id)
    {
        var feedback = await _feedbackRepo.GetByIdAsync(id)
            ?? throw new NotFoundException("Không tìm thấy feedback.");
        await _feedbackRepo.DeleteAsync(feedback);
    }

    private async Task<Feedback> GetOwnedFeedbackAsync(Guid id)
    {
        var feedback = await _feedbackRepo.GetByIdAsync(id)
            ?? throw new NotFoundException("Không tìm thấy feedback.");

        var user = await _userRepo.GetByIdAsync(feedback.UserId);
        if (user != null)
        {
            await _userRepo.LoadRoleAsync(user);
            feedback.User = user;
        }

        return feedback;
    }

    private async Task<FeedbackDto> MapToDtoAsync(Feedback f)
    {
        var roleName = f.User.Role?.Name ?? UserRole.GetNameById(f.User.RoleId);

        string? authorTitle = null;
        if (roleName == UserRole.HR)
        {
            var hrProfile = await _hrProfileRepo.GetByUserIdAsync(f.UserId);
            if (hrProfile != null)
            {
                var company = await _companyRepo.GetByIdAsync(hrProfile.CompanyId);
                authorTitle = string.Join(" · ", new[] { hrProfile.JobTitle, company?.Name }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
            }
        }
        else if (roleName == UserRole.Candidate)
        {
            var candidateProfile = await _candidateProfileRepo.GetByUserIdAsync(f.UserId);
            authorTitle = candidateProfile?.TargetRole;
        }

        return new FeedbackDto
        {
            Id = f.Id,
            UserId = f.UserId,
            AuthorName = f.User.FullName,
            AuthorTitle = string.IsNullOrWhiteSpace(authorTitle) ? null : authorTitle,
            RoleName = roleName,
            AvatarUrl = f.User.AvatarUrl,
            Content = f.Content,
            Rating = f.Rating,
            Status = f.Status,
            CreatedAt = f.CreatedAt
        };
    }
}
