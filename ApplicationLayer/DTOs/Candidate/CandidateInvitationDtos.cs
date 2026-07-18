namespace ApplicationLayer.DTOs.Candidate;

/// <summary>1 lời mời phỏng vấn candidate nhận được (SCRUM-295).</summary>
public class CandidateInvitationListItemDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogo { get; set; }
    public string QuestionSetTitle { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

/// <summary>Read-model 1 dòng invitation join Companies/question_sets — dùng nội bộ bởi repository.</summary>
public class CandidateInvitationRow
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLogo { get; set; }
    public string? QuestionSetTitle { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public class InvitationActionResponseDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? RespondedAt { get; set; }
}
