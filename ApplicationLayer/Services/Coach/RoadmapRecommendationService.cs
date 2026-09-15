using System.Text.Json;
using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-455: dựng personalized roadmap từ competency profile + curated nodes.
/// PriorityScore = Gap × ImportanceWeight (deterministic). LLM chỉ reorder/giải thích trong tập node retrieved.
/// </summary>
public interface IRoadmapRecommendationService
{
    Task RebuildFromDiagnosticAsync(
        Guid candidateUserId,
        CandidateAssessment assessment,
        CompetencyFramework? framework,
        CandidateSkillPlan profile);

    Task RefreshAfterReassessmentAsync(
        Guid candidateUserId,
        CandidateAssessment assessment,
        CompetencyFramework? framework,
        CandidateSkillPlan profile);
}

public class RoadmapRecommendationService : IRoadmapRecommendationService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ICandidateRoadmapRepository _roadmaps;
    private readonly IRoadmapNodeRepository _nodes;
    private readonly IRagService _rag;

    public RoadmapRecommendationService(
        ICandidateRoadmapRepository roadmaps,
        IRoadmapNodeRepository nodes,
        IRagService rag)
    {
        _roadmaps = roadmaps;
        _nodes = nodes;
        _rag = rag;
    }

    public async Task RebuildFromDiagnosticAsync(
        Guid candidateUserId,
        CandidateAssessment assessment,
        CompetencyFramework? framework,
        CandidateSkillPlan profile)
    {
        var blueprint = CompetencyBlueprintJson.Deserialize(assessment.BlueprintJson);
        var toAdd = new List<CandidateRoadmap>();
        var sources = profile.Items.Where(i => i.CurrentScore is not null).ToList();
        if (sources.Count == 0)
        {
            sources = assessment.SkillResults.Select(r => new CandidateSkillPlanItem
            {
                Skill = r.Skill,
                CurrentScore = r.SkillScore,
                TargetScore = r.TargetScore,
                ImportanceWeight = r.ImportanceWeight
            }).ToList();
        }

        foreach (var item in sources)
        {
            var fwSkill = framework?.Skills.FirstOrDefault(s =>
                CompetencyScoringService.NormalizeSkill(s.Skill)
                == CompetencyScoringService.NormalizeSkill(item.Skill));
            var bpSkill = blueprint?.Competencies.FirstOrDefault(c =>
                CompetencyScoringService.NormalizeSkill(c.SkillName)
                == CompetencyScoringService.NormalizeSkill(item.Skill));
            var target = fwSkill?.TargetScore ?? bpSkill?.TargetScore ?? item.TargetScore;
            var weight = fwSkill?.ImportanceWeight ?? bpSkill?.Weight ?? item.ImportanceWeight;
            var current = item.CurrentScore ?? 0;
            var gap = Math.Round(target - current, 2);
            toAdd.Add(await BuildRoadmapAsync(
                candidateUserId, assessment, framework, blueprint, item.Skill, current, target, gap, weight, fwSkill, bpSkill));
        }

        if (toAdd.Count == 0) return;

        var existing = await _roadmaps.ListAllByCandidateAsync(candidateUserId);
        var previouslyActiveIds = existing.Where(r => r.IsActive).Select(r => r.Id).ToList();
        await _roadmaps.ArchiveActiveByCandidateAsync(candidateUserId);

        try
        {
            await _roadmaps.AddRangeAsync(toAdd);
        }
        catch
        {
            // Không để candidate mất hết roadmap cũ nếu insert thế hệ mới fail.
            await _roadmaps.RestoreActiveAsync(previouslyActiveIds);
            throw;
        }
    }

    public async Task RefreshAfterReassessmentAsync(
        Guid candidateUserId,
        CandidateAssessment assessment,
        CompetencyFramework? framework,
        CandidateSkillPlan profile)
    {
        var scope = assessment.SkillResults
            .Select(r => CompetencyScoringService.NormalizeSkill(r.Skill))
            .ToHashSet();
        var active = await _roadmaps.ListByCandidateAsync(candidateUserId);
        var blueprint = CompetencyBlueprintJson.Deserialize(assessment.BlueprintJson);

        foreach (var result in assessment.SkillResults)
        {
            var key = CompetencyScoringService.NormalizeSkill(result.Skill);
            if (scope.Count > 0 && !scope.Contains(key)) continue;

            var fwSkill = framework?.Skills.FirstOrDefault(s =>
                CompetencyScoringService.NormalizeSkill(s.Skill) == key);
            var bpSkill = blueprint?.Competencies.FirstOrDefault(c =>
                CompetencyScoringService.NormalizeSkill(c.SkillName) == key);
            var target = fwSkill?.TargetScore ?? bpSkill?.TargetScore ?? result.TargetScore;
            var weight = fwSkill?.ImportanceWeight ?? bpSkill?.Weight ?? result.ImportanceWeight;
            var current = result.SkillScore;
            var gap = Math.Round(target - current, 2);
            var kind = gap > 0 ? CandidateRoadmapKind.Gap : CandidateRoadmapKind.Advanced;
            var score = ComputePriorityScore(gap, weight);

            var match = active.FirstOrDefault(r =>
                CompetencyScoringService.NormalizeSkill(r.Skill) == key);
            if (match is null)
            {
                var created = await BuildRoadmapAsync(
                    candidateUserId, assessment, framework, blueprint, result.Skill, current, target, gap, weight, fwSkill, bpSkill);
                await _roadmaps.AddRangeAsync(new[] { created });
                continue;
            }

            match.CurrentScore = current;
            match.TargetScore = target;
            match.Gap = Math.Max(0, gap);
            match.PriorityScore = score;
            match.Kind = kind;
            match.Priority = LabelPriority(score);
            match.SourceAssessmentId = assessment.Id;
            match.ExplanationJson = JsonSerializer.Serialize(new
            {
                reason = kind == CandidateRoadmapKind.Gap
                    ? $"Gap {gap} điểm so với target {target}. Ưu tiên luyện topic yếu rồi Re-assessment."
                    : $"Đã đạt target {target}. Có thể luyện nâng cao, không bắt buộc."
            }, JsonOpts);
            await _roadmaps.UpdateAsync(match);
        }
    }

    /// <summary>PriorityScore = max(Gap, 0) × ImportanceWeight. Advanced (gap ≤ 0) có score 0 nhưng không bị khóa.</summary>
    public static double ComputePriorityScore(double gap, double importanceWeight)
        => Math.Round(Math.Max(0, gap) * importanceWeight, 4);

    public static string LabelPriority(double priorityScore)
    {
        if (priorityScore >= 4) return "high";
        if (priorityScore >= 1.5) return "medium";
        return "low";
    }

    private async Task<CandidateRoadmap> BuildRoadmapAsync(
        Guid candidateUserId,
        CandidateAssessment assessment,
        CompetencyFramework? framework,
        CompetencyBlueprint? blueprint,
        string skill,
        double current,
        double target,
        double gap,
        double weight,
        CompetencyFrameworkSkill? fwSkill,
        CompetencyItem? bpSkill)
    {
        var kind = gap > 0 ? CandidateRoadmapKind.Gap : CandidateRoadmapKind.Advanced;
        var score = ComputePriorityScore(gap, weight);
        var adaptive = IsAdaptive(assessment, framework, blueprint);

        List<TopicPick> items;
        string? explanation;
        if (adaptive)
        {
            var personalized = await PersonalizeAdaptiveTopicsAsync(
                assessment, blueprint, skill, current, target, gap, bpSkill);
            items = personalized.Items;
            explanation = personalized.Explanation;
        }
        else
        {
            var nodes = new List<RoadmapNode>();
            if (framework is not null)
            {
                nodes = await _nodes.ListBySkillAsync(framework.RoleKey, framework.TargetLevel, skill);
                if (nodes.Count == 0 && !string.Equals(framework.TargetLevel, CoachSeniorityLevel.Junior, StringComparison.OrdinalIgnoreCase))
                    nodes = await _nodes.ListBySkillAsync(framework.RoleKey, CoachSeniorityLevel.Junior, skill);
            }
            var topics = await PersonalizeTopicsAsync(framework, skill, current, target, gap, nodes, fwSkill);
            items = topics.Items;
            explanation = topics.Explanation;
        }

        explanation ??= kind == CandidateRoadmapKind.Gap
            ? $"Gap {gap} điểm so với target {target}. Ưu tiên luyện topic yếu rồi Re-assessment."
            : $"Đã đạt target {target}. Có thể luyện nâng cao, không bắt buộc.";

        var roadmap = new CandidateRoadmap
        {
            CandidateUserId = candidateUserId,
            SourceAssessmentId = assessment.Id,
            FrameworkId = framework?.Id,
            SourceMode = adaptive ? CompetencySourceMode.RagDynamic : CompetencySourceMode.Framework,
            Skill = Truncate(skill, 200),
            CurrentScore = current,
            TargetScore = target,
            Gap = Math.Max(0, gap),
            PriorityScore = score,
            Kind = kind,
            Priority = LabelPriority(score),
            Status = CandidateRoadmapStatus.Suggested,
            ExplanationJson = JsonSerializer.Serialize(new { reason = explanation }, JsonOpts)
        };

        var order = 1;
        foreach (var t in items.Take(5))
        {
            var topic = Truncate(t.Topic, 300);
            if (string.IsNullOrWhiteSpace(topic)) continue;
            roadmap.Items.Add(new CandidateRoadmapItem
            {
                Topic = topic,
                Subtopic = TruncateNullable(t.Subtopic, 300),
                SortOrder = order++,
                Status = CandidateRoadmapItemStatus.Pending,
                SourceUrl = TruncateNullable(t.SourceUrl, 1000),
                SourceTitle = TruncateNullable(t.SourceTitle, 500),
                RoadmapNodeId = t.NodeId
            });
        }

        if (order == 1)
        {
            roadmap.Items.Add(new CandidateRoadmapItem
            {
                Topic = Truncate($"{skill} fundamentals", 300),
                SortOrder = order++,
                Status = CandidateRoadmapItemStatus.Pending
            });
        }

        roadmap.Items.Add(new CandidateRoadmapItem
        {
            Topic = "Re-assessment",
            SortOrder = order,
            Status = CandidateRoadmapItemStatus.Pending,
            IsReassessmentGate = true
        });
        return roadmap;
    }

    private static bool IsAdaptive(
        CandidateAssessment assessment,
        CompetencyFramework? framework,
        CompetencyBlueprint? blueprint)
        => string.Equals(assessment.ResolutionMode, CompetencyResolutionMode.Adaptive, StringComparison.OrdinalIgnoreCase)
           || string.Equals(blueprint?.SourceMode, CompetencyResolutionMode.Adaptive, StringComparison.OrdinalIgnoreCase)
           || (framework is null && !string.Equals(assessment.ResolutionMode, CompetencyResolutionMode.Framework, StringComparison.OrdinalIgnoreCase));

    private async Task<(List<TopicPick> Items, string? Explanation)> PersonalizeAdaptiveTopicsAsync(
        CandidateAssessment assessment,
        CompetencyBlueprint? blueprint,
        string skill,
        double current,
        double target,
        double gap,
        CompetencyItem? bpSkill)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in bpSkill?.Topics ?? [])
            if (!string.IsNullOrWhiteSpace(t)) allowed.Add(t.Trim());

        List<RagCompetencyChunkDto> chunks = [];
        try
        {
            var ctx = await _rag.RetrieveCompetencyContextAsync(new RagCompetencyContextRequest
            {
                TargetRole = blueprint?.TargetRole ?? assessment.RoleFamilyKey ?? skill,
                TargetLevel = blueprint?.TargetLevel ?? CoachSeniorityLevel.Junior,
                RoleFamilyKey = assessment.RoleFamilyKey,
                Skills = [skill]
            });
            if (ctx.Success)
            {
                chunks = ctx.Chunks;
                foreach (var c in ctx.Chunks)
                {
                    if (!string.IsNullOrWhiteSpace(c.Section)) allowed.Add(c.Section.Trim());
                    if (!string.IsNullOrWhiteSpace(c.SourceTitle)) allowed.Add(c.SourceTitle.Trim());
                }
            }
        }
        catch
        {
            // Adaptive roadmap: không giả FRAMEWORK; fallback topic từ blueprint đã có evidence.
        }

        try
        {
            var rec = await _rag.GenerateAdaptiveRoadmapAsync(new RagAdaptiveRoadmapRequest
            {
                TargetRole = blueprint?.TargetRole ?? skill,
                TargetLevel = blueprint?.TargetLevel ?? CoachSeniorityLevel.Junior,
                Skill = skill,
                CurrentScore = current,
                TargetScore = target,
                Gap = gap,
                BlueprintTopics = allowed.ToList(),
                Chunks = chunks
            });
            if (rec.Success && rec.Topics.Count > 0)
            {
                var picked = rec.Topics
                    .Where(t => allowed.Count == 0 || allowed.Contains(t.Topic))
                    .Select(t => new TopicPick(t.Topic, t.Subtopic, null, t.SourceUrl, t.SourceTitle))
                    .ToList();
                if (picked.Count > 0)
                    return (picked, rec.Explanation);
            }
        }
        catch
        {
            // giữ fallback blueprint topics
        }

        var fallbackNames = (bpSkill?.Topics.Count > 0
            ? bpSkill.Topics
            : new List<string> { $"{skill} fundamentals", $"{skill} application", $"{skill} advanced" });
        return (fallbackNames.Select(n => new TopicPick(n, null, null, null, null)).ToList(), null);
    }

    private async Task<(List<TopicPick> Items, string? Explanation)> PersonalizeTopicsAsync(
        CompetencyFramework? framework,
        string skill,
        double current,
        double target,
        double gap,
        List<RoadmapNode> nodes,
        CompetencyFrameworkSkill? fwSkill)
    {
        var fallback = FallbackTopics(nodes, fwSkill, skill);
        if (framework is null || nodes.Count == 0)
            return (fallback, null);

        try
        {
            var rec = await _rag.RecommendRoadmapAsync(new RagRoadmapRecommendRequest
            {
                TargetRole = framework.DisplayRole,
                TargetLevel = framework.TargetLevel,
                WeakSkills =
                [
                    new RagRoadmapWeakSkillDto
                    {
                        Skill = skill,
                        CurrentScore = current,
                        TargetScore = target,
                        Gap = gap
                    }
                ],
                CandidateNodes = nodes.Select(n => new RagRoadmapNodeDto
                {
                    Topic = n.Topic,
                    Subtopic = n.Subtopic,
                    Skill = n.Skill,
                    Importance = n.Importance,
                    Prerequisites = ParseStringList(n.PrerequisitesJson),
                    NextTopics = ParseStringList(n.NextTopicsJson),
                    SourceTitle = n.SourceTitle,
                    SourceUrl = n.SourceUrl
                }).ToList()
            });
            if (rec.Success && rec.Topics.Count > 0)
            {
                var allowed = nodes.ToDictionary(n => n.Topic, StringComparer.OrdinalIgnoreCase);
                var picked = new List<TopicPick>();
                foreach (var t in rec.Topics)
                {
                    if (!allowed.TryGetValue(t.Topic, out var node)) continue;
                    picked.Add(new TopicPick(node.Topic, node.Subtopic, node.Id, node.SourceUrl, node.SourceTitle));
                }
                if (picked.Count > 0)
                    return (picked, rec.Explanation);
            }
        }
        catch
        {
            // LLM fail -> deterministic fallback, không chặn roadmap.
        }

        return (fallback, null);
    }

    public static List<TopicPick> FallbackTopics(
        IReadOnlyList<RoadmapNode> nodes,
        CompetencyFrameworkSkill? fwSkill,
        string skill)
    {
        if (nodes.Count > 0)
            return nodes
                .OrderByDescending(n => n.Importance)
                .ThenBy(n => n.SortOrder)
                .Select(n => new TopicPick(n.Topic, n.Subtopic, n.Id, n.SourceUrl, n.SourceTitle))
                .ToList();

        var names = CompetencyTopicParser.ParseTopicNames(fwSkill?.TopicsJson);
        if (names.Count == 0)
            names = [$"{skill} fundamentals", $"{skill} application", $"{skill} advanced"];
        return names.Select(n => new TopicPick(n, null, null, null, null)).ToList();
    }

    private static string Truncate(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static string? TruncateNullable(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static List<string> ParseStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    public sealed record TopicPick(
        string Topic,
        string? Subtopic,
        Guid? NodeId,
        string? SourceUrl,
        string? SourceTitle);
}
