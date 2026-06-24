namespace DomainLayer.Constants;

public static class JdInputType
{
    public const string Text = "TEXT";
    public const string File = "FILE";
}

public static class ErrorStage
{
    public const string MissingJdInput = "MISSING_JD_INPUT";
    public const string InvalidFileType = "INVALID_FILE_TYPE";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string JdParse = "JD_PARSE";
    public const string JdValidation = "JD_VALIDATION";
    public const string Validation = "VALIDATION";
    public const string RagUnavailable = "RAG_UNAVAILABLE";
    public const string PlanGeneration = "PLAN_GENERATION";
    public const string QuestionGeneration = "QUESTION_GENERATION";
}
