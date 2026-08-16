using ApplicationLayer.Interfaces.Jobs;
using Hangfire;

namespace InfrastructureLayer.Jobs;

public class JobScheduler : IJobScheduler
{
    public void EnqueueKnowledgeIngest(Guid documentId)
    {
        BackgroundJob.Enqueue<IKnowledgeIngestJob>(job => job.ExecuteAsync(documentId));
    }

    public void EnqueueGeneratePlan(Guid jobId)
    {
        BackgroundJob.Enqueue<IGeneratePlanJob>(job => job.ExecuteAsync(jobId));
    }

    public void EnqueueGenerateQuestionsFromPlan(Guid jobId)
    {
        BackgroundJob.Enqueue<IGenerateQuestionsFromPlanJob>(job => job.ExecuteAsync(jobId));
    }

    public void EnqueueCandidatePersonalSet(Guid jobId)
    {
        BackgroundJob.Enqueue<IGenerateCandidatePersonalSetJob>(job => job.ExecuteAsync(jobId));
    }
}
