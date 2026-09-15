using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Moq;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class CompetencyFrameworkResolverTests
{
    [Fact]
    public void MatchRoleKey_UsesAliasData_NotHardcodedStack()
    {
        var frameworks = new List<CompetencyFramework>
        {
            new() { RoleKey = "dotnet-backend", DisplayRole = ".NET Backend", TargetLevel = "Junior" },
            new() { RoleKey = "react-frontend", DisplayRole = "React Frontend", TargetLevel = "Junior" }
        };
        var aliases = new List<CompetencyRoleAlias>
        {
            new() { RoleKey = "dotnet-backend", Alias = ".net", MatchKind = CompetencyRoleAliasMatchKind.Contains, SortOrder = 2 },
            new() { RoleKey = "dotnet-backend", Alias = "asp.net core", MatchKind = CompetencyRoleAliasMatchKind.Contains, SortOrder = 1 },
            new() { RoleKey = "react-frontend", Alias = "react", MatchKind = CompetencyRoleAliasMatchKind.Contains, SortOrder = 1 }
        };

        Assert.Equal("react-frontend", CompetencyFrameworkResolver.MatchRoleKey("React Frontend Developer", frameworks, aliases));
        Assert.Equal("dotnet-backend", CompetencyFrameworkResolver.MatchRoleKey("ASP.NET Core Developer", frameworks, aliases));
    }

    [Fact]
    public void MatchRoleKey_UnknownRole_ReturnsNull()
    {
        var frameworks = new List<CompetencyFramework>
        {
            new() { RoleKey = "dotnet-backend", DisplayRole = ".NET Backend", TargetLevel = "Junior" }
        };
        Assert.Null(CompetencyFrameworkResolver.MatchRoleKey("React Frontend Developer", frameworks, []));
        Assert.Null(CompetencyFrameworkResolver.MatchRoleKey("Go Backend", frameworks, []));
    }

    [Fact]
    public async Task ResolveAsync_DoesNotFallbackToOtherRole()
    {
        var repo = new Mock<ICompetencyFrameworkRepository>();
        repo.Setup(r => r.ListActiveWithSkillsAsync()).ReturnsAsync(
        [
            new CompetencyFramework { RoleKey = "dotnet-backend", DisplayRole = ".NET Backend", TargetLevel = "Junior", Status = "Active" }
        ]);
        repo.Setup(r => r.ListAliasesAsync()).ReturnsAsync(
        [
            new CompetencyRoleAlias { RoleKey = "dotnet-backend", Alias = ".net", MatchKind = CompetencyRoleAliasMatchKind.Contains }
        ]);

        var resolver = new CompetencyFrameworkResolver(repo.Object);
        var result = await resolver.ResolveAsync("React Frontend Developer", "Junior");
        Assert.False(result.Matched);
        Assert.Null(result.Framework);
    }
}
