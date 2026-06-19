namespace DomainLayer.Constants;

public static class QuestionGenerationJobStatus
{
    public const string PlanQueued = "PLAN_QUEUED";
    public const string PlanProcessing = "PLAN_PROCESSING";
    public const string WaitingHrApproval = "WAITING_HR_APPROVAL";
    public const string QuestionQueued = "QUESTION_QUEUED";
    public const string QuestionProcessing = "QUESTION_PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}
