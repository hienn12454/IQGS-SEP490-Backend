using System.Text.Json;
using ApplicationLayer.DTOs.Candidate;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-507: dựng màn tổng kết Coach từ baseline/drill/gap — deterministic, không LLM.
/// SCRUM-514: thêm tóm tắt câu đánh giá lại (Đạt/Chưa đạt theo ngưỡng drill).
/// </summary>
public static class CoachWrapUpBuilder
{
    public const int MaxStrengths = 6;
    public const int MaxWeakTopics = 5;
    public const int MaxNextSkills = 8;
    public const int MaxAnswers = 40;
    public const int QuestionPreviewLength = 120;
    public const double ImproveMinDelta = 1.0;

    /// <param name="planItems">Skill plan của candidate (baseline / current / target).</param>
    /// <param name="acceptedRoadmaps">Chỉ lộ trình đã Accept (AcceptedAt != null).</param>
    /// <param name="drillPassExclusiveMin">Ngưỡng pass drill (điểm phải lớn hơn giá trị này).</param>
    /// <param name="latest">Snapshot báo cáo gần nhất cho headline readiness.</param>
    /// <param name="drillAttemptScoresByItemId">Điểm mọi lần nộp drill theo roadmap item (không gồm cổng).</param>
    /// <param name="reassessmentAnswers">Câu đã chấm của cổng đánh giá lại — null khi wrap-up chưa mở.</param>
    public static CoachWrapUpDto Build(
        IReadOnlyList<CandidateSkillPlanItem> planItems,
        IReadOnlyList<CandidateRoadmap> acceptedRoadmaps,
        double drillPassExclusiveMin,
        CoachWrapUpSnapshot? latest = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<double>>? drillAttemptScoresByItemId = null,
        IReadOnlyList<CoachWrapUpAnswerInput>? reassessmentAnswers = null)
    {
        var accepted = acceptedRoadmaps ?? Array.Empty<CandidateRoadmap>();
        var completed = accepted.Count(r =>
            string.Equals(r.Status, CandidateRoadmapStatus.Completed, StringComparison.OrdinalIgnoreCase));
        var total = accepted.Count;
        var available = total > 0 && completed == total;

        var dto = new CoachWrapUpDto
        {
            Available = available,
            CompletedRoadmaps = completed,
            TotalRoadmaps = total,
            OverallReadiness = latest?.OverallReadiness,
            OverallDelta = latest?.OverallDelta,
            AchievedLevel = latest?.AchievedLevel,
            TargetReadinessStatus = latest?.TargetReadinessStatus,
            SuggestedNextLevel = latest?.SuggestedNextLevel,
            SuggestedNextLevelAvailable = latest?.SuggestedNextLevelAvailable ?? false,
            SuggestedNextLevelMessage = latest?.SuggestedNextLevelMessage
        };

        if (!available)
            return dto;

        var items = planItems ?? Array.Empty<CandidateSkillPlanItem>();
        var passMin = drillPassExclusiveMin > 0
            ? drillPassExclusiveMin
            : CoachDrillPassPolicy.DefaultPassScoreExclusiveMin;

        // Chỉ skill đã Accept lộ trình — không dump cả profile diagnostic (SQL/C#/…) khi user chỉ luyện 1 topic.
        var practiced = PracticedSkillKeys(accepted);
        var practicedItems = items
            .Where(i => practiced.Contains(CompetencyScoringService.NormalizeSkill(i.Skill)))
            .ToList();

        dto.Improved = BuildImproved(practicedItems);
        dto.Strengths = BuildStrengths(practicedItems);
        dto.WeakTopics = BuildWeakTopics(accepted, passMin, drillAttemptScoresByItemId);
        dto.NextSkills = BuildNextSkills(items, accepted);
        ApplyAnswers(dto, reassessmentAnswers, passMin);

        return dto;
    }

