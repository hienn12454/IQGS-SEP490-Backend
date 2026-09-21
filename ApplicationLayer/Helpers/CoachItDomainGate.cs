using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-466: L1 domain IT cho AI Coach — skills + summary + role phải nghiêng IT.
/// </summary>
public static class CoachItDomainGate
{
    public const string Stage = "COACH_IT_DOMAIN";

    public const string DefaultReject =
        "Kỹ năng/hồ sơ hiện tại không thuộc lĩnh vực IT/phần mềm. " +
        "AI Coach chỉ hỗ trợ coaching kỹ thuật.";

    /// <summary>
    /// Ghép skills + summary + suggested/target role rồi ValidateItDomain.
    /// Throw 400 nếu non-IT hoặc không có tín hiệu IT.
    /// </summary>
    public static void EnsureItSkills(
        IReadOnlyList<string>? skills,
        string? summary = null,
        string? suggestedRole = null,
        string? targetRole = null)
    {
        var parts = new List<string>();
        if (skills is { Count: > 0 })
            parts.Add(string.Join(", ", skills.Where(s => !string.IsNullOrWhiteSpace(s))));
        if (!string.IsNullOrWhiteSpace(summary))
            parts.Add(summary.Trim());
        if (!string.IsNullOrWhiteSpace(suggestedRole))
            parts.Add(suggestedRole.Trim());
        if (!string.IsNullOrWhiteSpace(targetRole))
            parts.Add(targetRole.Trim());

        var blob = string.Join("\n", parts);
        if (string.IsNullOrWhiteSpace(blob))
            throw new BadRequestException("Hãy upload CV (hoặc thêm TechStack) trước khi dùng AI Coach.");

        var err = JobDescriptionValidator.ValidateItDomain(blob);
        if (err is not null)
            throw new BadRequestException(err);

        // Skills thuần non-IT (marketing, kế toán…) — nonItScore thắng
        if (skills is { Count: > 0 })
        {
            var skillBlob = string.Join(", ", skills);
            var skillErr = JobDescriptionValidator.ValidateItDomain(skillBlob);
            // Skill list ngắn có thể không đủ keyword IT role — chỉ reject khi non-IT thắng rõ
            var (it, nonIt) = JobDescriptionValidator.ScoreItDomain(skillBlob);
            if (nonIt > it && it == 0)
                throw new BadRequestException(DefaultReject);
            if (skillErr is not null && nonIt > it)
                throw new BadRequestException(DefaultReject);
        }
    }
}
