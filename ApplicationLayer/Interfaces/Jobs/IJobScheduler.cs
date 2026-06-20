namespace ApplicationLayer.Interfaces.Jobs;

public interface IJobScheduler
{
    void EnqueueKnowledgeIngest(Guid documentId);
    void EnqueueGeneratePlan(Guid jobId);
    void EnqueueGenerateQuestionsFromPlan(Guid jobId);
}
