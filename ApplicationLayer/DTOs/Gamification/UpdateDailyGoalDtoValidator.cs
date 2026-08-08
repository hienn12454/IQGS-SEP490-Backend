using ApplicationLayer.Settings;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.DTOs.Gamification;

public sealed class UpdateDailyGoalDtoValidator : AbstractValidator<UpdateDailyGoalDto>
{
    public UpdateDailyGoalDtoValidator(IOptions<GamificationOptions> options)
    {
        var allowed = options.Value.AllowedDailyGoalXpValues;

        RuleFor(x => x.DailyGoalXp)
            .Must(v => allowed.Contains(v))
            .WithMessage($"dailyGoalXp phải là một trong các giá trị: {string.Join(", ", allowed)}.");
    }
}
