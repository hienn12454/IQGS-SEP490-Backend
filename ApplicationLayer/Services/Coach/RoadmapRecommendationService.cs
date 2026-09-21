using System.Text.Json;
using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
    private readonly IKnowledgeDocumentRepository _knowledgeDocs;
    private readonly IConfiguration _config;
    private readonly ILogger<RoadmapRecommendationService>? _logger;

    public RoadmapRecommendationService(
        ICandidateRoadmapRepository roadmaps,
        IRoadmapNodeRepository nodes,
        IRagService rag,
        IKnowledgeDocumentRepository knowledgeDocs,
        IConfiguration config,
        ILogger<RoadmapRecommendationService>? logger = null)
    {
        _roadmaps = roadmaps;
        _nodes = nodes;
        _rag = rag;
        _knowledgeDocs = knowledgeDocs;
        _config = config;
        _logger = logger;
    }

    public async Task RebuildFromDiagnosticAsync(
        Guid candidateUserId,
        CandidateAssessment assessment,
        CompetencyFramework? framework,
        CandidateSkillPlan profile)
    {
        var blueprint = CompetencyBlueprintJson.Deserialize(assessment.BlueprintJson);
        var toAdd = new List<CandidateRoadmap>();
        // SCRUM-461: Diagnostic chỉ dựng roadmap cho skill của LẦN ĐO này.
        // Không lấy toàn bộ profile.Items (có thể còn skill Adaptive stack cũ: asp.net sau CV FE).
        var scope = assessment.SkillResults
            .Select(r => CompetencyScoringService.NormalizeSkill(r.Skill))
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var sources = assessment.SkillResults.Select(r => new CandidateSkillPlanItem
        {
            Skill = r.Skill,
            CurrentScore = r.SkillScore,
            TargetScore = r.TargetScore,
            ImportanceWeight = r.ImportanceWeight
        }).ToList();

        if (sources.Count == 0)
        {
            sources = profile.Items
                .Where(i => i.CurrentScore is not null
                            && (scope.Count == 0 || scope.Contains(CompetencyScoringService.NormalizeSkill(i.Skill))))
                .ToList();
        }
        else if (profile.Items.Count > 0)
        {
            // Ưu tiên điểm/weight đã merge trên profile, nhưng chỉ skill nằm trong assessment.
            sources = profile.Items
                .Where(i => i.CurrentScore is not null
                            && scope.Contains(CompetencyScoringService.NormalizeSkill(i.Skill)))
                .Select(i => new CandidateSkillPlanItem
                {
                    Skill = i.Skill,
                    CurrentScore = i.CurrentScore,
                    TargetScore = i.TargetScore,
                    ImportanceWeight = i.ImportanceWeight
                })
                .ToList();
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
        }

        // SCRUM-462: skills trong snapshot = CV; coreSkills có thể pad framework ngoài CV.
        var cvSkills = ParseCvSkillsFromSnapshot(assessment.ContextSnapshotJson);

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
            var created = await BuildRoadmapAsync(
                candidateUserId, assessment, framework, blueprint, item.Skill, current, target, gap, weight, fwSkill, bpSkill, cvSkills);
            created.Framework = null;
            created.SourceAssessment = null;
            toAdd.Add(created);
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
                var cvSkills = ParseCvSkillsFromSnapshot(assessment.ContextSnapshotJson);
                var created = await BuildRoadmapAsync(
                    candidateUserId, assessment, framework, blueprint, result.Skill, current, target, gap, weight, fwSkill, bpSkill, cvSkills);
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
            // Giữ provenance skillSource / outsideCvReason khi refresh điểm.
            var (skillSource, outsideCvReason) = ParseSkillProvenanceFromJson(match.ExplanationJson);
            match.ExplanationJson = JsonSerializer.Serialize(new
            {
                reason = kind == CandidateRoadmapKind.Gap
                    ? $"Gap {gap} điểm so với target {target}. Ưu tiên luyện topic yếu rồi Re-assessment."
                    : $"Đã đạt target {target}. Có thể luyện nâng cao, không bắt buộc.",
                kbSource = ParseKbSourceFromJson(match.ExplanationJson),
                skillSource,
                outsideCvReason
            }, JsonOpts);
            await _roadmaps.UpdateAsync(match);
        }
    }

    private static string ParseKbSourceFromJson(string? explanationJson)
    {
        if (string.IsNullOrWhiteSpace(explanationJson))
            return CoachRoadmapKnowledgeFolder.KbSourceInferred;
        try
        {
            using var doc = JsonDocument.Parse(explanationJson);
            if (doc.RootElement.TryGetProperty("kbSource", out var ks))
            {
                var v = ks.GetString()?.Trim().ToLowerInvariant();
                if (v == CoachRoadmapKnowledgeFolder.KbSourceSystem)
                    return CoachRoadmapKnowledgeFolder.KbSourceSystem;
            }
        }
        catch (JsonException)
        {
            /* inferred */
        }
        return CoachRoadmapKnowledgeFolder.KbSourceInferred;
    }

    /// <summary>SCRUM-462: đọc skillSource/outsideCvReason đã lưu — mặc định cv.</summary>
    public static (string SkillSource, string? OutsideCvReason) ParseSkillProvenanceFromJson(string? explanationJson)
    {
        if (string.IsNullOrWhiteSpace(explanationJson))
            return ("cv", null);
        try
        {
            using var doc = JsonDocument.Parse(explanationJson);
            var root = doc.RootElement;
            var source = "cv";
            if (root.TryGetProperty("skillSource", out var ss))
            {
                var v = ss.GetString()?.Trim();
                if (string.Equals(v, "outsideCv", StringComparison.OrdinalIgnoreCase))
                    source = "outsideCv";
            }
            string? reason = null;
            if (root.TryGetProperty("outsideCvReason", out var ocr) && ocr.ValueKind == JsonValueKind.String)
                reason = ocr.GetString();
            return (source, reason);
        }
        catch (JsonException)
        {
            return ("cv", null);
        }
    }

    /// <summary>SCRUM-462: mảng skills trong ContextSnapshot = UnionCvSkills lúc StartDiagnostic.</summary>
    public static HashSet<string> ParseCvSkillsFromSnapshot(string? contextSnapshotJson)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(contextSnapshotJson)) return set;
        try
        {
            using var doc = JsonDocument.Parse(contextSnapshotJson);
            if (!doc.RootElement.TryGetProperty("skills", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return set;
            foreach (var x in arr.EnumerateArray())
            {
                var s = x.GetString();
                if (string.IsNullOrWhiteSpace(s)) continue;
                var n = CompetencyScoringService.NormalizeSkill(s);
                if (n.Length > 0) set.Add(n);
            }
        }
        catch (JsonException)
        {
            /* rỗng → coi mọi skill là từ CV (an toàn Adaptive) */
        }
        return set;
    }

    public static bool IsSkillFromCv(string skill, HashSet<string> cvNormalized)
    {
        if (cvNormalized.Count == 0) return true;
        return cvNormalized.Contains(CompetencyScoringService.NormalizeSkill(skill));
    }

    public static string BuildOutsideCvReason(
        string skill,
        string? role,
        string? level,
        double importanceWeight,
        double targetScore)
    {
        var roleText = string.IsNullOrWhiteSpace(role) ? "mục tiêu" : role.Trim();
        var levelText = string.IsNullOrWhiteSpace(level) ? "Junior" : level.Trim();
        return
            $"Framework {levelText} cho role {roleText} yêu cầu skill \"{skill}\" dù CV chưa nêu — importance {importanceWeight:0.##}, target {targetScore:0.#}.";
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
        CompetencyItem? bpSkill,
        HashSet<string> cvNormalized)
    {
        var kind = gap > 0 ? CandidateRoadmapKind.Gap : CandidateRoadmapKind.Advanced;
        var score = ComputePriorityScore(gap, weight);
        var adaptive = IsAdaptive(assessment, framework, blueprint);

        List<TopicPick> items;
        string? explanation;
        var kbSource = CoachRoadmapKnowledgeFolder.KbSourceInferred;
        if (adaptive)
        {
            var personalized = await PersonalizeAdaptiveTopicsAsync(
                assessment, blueprint, skill, current, target, gap, bpSkill);
            items = personalized.Items;
            explanation = personalized.Explanation;
            // Adaptive không retrieve folder coach-roadmap — coi là suy luận trừ khi sau này mở rộng.
            kbSource = CoachRoadmapKnowledgeFolder.KbSourceInferred;
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
            kbSource = topics.KbSource;
        }

        explanation ??= kind == CandidateRoadmapKind.Gap
            ? $"Gap {gap} điểm so với target {target}. Ưu tiên luyện topic yếu rồi Re-assessment."
            : $"Đã đạt target {target}. Có thể luyện nâng cao, không bắt buộc.";

        // Adaptive / khớp CV → cv; pad framework không có trên CV → outsideCv + lý do.
        var fromCv = adaptive || IsSkillFromCv(skill, cvNormalized);
        var skillSource = fromCv ? "cv" : "outsideCv";
        string? outsideCvReason = fromCv
            ? null
            : BuildOutsideCvReason(
                skill,
                framework?.DisplayRole ?? blueprint?.TargetRole ?? assessment.RoleFamilyKey,
                framework?.TargetLevel ?? blueprint?.TargetLevel,
                weight,
                target);

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
            AcceptedAt = null,
            ExplanationJson = JsonSerializer.Serialize(new
            {
                reason = explanation,
                kbSource,
                skillSource,
                outsideCvReason
            }, JsonOpts)
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
                IsIncluded = true,
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
                Status = CandidateRoadmapItemStatus.Pending,
                IsIncluded = true
            });
        }

        roadmap.Items.Add(new CandidateRoadmapItem
        {
            Topic = "Re-assessment",
            SortOrder = order,
            Status = CandidateRoadmapItemStatus.Pending,
            IsReassessmentGate = true,
            IsIncluded = true
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
                // SCRUM-461: chỉ giữ chunk overlap skill đang luyện — không nuốt section .NET vào allowed.
                chunks = CoachCvSkillGate.FilterChunks(ctx.Chunks, [skill]);
                foreach (var c in chunks)
                {
                    if (!string.IsNullOrWhiteSpace(c.Section)) allowed.Add(c.Section.Trim());
                    if (!string.IsNullOrWhiteSpace(c.SourceTitle)) allowed.Add(c.SourceTitle.Trim());
                }
            }
        }
        catch
        {
            // Adaptive roadmap: không giả FRAMEWORK; fallback topic từ blueprint / inferred.
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
                // Có chunk system: bắt buộc topic ∈ allowed. Inferred (chunks rỗng): chấp nhận topic LLM.
                var requireAllowed = chunks.Count > 0 && allowed.Count > 0;
                var picked = rec.Topics
                    .Where(t => !requireAllowed || allowed.Contains(t.Topic))
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

    private async Task<(List<TopicPick> Items, string? Explanation, string KbSource)> PersonalizeTopicsAsync(
        CompetencyFramework? framework,
        string skill,
        double current,
        double target,
        double gap,
        List<RoadmapNode> nodes,
        CompetencyFrameworkSkill? fwSkill)
    {
        var fallback = FallbackTopics(nodes, fwSkill, skill);
        var kbFolder = CoachRoadmapKnowledgeFolder.Resolve(_config);
        var coachDocs = await _knowledgeDocs.ListSystemDocumentIdsByFolderAsync(kbFolder);
        var kbSource = coachDocs.Count > 0
            ? CoachRoadmapKnowledgeFolder.KbSourceSystem
            : CoachRoadmapKnowledgeFolder.KbSourceInferred;

        if (coachDocs.Count == 0)
        {
            _logger?.LogWarning(
                "EMPTY_RETRIEVAL: chưa có tài liệu SYSTEM trong folder \"{Folder}\" — roadmap dùng node/framework (suy luận).",
                kbFolder);
        }

        if (framework is null || nodes.Count == 0)
            return (fallback, null, kbSource);

        try
        {
            var rec = await _rag.RecommendRoadmapAsync(new RagRoadmapRecommendRequest
            {
                TargetRole = framework.DisplayRole,
                TargetLevel = framework.TargetLevel,
                RoleKey = framework.RoleKey,
                DocumentIds = coachDocs.Count > 0 ? coachDocs.ToList() : new List<Guid>(),
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
                    return (picked, rec.Explanation, kbSource);
            }
        }
        catch
        {
            // LLM fail -> deterministic fallback, không chặn roadmap.
        }

        return (fallback, null, kbSource);
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
