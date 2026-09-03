using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Helpers;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

/// <summary>SCRUM-428: slice outline + apply RAG regen.</summary>
public sealed class StudioQuestionRegenHelperTests
{
    [Fact]
    public void ResolveSlot_UsesOutlineOrder()
    {
        var q = new InterviewQuestion
        {
            Content = "old",
            OrderIndex = 2,
            Type = QuestionType.Technical,
            Difficulty = QuestionDifficulty.Easy,
            TagsJson = """{"skill":"X","focusArea":"X","rationale":"old goal"}"""
        };
        var planJson = """
        {
          "recommendedQuestionOutline": [
            { "order": 1, "type": "technical", "difficulty": "easy", "skill": "A", "focusArea": "A", "goal": "G1", "answerMethod": "Text" },
            { "order": 2, "type": "technical", "difficulty": "medium", "skill": "SOLID", "focusArea": "SRP", "goal": "Đánh giá SOLID", "answerMethod": "Code",
              "citations": [{ "sourceFile": "job-description", "chunkIndex": 1, "excerpt": "OOP", "origin": "HR", "usedFor": ["why-asked"] }] }
          ]
        }
        """;

        var slot = StudioQuestionRegenHelper.ResolveSlot(q, planJson);
        Assert.Equal(2, slot.Order);
        Assert.Equal("SOLID", slot.Skill);
        Assert.Equal("Đánh giá SOLID", slot.Goal);
        Assert.Equal("Code", slot.AnswerMethod);
        Assert.NotNull(slot.Citations);
        Assert.Single(slot.Citations!);
    }

    [Fact]
    public void BuildSingleSlotApprovedPlan_TotalOne()
    {
        var slot = new PlanOutlineItemDto(1, "technical", "easy", "OOP", "Encapsulation", "Đánh giá OOP", "Code");
        var plan = StudioQuestionRegenHelper.BuildSingleSlotApprovedPlan(
            """{"roleTitle":"Dev","totalQuestions":30,"summary":"full"}""",
            slot);

        var json = System.Text.Json.JsonSerializer.Serialize(plan);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("totalQuestions").GetInt32());
        Assert.True(root.TryGetProperty("recommendedQuestionOutline", out var outline));
        Assert.Equal(1, outline.GetArrayLength());
        Assert.Equal("OOP", outline[0].GetProperty("skill").GetString());
    }

    [Fact]
    public void ApplyRagResult_LocksRationaleToGoal()
    {
        var target = new InterviewQuestion
        {
            Content = "old",
            OrderIndex = 1,
            Type = QuestionType.Technical,
            Difficulty = QuestionDifficulty.Easy,
            TagsJson = """{"attachedImageBlobPath":"projects/p/q.png"}"""
        };
        var slot = new PlanOutlineItemDto(1, "technical", "medium", "SOLID", "SRP", "Đánh giá SOLID", "Code");
        var rag = new RagGeneratedQuestionDto
        {
            Question = "Giải thích SRP với ví dụ?",
            QuestionType = "technical",
            Difficulty = "medium",
            Rationale = "LLM viết khác",
            SampleAnswer = "SRP là…",
            Skill = "SOLID",
            FocusArea = "SRP",
            AnswerMethod = "Code",
            CodeTemplateType = "CODE_COMPLETION",
            CodeSnippet = "class X {}"
        };

        StudioQuestionRegenHelper.ApplyRagResultToQuestion(target, rag, slot, true, true);

        Assert.Equal("Giải thích SRP với ví dụ?", target.Content);
        Assert.Equal("SRP là…", target.ExpectedAnswer);
        var meta = StudioRagQuestionMapper.ParseMeta(target.TagsJson);
        Assert.Equal("Đánh giá SOLID", meta.Rationale);
        Assert.Equal("projects/p/q.png", meta.AttachedImageBlobPath);
        Assert.Equal("CODE_COMPLETION", meta.CodeTemplateType);
    }

    [Fact]
    public void NormalizeInstruction_TrimsAndFlattens()
    {
        Assert.Null(StudioQuestionRegenHelper.NormalizeInstruction("  "));
        Assert.Equal("Làm khó hơn", StudioQuestionRegenHelper.NormalizeInstruction("  Làm khó hơn \n "));
    }

    [Fact]
    public void BuildAvoidQuestionsNote_TruncatesAndJoins()
    {
        var longOne = new string('x', 200);
        var note = StudioQuestionRegenHelper.BuildAvoidQuestionsNote(
            ["Câu ngắn", "  ", longOne, "Câu ba"],
            maxItems: 3,
            maxLenEach: 120);

        Assert.NotNull(note);
        Assert.StartsWith("AVOID_QUESTIONS=", note);
        Assert.Contains("|#|", note);
        Assert.Contains("Câu ngắn", note);
        Assert.Contains("Câu ba", note); // empty skipped → short, truncated long, câu ba
        Assert.Contains("…", note);
        Assert.DoesNotContain(longOne, note);
    }

    [Fact]
    public void BuildAvoidQuestionsNote_NullWhenEmpty()
    {
        Assert.Null(StudioQuestionRegenHelper.BuildAvoidQuestionsNote([]));
        Assert.Null(StudioQuestionRegenHelper.BuildAvoidQuestionsNote(["  ", "\n"]));
    }

    [Fact]
    public void BuildRegenHrNote_AppendsAvoid()
    {
        var note = StudioQuestionRegenHelper.BuildRegenHrNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Mixed", ["BUG_DETECTION"], "LANG=vi",
            ["OOP"], "Làm khó hơn",
            "AVOID_QUESTIONS=Câu A|#|Câu B");

        Assert.Contains("STUDIO_REGEN=1", note);
        Assert.Contains("HR_REGEN_NOTE=Làm khó hơn", note);
        Assert.Contains("AVOID_QUESTIONS=Câu A|#|Câu B", note);
    }
}
