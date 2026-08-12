using ApplicationLayer.DTOs.Profile;
using Xunit;

namespace ApplicationLayer.UnitTests.Profile;

public class UpdateCandidateProfileDtoValidatorTests
{
    private readonly UpdateCandidateProfileDtoValidator _validator = new();

    private static UpdateCandidateProfileDto Dto(string? timeZoneId) => new()
    {
        FullName = "Nguyễn Văn A",
        TimeZoneId = timeZoneId
    };

    [Fact]
    public void NullTimeZoneId_IsValid()
    {
        var result = _validator.Validate(Dto(null));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyTimeZoneId_IsValid()
    {
        var result = _validator.Validate(Dto(""));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Asia/Ho_Chi_Minh")]
    [InlineData("UTC")]
    [InlineData("America/New_York")]
    public void ValidIanaTimeZoneId_IsValid(string timeZoneId)
    {
        var result = _validator.Validate(Dto(timeZoneId));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Not/A_Real_Zone")]
    [InlineData("Asia/Not_A_City")]
    [InlineData("random-garbage")]
    public void InvalidTimeZoneId_IsInvalid(string timeZoneId)
    {
        var result = _validator.Validate(Dto(timeZoneId));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateCandidateProfileDto.TimeZoneId));
    }
}
