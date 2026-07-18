using System.ComponentModel.DataAnnotations;

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
    public string? CompanyWebsite { get; set; }
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

/// <summary>Body tùy chọn khi chấp nhận lời mời — candidate có thể gửi kèm lời nhắn và/hoặc SĐT cho HR liên hệ phỏng vấn.</summary>
public class AcceptInvitationRequestDto
{
    [MaxLength(2000, ErrorMessage = "Lời nhắn tối đa 2000 ký tự.")]
    public string? ResponseMessage { get; set; }

    /// <summary>SĐT tùy chọn — candidate tự quyết định có chia sẻ hay không, chỉ HR gửi lời mời này thấy được.</summary>
    [RegularExpression(@"^0\d{9}$",
        ErrorMessage = "Số điện thoại không hợp lệ. Phải bắt đầu bằng số 0 và có đúng 10 chữ số.")]
    public string? PhoneNumber { get; set; }
}
