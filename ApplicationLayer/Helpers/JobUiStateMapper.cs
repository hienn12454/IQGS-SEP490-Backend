using ApplicationLayer.DTOs.QuestionGeneration;
using DomainLayer.Constants;

namespace ApplicationLayer.Helpers;

public static class JobUiStateMapper
{
    public static JobUiStateDto Map(
        string status,
        bool hasDraft,
        bool planApproved,
        StructuredErrorResponseDto? error)
    {
        var phase = ResolvePhase(status, hasDraft);
        var failureStage = error?.Stage;
        var isFailed = status == QuestionGenerationJobStatus.Failed;
        var isInputEditableFailure = isFailed && IsInputEditableStage(failureStage);
        var isRagUnavailable = failureStage == ErrorStage.RagUnavailable;

        var actions = new JobUiActionsDto
        {
            CanPoll = status is QuestionGenerationJobStatus.PlanQueued
                or QuestionGenerationJobStatus.PlanProcessing
                or QuestionGenerationJobStatus.QuestionQueued
                or QuestionGenerationJobStatus.QuestionProcessing,
            CanEditInput = isInputEditableFailure && !planApproved,
            CanRetryPlan = isFailed && isRagUnavailable && !planApproved,
            CanRetryQuestions = isFailed && isRagUnavailable && planApproved,
            CanEditPlan = status == QuestionGenerationJobStatus.WaitingHrApproval,
            CanApprovePlan = status == QuestionGenerationJobStatus.WaitingHrApproval,
            CanViewQuestions = status == QuestionGenerationJobStatus.Completed,
            CanEditQuestions = status == QuestionGenerationJobStatus.Completed && !hasDraft,
            CanSaveDraft = status == QuestionGenerationJobStatus.Completed && !hasDraft,
            CanViewDraft = hasDraft,
            CanEditDraftQuestions = hasDraft
        };

        var suggestedAction = ResolveSuggestedAction(status, hasDraft, actions);

        return new JobUiStateDto
        {
            StatusLabel = ResolveStatusLabel(status, failureStage),
            Phase = phase,
            IsPolling = actions.CanPoll,
            SuggestedAction = suggestedAction,
            Actions = actions
        };
    }

    public static JobFailureDto? MapFailure(StructuredErrorResponseDto? error)
    {
        if (error is null)
            return null;

        return new JobFailureDto
        {
            Title = error.Error,
            Message = error.Detail ?? error.Errors.FirstOrDefault() ?? error.Error,
            Stage = error.Stage,
            Source = error.Source,
            Errors = error.Errors
        };
    }

    public static bool IsInputEditableStage(string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
            return true;

        return stage is ErrorStage.Validation
            or ErrorStage.JdValidation
            or ErrorStage.PlanGeneration
            or ErrorStage.JdParse
            or ErrorStage.MissingJdInput;
    }

    private static string ResolvePhase(string status, bool hasDraft)
    {
        if (hasDraft)
            return JobPhase.Done;

        return status switch
        {
            QuestionGenerationJobStatus.Completed => JobPhase.Questions,
            QuestionGenerationJobStatus.QuestionQueued or QuestionGenerationJobStatus.QuestionProcessing => JobPhase.Questions,
            _ => JobPhase.Plan
        };
    }

    private static string ResolveSuggestedAction(string status, bool hasDraft, JobUiActionsDto actions)
    {
        if (actions.CanPoll)
            return JobSuggestedAction.Poll;

        if (status == QuestionGenerationJobStatus.Failed)
        {
            if (actions.CanEditInput)
                return JobSuggestedAction.EditInput;
            if (actions.CanRetryQuestions)
                return JobSuggestedAction.RetryQuestions;
            if (actions.CanRetryPlan)
                return JobSuggestedAction.RetryPlan;
            return JobSuggestedAction.None;
        }

        if (status == QuestionGenerationJobStatus.WaitingHrApproval)
            return JobSuggestedAction.ReviewPlan;

        if (status == QuestionGenerationJobStatus.Completed)
        {
            if (hasDraft)
                return JobSuggestedAction.ViewDraft;
            return JobSuggestedAction.ReviewQuestions;
        }

        return JobSuggestedAction.None;
    }

    private static string ResolveStatusLabel(string status, string? failureStage)
    {
        if (status == QuestionGenerationJobStatus.Failed)
        {
            return failureStage switch
            {
                ErrorStage.Validation or ErrorStage.JdValidation =>
                    "Sinh plan thất bại — cần sửa thông tin đầu vào",
                ErrorStage.RagUnavailable => "Dịch vụ AI tạm thời không khả dụng",
                ErrorStage.PlanGeneration => "Sinh plan thất bại",
                _ => "Xử lý thất bại"
            };
        }

        return status switch
        {
            QuestionGenerationJobStatus.PlanQueued => "Đang chờ sinh plan",
            QuestionGenerationJobStatus.PlanProcessing => "Đang sinh plan",
            QuestionGenerationJobStatus.WaitingHrApproval => "Chờ HR duyệt plan",
            QuestionGenerationJobStatus.QuestionQueued => "Đang chờ sinh câu hỏi",
            QuestionGenerationJobStatus.QuestionProcessing => "Đang sinh câu hỏi",
            QuestionGenerationJobStatus.Completed => "Hoàn thành",
            _ => status
        };
    }
}
