using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class HrPracticeSessionFeedbackService : IHrPracticeSessionFeedbackService
{
    private readonly IPracticeSessionRepository _sessionRepository;
    private readonly ICandidateProfileRepository _profileRepository;
    private readonly ICandidateMarketplaceRepository _marketplaceRepository;
    private readonly ICandidateAnswerRepository _answerRepository;
    private readonly IAiFeedbackRepository _feedbackRepository;

    public HrPracticeSessionFeedbackService(
        IPracticeSessionRepository sessionRepository,
        ICandidateProfileRepository profileRepository,
        ICandidateMarketplaceRepository marketplaceRepository,
        ICandidateAnswerRepository answerRepository,
        IAiFeedbackRepository feedbackRepository)
    {
        _sessionRepository = sessionRepository;
        _profileRepository = profileRepository;
        _marketplaceRepository = marketplaceRepository;
        _answerRepository = answerRepository;
        _feedbackRepository = feedbackRepository;
    }

    public async Task<PracticeSessionFeedbackDto> GetFeedbackForHrAsync(Guid sessionId, Guid hrUserId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new NotFoundException("Phiên luyện tập không tồn tại.");

        if (session.QuestionSet is null || session.QuestionSet.OwnerId != hrUserId)
            throw new ForbiddenException("Bạn chỉ xem được bài làm trên bộ câu hỏi của mình.");

        var profile = await _profileRepository.GetByUserIdAsync(session.CandidateUserId)
            ?? throw new NotFoundException("Không tìm thấy hồ sơ ứng viên.");

        if (!profile.AllowRecruiterRecommendation)
            throw new ForbiddenException("Ứng viên không cho phép HR xem hồ sơ.");

        if (session.Status != PracticeSessionStatus.Completed)
            throw new BadRequestException("Chỉ xem được bài làm của phiên đã hoàn thành.");

        var questions = await _marketplaceRepository.GetQuestionsSnapshotAsync(session.QuestionSetId);
        var answers = await _answerRepository.GetEntitiesBySessionIdAsync(sessionId);
        var feedbacks = await _feedbackRepository.GetBySessionIdAsync(sessionId);

        return PracticeSessionFeedbackMapper.Map(
            session, questions, answers, feedbacks, lockTeaser: false);
    }
}
