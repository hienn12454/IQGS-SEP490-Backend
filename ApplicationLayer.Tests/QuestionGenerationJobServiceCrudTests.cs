using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ApplicationLayer.Tests;

public class QuestionGenerationJobServiceCrudTests
{
    private readonly Mock<IQuestionGenerationJobRepository> _repository = new();
    private readonly Mock<IQuestionSetRepository> _questionSetRepository = new();
    private readonly QuestionGenerationJobService _service;

    public QuestionGenerationJobServiceCrudTests()
    {
        _questionSetRepository
            .Setup(r => r.ExistsBySourceJobIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(false);

        _service = new QuestionGenerationJobService(
            _repository.Object,
            new Mock<IJobScheduler>().Object,
            new Mock<IRagService>().Object,
            _questionSetRepository.Object,
            Options.Create(new KnowledgeBaseSettings { AllowedExtensions = [".pdf"], MaxFileSizeMb = 50 }));
    }

    [Fact]
    public async Task UpdateQuestionAsync_WhenJobNotCompleted_ThrowsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, ownerId, QuestionGenerationJobStatus.QuestionProcessing);
        _repository.Setup(r => r.GetByIdWithPlanAndQuestionsAsync(jobId)).ReturnsAsync(job);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.UpdateQuestionAsync(jobId, Guid.NewGuid(), ownerId, new UpdateQuestionRequestDto
            {
                Question = "Q?",
                QuestionType = "technical",
                Difficulty = "medium"
            }));
    }

    [Fact]
    public async Task DeleteQuestionAsync_WhenLastQuestion_ThrowsBadRequest()
    {
        var ownerId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var qId = Guid.NewGuid();
        var job = CreateJob(jobId, ownerId, QuestionGenerationJobStatus.Completed);
        job.Questions = new List<GeneratedQuestion>
        {
            new() { Id = qId, JobId = jobId, Order = 1, Question = "Q", QuestionType = "technical", Difficulty = "medium", EvaluationCriteriaJson = "[]", CitationsJson = "[]" }
        };

        _repository.Setup(r => r.GetByIdWithPlanAndQuestionsAsync(jobId)).ReturnsAsync(job);
        _repository.Setup(r => r.GetQuestionCountByJobIdAsync(jobId)).ReturnsAsync(1);
        _repository.Setup(r => r.GetQuestionByIdAsync(qId)).ReturnsAsync(job.Questions.First());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.DeleteQuestionAsync(jobId, qId, ownerId));
    }

    private static QuestionGenerationJob CreateJob(Guid jobId, Guid ownerId, string status)
    {
        return new QuestionGenerationJob
        {
            Id = jobId,
            OwnerId = ownerId,
            JobDescription = "JD",
            Status = status,
            NumberOfQuestions = 1,
            Difficulty = "medium",
            QuestionTypesJson = "[]",
            SkillsJson = "[]",
            Questions = new List<GeneratedQuestion>()
        };
    }
}
