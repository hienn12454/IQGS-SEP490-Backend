using FluentValidation;

namespace ApplicationLayer.DTOs.Profile;

/// <summary>Validate riêng TimeZoneId (IANA id hợp lệ) — các field khác đã đủ bởi DataAnnotations trên DTO.</summary>
public sealed class UpdateCandidateProfileDtoValidator : AbstractValidator<UpdateCandidateProfileDto>
{
    public UpdateCandidateProfileDtoValidator()
    {
        RuleFor(x => x.TimeZoneId)
            .Must(BeAValidTimeZoneId)
            .WithMessage("TimeZoneId không hợp lệ — phải là IANA timezone id hợp lệ (vd \"Asia/Ho_Chi_Minh\", \"UTC\").")
            .When(x => !string.IsNullOrWhiteSpace(x.TimeZoneId));
    }

    private static bool BeAValidTimeZoneId(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return true;

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
