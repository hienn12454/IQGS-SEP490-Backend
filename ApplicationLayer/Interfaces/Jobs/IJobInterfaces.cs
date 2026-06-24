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
