using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ApplicationLayer.Tests;

public class QuestionGenerationJobServiceListTests
{
    private readonly Mock<IQuestionGenerationJobRepository> _repository = new();
    private readonly Mock<IJobScheduler> _scheduler = new();
    private readonly Mock<IRagService> _ragService = new();
    private readonly Mock<IQuestionSetRepository> _questionSetRepository = new();
    private readonly QuestionGenerationJobService _service;

    public QuestionGenerationJobServiceListTests()
    {
        var kbSettings = Options.Create(new KnowledgeBaseSettings
        {
            AllowedExtensions = [".pdf", ".docx", ".txt"],
            MaxFileSizeMb = 50
        });

        _questionSetRepository
            .Setup(r => r.GetSourceJobIdsWithDraftAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new HashSet<Guid>());

        _service = new QuestionGenerationJobService(
            _repository.Object,
            _scheduler.Object,
            _ragService.Object,
            _questionSetRepository.Object,
            kbSettings);
    }

    [Fact]
    public async Task ListJobsAsync_ReturnsPagedItemsForOwner()
    {
        var ownerId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var job = new QuestionGenerationJob
        {
            Id = jobId,
            OwnerId = ownerId,
            JobDescription = new string('A', 150),
            Status = QuestionGenerationJobStatus.Completed,
            NumberOfQuestions = 5,
            JdInputType = JdInputType.Text,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow,
            Questions = new List<GeneratedQuestion>
            {
                new() { Id = Guid.NewGuid(), JobId = jobId, Order = 1, Question = "Q1", QuestionType = "technical", Difficulty = "medium", EvaluationCriteriaJson = "[]", CitationsJson = "[]" }
            }
        };

        _repository.Setup(r => r.GetPagedByOwnerAsync(ownerId, It.IsAny<QuestionGenerationListQueryDto>()))
            .ReturnsAsync(new PagedResultDto<QuestionGenerationJob>
            {
                Items = new List<QuestionGenerationJob> { job },
                TotalCount = 1,
                Page = 1,
                PageSize = 20
            });

        var result = await _service.ListJobsAsync(ownerId, new QuestionGenerationListQueryDto());

        Assert.Single(result.Items);
        Assert.Equal(jobId, result.Items[0].JobId);
        Assert.Equal(1, result.Items[0].QuestionCount);
        Assert.EndsWith("...", result.Items[0].JobDescriptionPreview);
        Assert.Equal(123, result.Items[0].JobDescriptionPreview.Length);
    }

    [Fact]
    public async Task ListJobsAsync_ClampPageSizeToMax100()
    {
        var ownerId = Guid.NewGuid();
        QuestionGenerationListQueryDto? captured = null;

        _repository.Setup(r => r.GetPagedByOwnerAsync(ownerId, It.IsAny<QuestionGenerationListQueryDto>()))
            .Callback<Guid, QuestionGenerationListQueryDto>((_, q) => captured = q)
            .ReturnsAsync(new PagedResultDto<QuestionGenerationJob>
            {
                Items = Array.Empty<QuestionGenerationJob>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 100
            });

        await _service.ListJobsAsync(ownerId, new QuestionGenerationListQueryDto { PageSize = 500 });

        Assert.NotNull(captured);
        Assert.Equal(100, captured!.PageSize);
    }
}
