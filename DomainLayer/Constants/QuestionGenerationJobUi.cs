namespace DomainLayer.Constants;

public static class JobPhase
{
    public const string Plan = "PLAN";
    public const string Questions = "QUESTIONS";
    public const string Done = "DONE";
}

public static class JobSuggestedAction
{
    public const string Poll = "POLL";
    public const string ReviewPlan = "REVIEW_PLAN";
    public const string ApprovePlan = "APPROVE_PLAN";
    public const string ReviewQuestions = "REVIEW_QUESTIONS";
    public const string EditQuestions = "EDIT_QUESTIONS";
    public const string SaveDraft = "SAVE_DRAFT";
    public const string EditInput = "EDIT_INPUT";
    public const string RetryPlan = "RETRY_PLAN";
    public const string RetryQuestions = "RETRY_QUESTIONS";
    public const string ViewDraft = "VIEW_DRAFT";
    public const string None = "NONE";
}
