using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface ICandidateAnswerRepository
{
    Task<CandidateAnswer?> GetAsync(Guid practiceSessionId, Guid questionSetQuestionId);
    Task AddAsync(CandidateAnswer answer);
    Task UpdateAsync(CandidateAnswer answer);
    Task<Dictionary<Guid, string>> GetAnswersBySessionIdAsync(Guid practiceSessionId);

    /// <summary>Xóa toàn bộ câu trả lời của 1 phiên — dùng khi luyện lại ghi đè phiên cũ.</summary>
    Task DeleteBySessionIdAsync(Guid practiceSessionId);
}
