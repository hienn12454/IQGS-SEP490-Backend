namespace DomainLayer.Entities;

public class PracticeSession : BaseEntity
{
    public Guid CandidateUserId { get; set; }
    public Guid QuestionSetId { get; set; }
    public string Status { get; set; } = Constants.PracticeSessionStatus.InProgress;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? OverallScore { get; set; }

    /// <summary>Nhận xét AI tổng quan (tiếng Việt) — SCRUM-305.</summary>
    public string? AiInsightVi { get; set; }

    /// <summary>Nhận xét AI tổng quan (tiếng Anh) — SCRUM-305.</summary>
    public string? AiInsightEn { get; set; }

    /// <summary>JSON {"vi":[...],"en":[...]} — kỹ năng cần cải thiện (SCRUM-305).</summary>
    public string? SkillsToImproveJson { get; set; }

    /// <summary>SCRUM-446: Snapshot anti-cheat từ PlatformSettings lúc start phiên — đổi setting giữa chừng không ảnh hưởng phiên đang chạy.</summary>
    public bool AntiCheatEnabled { get; set; }

    /// <summary>SCRUM-446: Snapshot ngưỡng rời tab tối đa lúc start.</summary>
    public int AntiCheatMaxTabLeaves { get; set; } = 3;

    /// <summary>SCRUM-446: Số lần đã ghi nhận rời tab (visibility hidden).</summary>
    public int TabLeaveCount { get; set; }

    /// <summary>SCRUM-446: Thời điểm ghi nhận rời tab gần nhất — dùng debounce ~2s.</summary>
    public DateTime? LastTabLeaveAt { get; set; }

    public QuestionSet QuestionSet { get; set; } = null!;
}
