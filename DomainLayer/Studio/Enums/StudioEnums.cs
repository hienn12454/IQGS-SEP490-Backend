namespace DomainLayer.Studio.Enums;

public enum InterviewProjectStatus
{
    Draft,
    Refining,
    AwaitingApproval,
    Approved,
    Generated,
    Archived
}

public enum JobDescriptionSourceType
{
    PastedText,
    UploadedFile
}

public enum DocumentProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public enum AiModelSelectionMode
{
    Auto
}

public enum AiChatMessageRole
{
    User,
    Assistant,
    System
}

public enum AiMessageStatus
{
    Pending,
    Streaming,
    Completed,
    Failed,
    Cancelled
}

public enum InterviewPlanStatus
{
    Draft,
    Refining,
    AwaitingApproval,
    Approved,
    Superseded,
    Rejected
}

public enum PlanApprovalAction
{
    Approved,
    Rejected
}

public enum QuestionType
{
    Technical,
    Behavioral,
    SystemDesign,
    ProblemSolving,
    Situational,
    FollowUp
}

public enum QuestionDifficulty
{
    Easy,
    Medium,
    Hard
}

public enum QuestionGenerationStatus
{
    Pending,
    Generating,
    Completed,
    Failed,
    Cancelled
}

public enum QuestionGeneratorType
{
    Mock,
    AiProvider,
    /// <summary>SCRUM-367: sinh qua RAG retrieve + LLM (giống luồng HR cũ).</summary>
    Rag
}

public enum SharePermission
{
    View,
    Edit
}
