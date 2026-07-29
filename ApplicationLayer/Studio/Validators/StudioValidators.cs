using ApplicationLayer.Studio.Contracts;
using FluentValidation;

namespace ApplicationLayer.Studio.Validators;

public sealed class CreateStudioProjectRequestValidator : AbstractValidator<CreateStudioProjectRequest>
{
    public CreateStudioProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class UpsertJobDescriptionRequestValidator : AbstractValidator<UpsertJobDescriptionRequest>
{
    public UpsertJobDescriptionRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty();
    }
}

public sealed class ApprovePlanRequestValidator : AbstractValidator<ApprovePlanRequest>
{
    public ApprovePlanRequestValidator()
    {
        RuleFor(x => x.Revision).GreaterThan(0);
        RuleFor(x => x.ConcurrencyVersion).NotEqual(Guid.Empty);
    }
}

public sealed class GenerateQuestionsRequestValidator : AbstractValidator<GenerateQuestionsRequest>
{
    public GenerateQuestionsRequestValidator()
    {
        RuleFor(x => x.PlanId).NotEqual(Guid.Empty);
    }
}

public sealed class UpdateQuestionRequestValidator : AbstractValidator<UpdateQuestionRequest>
{
    public UpdateQuestionRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.EstimatedMinutes).GreaterThan(0);
    }
}

public sealed class StudioQuestionListRequestValidator : AbstractValidator<StudioQuestionListRequest>
{
    public StudioQuestionListRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class CreateShareLinkRequestValidator : AbstractValidator<CreateShareLinkRequest>
{
    public CreateShareLinkRequestValidator()
    {
        RuleFor(x => x.ExpiresAt).Must(x => !x.HasValue || x.Value > DateTime.UtcNow)
            .WithMessage("ExpiresAt phải lớn hơn hiện tại.");
    }
}
