namespace DomainLayer.Constants;

public static class CandidatePersonalSetPurpose
{
    public const string JdGap = "JdGap";
    public const string CvDiagnostic = "CvDiagnostic";
    public const string CvDrill = "CvDrill";
    /// <summary>SCRUM-447: bộ câu re-assessment sau roadmap.</summary>
    public const string CvReassessment = "CvReassessment";
    /// <summary>SCRUM-506: bài sàng lọc skill CV chưa đo sau diagnostic.</summary>
    public const string CvScreening = "CvScreening";

    public static bool IsCoach(string? purpose)
        => purpose is CvDiagnostic or CvDrill or CvReassessment or CvScreening;
}
