using System.Text.Json;
using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

public interface IRoadmapNodeImportService
{
    Task<RoadmapNodeImportResultDto> ImportAsync(RoadmapNodeImportRequestDto payload);
    Task<RoadmapNodeImportResultDto> ImportJsonlAsync(string jsonl);
    Task<List<RoadmapNodeAdminDto>> ListAsync();
    Task DeleteAsync(Guid id);
}

public class RoadmapNodeImportService : IRoadmapNodeImportService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IRoadmapNodeRepository _nodes;

    public RoadmapNodeImportService(IRoadmapNodeRepository nodes) => _nodes = nodes;

    public async Task<RoadmapNodeImportResultDto> ImportAsync(RoadmapNodeImportRequestDto payload)
    {
        var result = new RoadmapNodeImportResultDto();
        if (payload.Nodes.Count == 0)
        {
            result.Errors.Add("Không có node nào để import.");
            return result;
        }

        var knownRoles = await _nodes.ListKnownRoleKeysAsync();
        var entities = new List<RoadmapNode>();
        var i = 0;
        foreach (var row in payload.Nodes)
        {
            i++;
            if (string.IsNullOrWhiteSpace(row.RoleKey) || string.IsNullOrWhiteSpace(row.Skill)
                || string.IsNullOrWhiteSpace(row.Topic) || string.IsNullOrWhiteSpace(row.Level))
            {
                result.Errors.Add($"Dòng {i}: roleKey, level, skill, topic là bắt buộc.");
                continue;
            }

            if (!CoachSeniorityLevel.IsValid(row.Level))
            {
                result.Errors.Add($"Dòng {i}: level phải là Fresher/Junior/Middle/Senior.");
                continue;
            }

            var roleKey = row.RoleKey.Trim().ToLowerInvariant();
            if (knownRoles.Count > 0 && !knownRoles.Contains(roleKey))
                result.Warnings.Add($"Dòng {i}: RoleKey \"{roleKey}\" chưa có trong catalog framework — không gán sai role, vẫn lưu để import framework sau.");

            entities.Add(new RoadmapNode
            {
                RoleKey = roleKey,
                Technology = string.IsNullOrWhiteSpace(row.Technology) ? null : row.Technology.Trim(),
                Level = row.Level.Trim(),
                Skill = row.Skill.Trim(),
                Topic = row.Topic.Trim(),
                Subtopic = string.IsNullOrWhiteSpace(row.Subtopic) ? null : row.Subtopic.Trim(),
                Importance = Math.Clamp(row.Importance, 0, 1),
                PrerequisitesJson = JsonSerializer.Serialize(row.Prerequisites ?? new List<string>(), JsonOpts),
                NextTopicsJson = JsonSerializer.Serialize(row.NextTopics ?? new List<string>(), JsonOpts),
                SourceTitle = row.SourceTitle,
                SourceUrl = row.SourceUrl,
                SourceVersion = row.SourceVersion,
                SortOrder = row.SortOrder > 0 ? row.SortOrder : i
            });
        }

        if (result.Errors.Count > 0)
            return result;

        await _nodes.UpsertRangeAsync(entities);
        result.Success = true;
        result.Upserted = entities.Count;
        return result;
    }

    public Task<RoadmapNodeImportResultDto> ImportJsonlAsync(string jsonl)
    {
        var payload = new RoadmapNodeImportRequestDto();
        var lineNo = 0;
        foreach (var raw in (jsonl ?? "").Split('\n'))
        {
            lineNo++;
            var line = raw.Trim();
            if (line.Length == 0) continue;
            try
            {
                var row = JsonSerializer.Deserialize<RoadmapNodeImportDto>(line, JsonOpts);
                if (row is null)
                {
                    return Task.FromResult(new RoadmapNodeImportResultDto
                    {
                        Errors = { $"Dòng {lineNo}: JSON rỗng." }
                    });
                }
                payload.Nodes.Add(row);
            }
            catch (JsonException ex)
            {
                return Task.FromResult(new RoadmapNodeImportResultDto
                {
                    Errors = { $"Dòng {lineNo}: JSON không hợp lệ — {ex.Message}" }
                });
            }
        }
        return ImportAsync(payload);
    }

    public async Task<List<RoadmapNodeAdminDto>> ListAsync()
    {
        var rows = await _nodes.ListAllAsync();
        return rows.Select(n => new RoadmapNodeAdminDto
        {
            Id = n.Id,
            RoleKey = n.RoleKey,
            Technology = n.Technology,
            Level = n.Level,
            Skill = n.Skill,
            Topic = n.Topic,
            Subtopic = n.Subtopic,
            Importance = n.Importance,
            SourceTitle = n.SourceTitle,
            SourceUrl = n.SourceUrl,
            SortOrder = n.SortOrder
        }).ToList();
    }

    public Task DeleteAsync(Guid id) => _nodes.DeleteAsync(id);
}
