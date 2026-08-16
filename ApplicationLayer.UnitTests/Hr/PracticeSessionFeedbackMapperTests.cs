using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.Helpers;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Xunit;

namespace ApplicationLayer.UnitTests.Hr;

public class PracticeSessionFeedbackMapperTests
{
    [Fact]
    public void HrView_DoesNotLock_AndExposesPersistedScore()
    {
        var session = NewSession();
        var questions = NewQuestions();
        var answerId = Guid.NewGuid();
        var answers = new List<CandidateAnswer>
        {
            new() { Id = answerId, QuestionSetQuestionId = questions[0].Id, AnswerText = "hello" },
            new() { Id = Guid.NewGuid(), QuestionSetQuestionId = questions[1].Id, AnswerText = "world" }
        };
        var feedbacks = new List<AiFeedback>
        {
            new()
            {
                CandidateAnswerId = answerId,
                Score = 81,
                EvaluationStatus = AiFeedbackEvaluationStatus.Succeeded,
                StrengthsJson = "[\"clear\"]",
                ImprovementsJson = "[]"
            }
        };

        var dto = PracticeSessionFeedbackMapper.Map(session, questions, answers, feedbacks, lockTeaser: false);

        Assert.Equal(PracticeFeedbackAccessLevel.Full, dto.AccessLevel);
        Assert.All(dto.Items, i => Assert.False(i.IsLocked));
        Assert.Equal(81, dto.Items.First(i => i.QuestionId == questions[0].Id).Score);
        Assert.Equal("hello", dto.Items.First(i => i.QuestionId == questions[0].Id).AnswerText);
    }

    [Fact]
    public void CandidateTeaser_LocksUnscoredQuestions()
    {
        var session = NewSession();
        var questions = NewQuestions();
        var answerId = Guid.NewGuid();
        var answers = new List<CandidateAnswer>
        {
            new() { Id = answerId, QuestionSetQuestionId = questions[0].Id, AnswerText = "hello" },
            new() { Id = Guid.NewGuid(), QuestionSetQuestionId = questions[1].Id, AnswerText = "world" }
        };
        var feedbacks = new List<AiFeedback>
        {
            new()
            {
                CandidateAnswerId = answerId,
                Score = 81,
                EvaluationStatus = AiFeedbackEvaluationStatus.Succeeded,
                StrengthsJson = "[\"clear\"]",
                ImprovementsJson = "[]"
            }
        };

        var dto = PracticeSessionFeedbackMapper.Map(session, questions, answers, feedbacks, lockTeaser: true);

        Assert.Equal(PracticeFeedbackAccessLevel.FreeTeaser, dto.AccessLevel);
        var scored = dto.Items.First(i => i.QuestionId == questions[0].Id);
        var other = dto.Items.First(i => i.QuestionId == questions[1].Id);
        Assert.False(scored.IsLocked);
        Assert.True(scored.IsTeaser);
        Assert.Equal(81, scored.Score);
        Assert.True(other.IsLocked);
        Assert.Null(other.Score);
        Assert.Null(dto.AiInsight);
    }

    private static PracticeSession NewSession() => new()
    {
        Id = Guid.NewGuid(),
        Status = PracticeSessionStatus.Completed,
        OverallScore = 40.5,
        AiInsightVi = "Ổn",
        AiInsightEn = "OK",
        SkillsToImproveJson = """{"vi":["SQL"],"en":["SQL"]}"""
    };

    private static List<PublishedQuestionRow> NewQuestions() =>
    [
        new() { Id = Guid.NewGuid(), Order = 1, Question = "Q1", QuestionType = "technical", Difficulty = "medium" },
        new() { Id = Guid.NewGuid(), Order = 2, Question = "Q2", QuestionType = "behavioral", Difficulty = "easy" }
    ];
}
