namespace ApplicationLayer.DTOs.Coach;

/// <summary>
/// Kết quả ScoreAssessmentFromSession — tránh silent skip khiến Rescore/Complete tưởng thành công.
/// </summary>
public sealed record CoachScoreResult(
    bool Scored,
    Guid? AssessmentId,
    string? Status,
    bool RoadmapUpdated,
    string? SkipReason)
{
    public static CoachScoreResult Skipped(string reason)
        => new(false, null, null, false, reason);

    public static CoachScoreResult Success(Guid assessmentId, string status, bool roadmapUpdated)
        => new(true, assessmentId, status, roadmapUpdated, null);
}
