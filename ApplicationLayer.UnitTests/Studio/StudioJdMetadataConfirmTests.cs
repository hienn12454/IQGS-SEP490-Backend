using ApplicationLayer.Studio.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

public sealed class StudioJdSeniorityTests
{
    [Theory]
    [InlineData("junior", "Junior")]
    [InlineData("Senior", "Senior")]
    [InlineData("mid-level", "Mid")]
    [InlineData("intern", "Intern")]
    [InlineData("Lead", "Lead")]
    public void NormalizeDisplay_AcceptsKnownAliases(string input, string expected)
    {
        Assert.Equal(expected, StudioJdSeniority.NormalizeDisplay(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("expert")]
    [InlineData("Không xác định")]
    public void NormalizeDisplay_RejectsInvalid(string? input)
    {
        Assert.Null(StudioJdSeniority.NormalizeDisplay(input));
    }

    [Fact]
    public void ToRagExperienceLevel_ReturnsLowercase()
    {
        Assert.Equal("junior", StudioJdSeniority.ToRagExperienceLevel("Junior"));
        Assert.Equal("mid", StudioJdSeniority.ToRagExperienceLevel("mid-level"));
        Assert.Null(StudioJdSeniority.ToRagExperienceLevel(null));
    }
}

public sealed class StudioRagPlanHrNoteBuilderTests
{
    [Fact]
    public void BuildInitial_IncludesConfirmedPositionRoleAndSeniority()
    {
        var note = StudioRagPlanHrNoteBuilder.BuildInitial(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            selectedDocs: 0,
            totalQuestions: 15,
            interviewMinutes: 60,
            language: "Vietnamese",
            targetPosition: "Backend Developer",
            confirmedRole: "Backend Developer",
            confirmedSeniority: "Junior");

        Assert.Contains("Vị trí mục tiêu: Backend Developer", note);
        Assert.Contains("Vai trò: Backend Developer", note);
        Assert.Contains("Cấp độ bắt buộc (HR đã xác nhận): Junior", note);
        Assert.Contains("experience_level BẮT BUỘC = junior", note);
        Assert.Contains("STUDIO_UI_PLAN=1", note);
    }
}