    /// <summary>
    /// SCRUM-514: Đạt = điểm &gt; ngưỡng drill. Điểm null (chưa chấm xong) tính là chưa đạt.
    /// Count khớp list trả về (cắt trần MaxAnswers) để UI hiện X/Y đúng với danh sách.
    /// </summary>
    private static void ApplyAnswers(
        CoachWrapUpDto dto,
        IReadOnlyList<CoachWrapUpAnswerInput>? answers,
        double passMin)
    {
        if (answers is null || answers.Count == 0)
            return;

        var mapped = answers
            .OrderBy(a => a.Order)
            .ThenBy(a => a.Skill, StringComparer.OrdinalIgnoreCase)
            .Take(MaxAnswers)
            .Select(a =>
            {
                var score = a.Score is double s ? Math.Round(s, 2) : (double?)null;
                return new CoachWrapUpAnswerDto
                {
                    Skill = a.Skill,
                    QuestionPreview = TruncatePreview(a.QuestionText),
                    Score = score,
                    Passed = CoachDrillPassPolicy.IsPassing(score, passMin)
                };
            })
            .ToList();

        dto.Answers = mapped;
        dto.AnswerTotalCount = mapped.Count;
        dto.AnswerPassedCount = mapped.Count(a => a.Passed);
    }

    private static string TruncatePreview(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim().Replace('\n', ' ').Replace('\r', ' ');
        if (trimmed.Length <= QuestionPreviewLength) return trimmed;
        return trimmed[..QuestionPreviewLength].TrimEnd() + "…";
    }

