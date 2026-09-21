namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-465: quy tắc expose JD cho candidate + gate bộ Tuyển — thuần, dễ unit test.
/// Full JobDescription (extract gen) không bao giờ trả cho candidate.
/// </summary>
public static class HiringJdExposureHelper
{
    /// <summary>
    /// Candidate chỉ thấy PublicJobDescription khi bộ Tuyển và bản ngắn non-empty.
    /// Practice hoặc thiếu bản ngắn → null (không lộ full extract).
    /// </summary>
    public static string? ResolveCandidateJobDescription(
        bool isHiringAssessment,
        string? publicJobDescription)
    {
        if (!isHiringAssessment) return null;
        var text = publicJobDescription?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    /// <summary>
    /// SAS file gốc chỉ khi bộ Tuyển và có JdBlobPath.
    /// </summary>
    public static bool ShouldExposeJdFileUrl(bool isHiringAssessment, string? jdBlobPath)
        => isHiringAssessment && !string.IsNullOrWhiteSpace(jdBlobPath);

    /// <summary>
    /// Publish / bật Tuyển yêu cầu bản ngắn non-empty.
    /// </summary>
    public static bool RequiresPublicJobDescription(bool isHiringAssessment, string? publicJobDescription)
        => isHiringAssessment && string.IsNullOrWhiteSpace(publicJobDescription?.Trim());

    /// <summary>
    /// Snippet list card ITViec — chỉ khi bộ Tuyển và có bản ngắn.
    /// </summary>
    public static string? BuildPublicJobDescriptionPreview(
        bool isHiringAssessment,
        string? publicJobDescription,
        int maxChars = 200)
    {
        if (!isHiringAssessment) return null;
        var text = publicJobDescription?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (maxChars < 1) maxChars = 200;
        if (text.Length <= maxChars) return text;
        return text[..maxChars].TrimEnd() + "…";
    }
}
