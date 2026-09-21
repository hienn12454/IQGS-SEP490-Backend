using DomainLayer.Constants;

namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-468: quy tắc metadata tin tuyển (location / salary / expertise / domain) — thuần, dễ unit test.
/// Chỉ áp dụng khi bật / publish bộ Tuyển; Practice không bắt buộc.
/// </summary>
public static class HiringPostingHelper
{
    /// <summary>
    /// Bật Tuyển / publish Tuyển cần Location + Expertise + Domain
    /// và (lương min/max hợp lệ HOẶC SalaryNegotiable).
    /// </summary>
    public static bool RequiresHiringPostingFields(
        bool isHiringAssessment,
        string? jobLocation,
        string? jobExpertise,
        string? jobDomain,
        int? salaryMin,
        int? salaryMax,
        bool salaryNegotiable)
    {
        if (!isHiringAssessment) return false;
        if (string.IsNullOrWhiteSpace(jobLocation?.Trim())) return true;
        if (string.IsNullOrWhiteSpace(jobExpertise?.Trim())) return true;
        if (string.IsNullOrWhiteSpace(jobDomain?.Trim())) return true;
        if (!HasValidSalary(salaryMin, salaryMax, salaryNegotiable)) return true;
        return false;
    }

    /// <summary>
    /// Lương hợp lệ: thỏa thuận, hoặc min/max &gt; 0 và max ≥ min khi cả hai có.
    /// </summary>
    public static bool HasValidSalary(int? salaryMin, int? salaryMax, bool salaryNegotiable)
    {
        if (salaryNegotiable) return true;
        if (salaryMin is null && salaryMax is null) return false;
        if (salaryMin is int min && min <= 0) return false;
        if (salaryMax is int max && max <= 0) return false;
        if (salaryMin is int lo && salaryMax is int hi && hi < lo) return false;
        return true;
    }

    /// <summary>Chuẩn hóa WorkplaceType; null nếu trống hoặc không hợp lệ.</summary>
    public static string? NormalizeWorkplaceType(string? workplaceType)
        => WorkplaceType.Normalize(workplaceType);

    /// <summary>Trim string nullable — empty → null.</summary>
    public static string? NormalizeOptionalText(string? value, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.Length > maxLength) text = text[..maxLength];
        return text;
    }
}