    /// <summary>Skill đã Accept lộ trình (user chủ động chọn luyện, kể cả chỉ 1 topic).</summary>
    private static HashSet<string> PracticedSkillKeys(IReadOnlyList<CandidateRoadmap> accepted)
        => accepted
            .Select(r => CompetencyScoringService.NormalizeSkill(r.Skill))
            .Where(k => k.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static List<CoachWrapUpSkillDeltaDto> BuildImproved(IReadOnlyList<CandidateSkillPlanItem> items)
        => items
            .Where(i => i.BaselineScore is double b && i.CurrentScore is double c && c - b >= ImproveMinDelta)
            .Select(i =>
            {
                var baseline = i.BaselineScore!.Value;
                var current = i.CurrentScore!.Value;
                return new CoachWrapUpSkillDeltaDto
                {
                    Skill = i.Skill,
                    BaselineScore = Math.Round(baseline, 2),
                    CurrentScore = Math.Round(current, 2),
                    Delta = Math.Round(current - baseline, 2)
                };
            })
            .OrderByDescending(x => x.Delta)
            .ThenBy(x => x.Skill, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<CoachWrapUpSkillDto> BuildStrengths(IReadOnlyList<CandidateSkillPlanItem> items)
        => items
            .Where(i => i.CurrentScore is double c && c >= i.TargetScore)
            .Select(i => new CoachWrapUpSkillDto
            {
                Skill = i.Skill,
                CurrentScore = Math.Round(i.CurrentScore!.Value, 2),
                TargetScore = i.TargetScore
            })
            .OrderByDescending(x => x.CurrentScore - x.TargetScore)
            .ThenBy(x => x.Skill, StringComparer.OrdinalIgnoreCase)
            .Take(MaxStrengths)
            .ToList();

    private static List<CoachWrapUpWeakTopicDto> BuildWeakTopics(
        IReadOnlyList<CandidateRoadmap> accepted,
        double passMin,
        IReadOnlyDictionary<Guid, IReadOnlyList<double>>? attemptScoresByItemId)
    {
        var weak = new List<CoachWrapUpWeakTopicDto>();
        foreach (var roadmap in accepted)
        {
            foreach (var item in roadmap.Items.Where(i => !i.IsReassessmentGate && i.IsIncluded))
            {
                var scores = new List<double>();
                if (attemptScoresByItemId is not null
                    && attemptScoresByItemId.TryGetValue(item.Id, out var attempts)
                    && attempts.Count > 0)
                {
                    scores.AddRange(attempts);
                }
                else if (item.DrillScore is double drillScore)
                {
                    scores.Add(drillScore);
                }

                if (scores.Count == 0) continue;

                var lowScores = scores.Where(s => !CoachDrillPassPolicy.IsPassing(s, passMin)).ToList();
                if (lowScores.Count == 0) continue;

                var lowest = lowScores.Min();
                var best = scores.Max();
                var overcame = CoachDrillPassPolicy.IsPassing(best, passMin)
                    || string.Equals(item.Status, CandidateRoadmapItemStatus.Completed, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Status, CandidateRoadmapItemStatus.ReadyForReassessment, StringComparison.OrdinalIgnoreCase);

                weak.Add(new CoachWrapUpWeakTopicDto
                {
                    Skill = roadmap.Skill,
                    Topic = string.IsNullOrWhiteSpace(item.Subtopic) ? item.Topic : $"{item.Topic} · {item.Subtopic}",
                    LowestScore = Math.Round(lowest, 2),
                    Overcame = overcame
                });
            }
        }

        return weak
            .OrderBy(w => w.LowestScore)
            .ThenBy(w => w.Skill, StringComparer.OrdinalIgnoreCase)
            .Take(MaxWeakTopics)
            .ToList();
    }

    private static List<CoachWrapUpNextSkillDto> BuildNextSkills(
        IReadOnlyList<CandidateSkillPlanItem> planItems,
        IReadOnlyList<CandidateRoadmap> accepted)
    {
        var next = new List<CoachWrapUpNextSkillDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in planItems
                     .Where(i => i.CurrentScore is double c && c < i.TargetScore)
                     .OrderByDescending(i => i.TargetScore - (i.CurrentScore ?? 0))
                     .ThenBy(i => i.Skill, StringComparer.OrdinalIgnoreCase))
        {
            var key = CompetencyScoringService.NormalizeSkill(item.Skill);
            if (!seen.Add(key)) continue;
            var current = item.CurrentScore ?? 0;
            next.Add(new CoachWrapUpNextSkillDto
            {
                Skill = item.Skill,
                CurrentScore = Math.Round(current, 2),
                TargetScore = item.TargetScore,
                Gap = Math.Round(item.TargetScore - current, 2),
                Reason = "gap"
            });
            if (next.Count >= MaxNextSkills) return next;
        }

        // Skill screening chưa có drill (chưa nộp topic nào).
        foreach (var roadmap in accepted
                     .Where(r => string.Equals(ParseConfidence(r.ExplanationJson), "screening", StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(r => r.Gap)
                     .ThenBy(r => r.Skill, StringComparer.OrdinalIgnoreCase))
        {
            var key = CompetencyScoringService.NormalizeSkill(roadmap.Skill);
            if (!seen.Add(key)) continue;

            var hasDrill = roadmap.Items.Any(i =>
                !i.IsReassessmentGate
                && (i.DrillScore is not null || i.DrillSessionId is not null || i.DrillQuestionSetId is not null));
            if (hasDrill) continue;

            next.Add(new CoachWrapUpNextSkillDto
            {
                Skill = roadmap.Skill,
                CurrentScore = roadmap.CurrentScore,
                TargetScore = roadmap.TargetScore,
                Gap = Math.Max(0, roadmap.Gap),
                Reason = "screening"
            });
            if (next.Count >= MaxNextSkills) break;
        }

        return next;
    }

    private static string? ParseConfidence(string? explanationJson)
    {
        if (string.IsNullOrWhiteSpace(explanationJson)) return null;
        var trimmed = explanationJson.Trim();
        if (!trimmed.StartsWith('{')) return null;
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.TryGetProperty("confidence", out var c)
                || doc.RootElement.TryGetProperty("Confidence", out c))
            {
                var v = c.GetString()?.Trim();
                return string.IsNullOrEmpty(v) ? null : v;
            }
        }
        catch (JsonException)
        {
            /* roadmap cũ */
        }
        return null;
    }
}

/// <summary>SCRUM-514: một câu đánh giá lại đã hydrate (skill + text + điểm) trước khi cắt preview.</summary>
public sealed record CoachWrapUpAnswerInput(string Skill, string QuestionText, double? Score, int Order = 0);

/// <summary>Snapshot báo cáo gần nhất để gắn headline lên wrap-up.</summary>
public sealed record CoachWrapUpSnapshot(
    double? OverallReadiness,
    double? OverallDelta,
    string? AchievedLevel,
    string? TargetReadinessStatus,
    string? SuggestedNextLevel,
    bool SuggestedNextLevelAvailable,
    string? SuggestedNextLevelMessage);
