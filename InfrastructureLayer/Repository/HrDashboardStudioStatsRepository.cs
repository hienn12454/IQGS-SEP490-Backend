using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Studio.Enums;
using InfrastructureLayer.Database;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

/// <summary>Load stats dashboard từ InterviewProjects + InterviewQuestions (không dùng job V1).</summary>
public sealed class HrDashboardStudioStatsRepository(AppDbContext db) : IHrDashboardStudioStatsRepository
{
    public async Task<IReadOnlyList<HrStudioProjectStatRow>> GetProjectStatsByOwnerAsync(Guid ownerId)
    {
        var projects = await db.InterviewProjects.AsNoTracking()
            .Where(p => p.OwnerId == ownerId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Status,
                p.CreatedAt,
                p.UpdatedAt
            })
            .ToListAsync();

        if (projects.Count == 0)
            return Array.Empty<HrStudioProjectStatRow>();

        var projectIds = projects.Select(p => p.Id).ToList();

        var questionRows = await db.InterviewQuestions.AsNoTracking()
            .Where(q => projectIds.Contains(q.ProjectId) && q.IsActive)
            .Select(q => new { q.ProjectId, q.Type })
            .ToListAsync();

        // Role title: latest plan title per project, fallback project name
        var planTitles = await db.InterviewPlans.AsNoTracking()
            .Where(pl => projectIds.Contains(pl.ProjectId) && pl.IsActive)
            .GroupBy(pl => pl.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                Title = g.OrderByDescending(x => x.Revision).Select(x => x.Title).FirstOrDefault()
            })
            .ToListAsync();

        var titleByProject = planTitles.ToDictionary(x => x.ProjectId, x => x.Title);

        var jdRoles = await db.StudioJobDescriptions.AsNoTracking()
            .Where(j => projectIds.Contains(j.ProjectId) && j.IsActive)
            .Select(j => new { j.ProjectId, j.DetectedRole, j.Title })
            .ToListAsync();
        var jdByProject = jdRoles.GroupBy(j => j.ProjectId)
            .ToDictionary(g => g.Key, g => g.First());

        var countsByProject = questionRows
            .GroupBy(q => q.ProjectId)
            .ToDictionary(g => g.Key, g => g.Count());

        var typesByProject = questionRows
            .GroupBy(q => q.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<HrStudioQuestionTypeCount>)g
                    .GroupBy(x => MapQuestionType(x.Type))
                    .Select(tg => new HrStudioQuestionTypeCount { Type = tg.Key, Count = tg.Count() })
                    .ToList());

        return projects.Select(p =>
        {
            string? role = null;
            if (titleByProject.TryGetValue(p.Id, out var planTitle) && !string.IsNullOrWhiteSpace(planTitle))
                role = planTitle.Trim();
            else if (jdByProject.TryGetValue(p.Id, out var jd))
                role = FirstNonEmpty(jd.DetectedRole, jd.Title);
            if (string.IsNullOrWhiteSpace(role))
                role = string.IsNullOrWhiteSpace(p.Name) ? null : p.Name.Trim();

            countsByProject.TryGetValue(p.Id, out var qCount);
            typesByProject.TryGetValue(p.Id, out var typeCounts);

            return new HrStudioProjectStatRow
            {
                ProjectId = p.Id,
                Name = p.Name,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                QuestionCount = qCount,
                RoleTitle = role,
                QuestionTypeCounts = typeCounts ?? Array.Empty<HrStudioQuestionTypeCount>()
            };
        }).ToList();
    }

    private static string MapQuestionType(QuestionType type) => type switch
    {
        QuestionType.Behavioral => "behavioral",
        QuestionType.SystemDesign => "system-design",
        QuestionType.ProblemSolving => "problem-solving",
        QuestionType.Situational => "situational",
        QuestionType.FollowUp => "follow-up",
        _ => "technical"
    };

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
