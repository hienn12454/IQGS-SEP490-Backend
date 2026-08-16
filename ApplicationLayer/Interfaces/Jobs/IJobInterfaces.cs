namespace ApplicationLayer.Interfaces.Jobs;

public interface IKnowledgeIngestJob
{
    Task ExecuteAsync(Guid documentId);
}

public interface IGeneratePlanJob
{
    Task ExecuteAsync(Guid jobId);
}

public interface IGenerateQuestionsFromPlanJob
{
    Task ExecuteAsync(Guid jobId);
}

public interface IStuckKnowledgeDocumentWatchdogJob
{
    Task ExecuteAsync();
}

public interface IStuckQuestionGenerationWatchdogJob
{
    Task ExecuteAsync();
}

/// <summary>Tự nộp các phiên practice IN_PROGRESS đã hết giới hạn thời gian (cả khi candidate thoát app).</summary>
public interface IExpiredPracticeSessionWatchdogJob
{
    Task ExecuteAsync();
}

/// <summary>Đánh Expired cho đơn upgrade Pending đã quá ExpiresAt (TTL 10 phút).</summary>
public interface IExpirePendingUpgradeOrdersJob
{
    Task ExecuteAsync();
}

public interface IGenerateCandidatePersonalSetJob
{
    Task ExecuteAsync(Guid jobId);
}
