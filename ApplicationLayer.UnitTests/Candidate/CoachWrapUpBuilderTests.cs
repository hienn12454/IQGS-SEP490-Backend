using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class CoachWrapUpBuilderTests
{
    [Fact]
    public void Build_NotAvailable_WhenAcceptedRoadmapStillActive()
    {
        var roadmaps = new List<CandidateRoadmap>
        {
            Accepted("Java", CandidateRoadmapStatus.Completed),
            Accepted("SQL", CandidateRoadmapStatus.Active)
        };

        var dto = CoachWrapUpBuilder.Build(
            [new CandidateSkillPlanItem { Skill = "Java", BaselineScore = 40, CurrentScore = 70, TargetScore = 70 }],
            roadmaps,
            drillPassExclusiveMin: 70);

        Assert.False(dto.Available);
        Assert.Equal(1, dto.CompletedRoadmaps);
        Assert.Equal(2, dto.TotalRoadmaps);
        Assert.Empty(dto.Improved);
    }

    [Fact]
    public void Build_Improved_WhenBaselineToCurrentGainsAtLeastOne()
    {
        var plan = new List<CandidateSkillPlanItem>
        {
            new() { Skill = "Java", BaselineScore = 40, CurrentScore = 70, TargetScore = 70 },
            new() { Skill = "SQL", BaselineScore = 60, CurrentScore = 60.5, TargetScore = 70 },
            new() { Skill = "Redis", BaselineScore = null, CurrentScore = 80, TargetScore = 70 }
        };
        var roadmaps = new List<CandidateRoadmap>
        {
            Accepted("Java", CandidateRoadmapStatus.Completed)
        };

        var dto = CoachWrapUpBuilder.Build(plan, roadmaps, 70);

        Assert.True(dto.Available);
        Assert.Single(dto.Improved);
        Assert.Equal("Java", dto.Improved[0].Skill);
        Assert.Equal(30, dto.Improved[0].Delta);
        Assert.Contains(dto.Strengths, s => s.Skill == "Java");
        // Redis đạt target nhưng không Accept lộ trình → không đưa vào «Làm rất tốt».
        Assert.DoesNotContain(dto.Strengths, s => s.Skill == "Redis");
        Assert.Contains(dto.NextSkills, n => n.Skill == "SQL" && n.Reason == "gap");
    }

    [Fact]
    public void Build_Strengths_IgnoresProfileSkillsUserDidNotAccept()
    {
        var plan = new List<CandidateSkillPlanItem>
        {
            new() { Skill = "ASP.NET Core", BaselineScore = 70, CurrentScore = 100, TargetScore = 70 },
            new() { Skill = "SQL", BaselineScore = 100, CurrentScore = 100, TargetScore = 65 },
            new() { Skill = "C#", BaselineScore = 100, CurrentScore = 100, TargetScore = 70 },
            new() { Skill = "REST API", BaselineScore = 90, CurrentScore = 100, TargetScore = 70 }
        };
        var roadmaps = new List<CandidateRoadmap>
        {
            Accepted("ASP.NET Core", CandidateRoadmapStatus.Completed)
        };

        var dto = CoachWrapUpBuilder.Build(plan, roadmaps, 70);

        Assert.True(dto.Available);
        Assert.Single(dto.Strengths);
        Assert.Equal("ASP.NET Core", dto.Strengths[0].Skill);
        Assert.Single(dto.Improved);
        Assert.Equal("ASP.NET Core", dto.Improved[0].Skill);
        Assert.DoesNotContain(dto.Improved, i => i.Skill == "REST API");
        Assert.Empty(dto.NextSkills);
    }

    [Fact]
    public void Build_WeakTopic_ShowsLowestFailingDrill_AndOvercameFlag()
    {
        var itemId = Guid.NewGuid();
        var roadmap = Accepted("C#", CandidateRoadmapStatus.Completed);
        roadmap.Items.Add(new CandidateRoadmapItem
        {
            Id = itemId,
            Topic = "LINQ",
            IsReassessmentGate = false,
            IsIncluded = true,
            Status = CandidateRoadmapItemStatus.Completed,
            DrillScore = 75
        });
        roadmap.Items.Add(new CandidateRoadmapItem
        {
            Id = Guid.NewGuid(),
            Topic = "Gate",
            IsReassessmentGate = true,
            IsIncluded = true,
            DrillScore = 40
        });

        var attempts = new Dictionary<Guid, IReadOnlyList<double>>
        {
            [itemId] = new List<double> { 55, 75 }
        };

        var dto = CoachWrapUpBuilder.Build(
            [new CandidateSkillPlanItem { Skill = "C#", BaselineScore = 50, CurrentScore = 80, TargetScore = 70 }],
            [roadmap],
            drillPassExclusiveMin: 70,
            drillAttemptScoresByItemId: attempts);

        Assert.Single(dto.WeakTopics);
        Assert.Equal("LINQ", dto.WeakTopics[0].Topic);
        Assert.Equal(55, dto.WeakTopics[0].LowestScore);
        Assert.True(dto.WeakTopics[0].Overcame);
    }

    [Fact]
    public void Build_NextSkills_IncludesScreeningWithoutDrill()
    {
        var screening = Accepted("Docker", CandidateRoadmapStatus.Completed);
        screening.ExplanationJson = """{"reason":"screening","confidence":"screening"}""";
        screening.CurrentScore = 45;
        screening.TargetScore = 70;
        screening.Gap = 25;
        screening.Items.Add(new CandidateRoadmapItem
        {
            Topic = "Containers",
            IsReassessmentGate = false,
            IsIncluded = true
        });

        var dto = CoachWrapUpBuilder.Build(
            Array.Empty<CandidateSkillPlanItem>(),
            [screening],
            70);

        Assert.True(dto.Available);
        Assert.Single(dto.NextSkills);
        Assert.Equal("Docker", dto.NextSkills[0].Skill);
        Assert.Equal("screening", dto.NextSkills[0].Reason);
    }

    [Fact]
    public void Build_Answers_CountsPassedAgainstExclusiveThreshold()
    {
        var roadmaps = new List<CandidateRoadmap>
        {
            Accepted("ASP.NET Core", CandidateRoadmapStatus.Completed)
        };
        var longQuestion = new string('A', 130);
        var answers = new List<CoachWrapUpAnswerInput>
        {
            new("ASP.NET Core", "Middleware pipeline?", 85, 2),
            new("ASP.NET Core", "DI lifetime?", 70, 1),
            new("ASP.NET Core", longQuestion, null, 3)
        };

        var dto = CoachWrapUpBuilder.Build(
            [new CandidateSkillPlanItem { Skill = "ASP.NET Core", BaselineScore = 40, CurrentScore = 80, TargetScore = 70 }],
            roadmaps,
            drillPassExclusiveMin: 70,
            reassessmentAnswers: answers);

        Assert.True(dto.Available);
        Assert.Equal(3, dto.AnswerTotalCount);
        Assert.Equal(1, dto.AnswerPassedCount);
        Assert.Equal(3, dto.Answers.Count);
        // Order 1 trước order 2.
        Assert.Equal(70, dto.Answers[0].Score);
        Assert.False(dto.Answers[0].Passed);
        Assert.True(dto.Answers[1].Passed);
        Assert.Null(dto.Answers[2].Score);
        Assert.False(dto.Answers[2].Passed);
        Assert.Equal(CoachWrapUpBuilder.QuestionPreviewLength + 1, dto.Answers[2].QuestionPreview.Length);
        Assert.EndsWith("…", dto.Answers[2].QuestionPreview);
    }

    [Fact]
    public void Build_Answers_IgnoredWhenWrapUpNotAvailable()
    {
        var dto = CoachWrapUpBuilder.Build(
            Array.Empty<CandidateSkillPlanItem>(),
            [Accepted("SQL", CandidateRoadmapStatus.Active)],
            70,
            reassessmentAnswers: [new CoachWrapUpAnswerInput("SQL", "Join?", 90)]);

        Assert.False(dto.Available);
        Assert.Equal(0, dto.AnswerTotalCount);
        Assert.Empty(dto.Answers);
    }

    private static CandidateRoadmap Accepted(string skill, string status)
        => new()
        {
            Skill = skill,
            Status = status,
            AcceptedAt = DateTime.UtcNow,
            TargetScore = 70,
            Gap = 0
        };
}
