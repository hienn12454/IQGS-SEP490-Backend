using DomainLayer.Constants;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Helpers;

/// <summary>Quy tắc P1 HR (restore / compare / view / lịch mời) — tách để unit test không mock repo.</summary>
public static class RecommendationP1Rules
{
    public static DateTime? NextViewedAt(DateTime? current, DateTime utcNow)
        => current ?? utcNow;

    public static void EnsureCanRestore(string status)
    {
        if (status == CandidateRecommendationStatus.Invited)
            throw new ConflictException("Recommendation đã gửi lời mời — không thể đổi trạng thái.");
        if (status != CandidateRecommendationStatus.Dismissed)
            throw new ConflictException("Chỉ có thể restore recommendation đang DISMISSED.");
    }

    public static List<Guid> NormalizeCompareIds(IEnumerable<Guid> ids)
    {
        var distinct = ids.Where(x => x != Guid.Empty).Distinct().ToList();
        if (distinct.Count is < 2 or > 3)
            throw new BadRequestException("So sánh cần 2 hoặc 3 recommendation.");
        return distinct;
    }

    public static Guid EnsureSameQuestionSet(IEnumerable<Guid> questionSetIds)
    {
        var setIds = questionSetIds.Distinct().ToList();
        if (setIds.Count != 1)
            throw new BadRequestException("Chỉ so sánh ứng viên cùng một bộ câu hỏi.");
        return setIds[0];
    }

    public static string? NormalizeMeetingMode(string? meetingMode)
    {
        var mode = string.IsNullOrWhiteSpace(meetingMode) ? null : meetingMode.Trim().ToUpperInvariant();
        if (mode is not null && mode != MeetingMode.Online && mode != MeetingMode.Onsite)
            throw new BadRequestException("MeetingMode phải là ONLINE hoặc ONSITE.");
        return mode;
    }

    public static void EnsureInviteSchedule(DateTime? scheduledAtUtc, string? timeZoneId, string? meetingMode, string? meetingLink, string? location)
    {
        var mode = NormalizeMeetingMode(meetingMode);
        if (scheduledAtUtc.HasValue && string.IsNullOrWhiteSpace(timeZoneId))
            throw new BadRequestException("Cần timeZoneId khi có lịch phỏng vấn.");
        if (mode == MeetingMode.Online && string.IsNullOrWhiteSpace(meetingLink))
            throw new BadRequestException("ONLINE cần meetingLink.");
        if (mode == MeetingMode.Onsite && string.IsNullOrWhiteSpace(location))
            throw new BadRequestException("ONSITE cần location.");
    }
}
