using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Moq;
using Xunit;

namespace ApplicationLayer.Tests;

public class QuestionSetServiceTests
{
    private readonly Mock<IQuestionSetRepository> _questionSetRepository = new();
    private readonly Mock<IQuestionGenerationJobRepository> _jobRepository = new();
    private readonly QuestionSetService _service;

    public QuestionSetServiceTests()
    {
        _service = new QuestionSetService(_questionSetRepository.Object, _jobRepository.Object);
    }

    [Fact]
    public async Task SaveDraftFromJobAsync_WhenDraftAlreadyExists_ThrowsConflict()
    {
        var ownerId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var job = CreateCompletedJob(jobId, ownerId);

        _jobRepository.Setup(r => r.GetByIdWithPlanAndQuestionsAsync(jobId)).ReturnsAsync(job);
        _questionSetRepository.Setup(r => r.ExistsBySourceJobIdAsync(jobId)).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.SaveDraftFromJobAsync(jobId, ownerId));
    }

    [Fact]
    public async Task SaveDraftFromJobAsync_WhenValid_CreatesDraftSnapshot()
    {
        var ownerId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var job = CreateCompletedJob(jobId, ownerId);

        _jobRepository.Setup(r => r.GetByIdWithPlanAndQuestionsAsync(jobId)).ReturnsAsync(job);
        _questionSetRepository.Setup(r => r.ExistsBySourceJobIdAsync(jobId)).ReturnsAsync(false);

        var result = await _service.SaveDraftFromJobAsync(jobId, ownerId);

        Assert.NotEqual(Guid.Empty, result.QuestionSetId);
        Assert.Equal(QuestionSetStatus.Draft, result.Status);
        Assert.Equal(jobId, result.SourceJobId);
        Assert.Equal(1, result.QuestionCount);

        _questionSetRepository.Verify(r => r.AddAsync(
            It.Is<QuestionSet>(qs => qs.SourceJobId == jobId && qs.OwnerId == ownerId),
            It.Is<IEnumerable<QuestionSetQuestion>>(qs => qs.Count() == 1)), Times.Once);
    }

    [Fact]
    public async Task GetQuestionSetAsync_WhenNotOwner_ThrowsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var questionSetId = Guid.NewGuid();

        _questionSetRepository.Setup(r => r.GetByIdWithQuestionsAsync(questionSetId))
            .ReturnsAsync(new QuestionSet
            {
                Id = questionSetId,
                OwnerId = Guid.NewGuid(),
                SourceJobId = Guid.NewGuid(),
                JobDescription = "JD",
                Questions = new List<QuestionSetQuestion>()
            });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetQuestionSetAsync(questionSetId, ownerId));
    }

    private static QuestionGenerationJob CreateCompletedJob(Guid jobId, Guid ownerId)
    {
        return new QuestionGenerationJob
        {
            Id = jobId,
            OwnerId = ownerId,
            JobDescription = "Senior .NET Developer",
            Status = QuestionGenerationJobStatus.Completed,
            CompletedAt = DateTime.UtcNow,
            Plan = new QuestionGenerationPlan
            {
                JobId = jobId,
                PlanJson = "{\"roleTitle\":\"Senior .NET Developer\"}"
            },
            Questions = new List<GeneratedQuestion>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    Order = 1,
                    Question = "Explain DI in ASP.NET Core",
                    QuestionType = "technical",
                    Difficulty = "medium",
                    EvaluationCriteriaJson = "[]",
                    CitationsJson = "[]"
                }
            }
        };
    }
}
