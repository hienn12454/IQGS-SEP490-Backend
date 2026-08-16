using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class CandidatePersonalSetJobRepository : ICandidatePersonalSetJobRepository
{
    private readonly Database.AppDbContext _db;

    public CandidatePersonalSetJobRepository(Database.AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(CandidatePersonalSetJob job)
    {
        await _db.CandidatePersonalSetJobs.AddAsync(job);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(CandidatePersonalSetJob job)
    {
        job.UpdatedAt = DateTime.UtcNow;
        _db.CandidatePersonalSetJobs.Update(job);
        await _db.SaveChangesAsync();
    }

    public Task<CandidatePersonalSetJob?> GetByIdAsync(Guid id)
        => _db.CandidatePersonalSetJobs.FirstOrDefaultAsync(j => j.Id == id && j.IsActive);

    public Task<CandidatePersonalSetJob?> GetByQuestionSetIdAsync(Guid questionSetId)
        => _db.CandidatePersonalSetJobs.AsNoTracking()
            .Where(j => j.QuestionSetId == questionSetId && j.IsActive)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<CandidatePersonalSetJob>> ListByCandidateAsync(Guid candidateUserId)
        => await _db.CandidatePersonalSetJobs
            .AsNoTracking()
            .Where(j => j.CandidateUserId == candidateUserId && j.IsActive)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
}
