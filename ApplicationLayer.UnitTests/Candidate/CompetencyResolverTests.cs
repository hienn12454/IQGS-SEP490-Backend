using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Moq;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class CompetencyResolverTests
{
    private static CompetencyRoleFamily Family(string key, string display, int order = 1)
        => new() { FamilyKey = key, DisplayName = display, Status = CompetencyFrameworkStatus.Active, SortOrder = order };

    [Fact]
    public void MatchFamilyKey_ExactAlias_Be_MapsToFamily()
    {
        var families = new List<CompetencyRoleFamily> { Family("backend", "Backend Developer") };
        var aliases = new List<CompetencyRoleFamilyAlias>
        {
            new() { FamilyKey = "backend", Alias = "be", MatchKind = CompetencyRoleAliasMatchKind.Exact, SortOrder = 1 }
        };
        Assert.Equal("backend", CompetencyResolver.MatchFamilyKey("BE", families, aliases));
    }

    [Fact]
    public async Task Resolve_FrameworkHit_ReturnsFramework()
    {
        var fw = new CompetencyFramework
        {
            Id = Guid.NewGuid(),
            RoleKey = "java-backend",
            DisplayRole = "Java Backend",
            TargetLevel = "Junior",
            RoleFamilyKey = "backend",
            Status = CompetencyFrameworkStatus.Active,
            Skills = { new CompetencyFrameworkSkill { Skill = "Spring", ImportanceWeight = 0.5 } }
        };
        var (resolver, _) = Build(fw, families: [Family("backend", "Backend Developer")]);
        var result = await resolver.ResolveAsync("Java Backend", "Junior", ["Spring"]);
        Assert.Equal(CompetencyResolutionMode.Framework, result.ResolutionMode);
        Assert.Equal(fw.Id, result.FrameworkId);
    }

    [Fact]
    public async Task Resolve_FamilyWithoutFramework_ReturnsAdaptive()
    {
        var (resolver, _) = Build(null, families: [Family("backend", "Backend Developer")],
            aliases:
            [
                new CompetencyRoleFamilyAlias
                {
                    FamilyKey = "backend",
                    Alias = "backend",
                    MatchKind = CompetencyRoleAliasMatchKind.Contains
                }
            ]);
        var result = await resolver.ResolveAsync("Go Backend Developer", "Junior", ["Go"]);
        Assert.Equal(CompetencyResolutionMode.Adaptive, result.ResolutionMode);
        Assert.Null(result.FrameworkId);
        Assert.Equal("backend", result.RoleFamilyKey);
    }

    [Fact]
    public async Task Resolve_UnknownDomain_ReturnsUnsupported()
    {
        var (resolver, _) = Build(null, families: [Family("backend", "Backend Developer")]);
        var result = await resolver.ResolveAsync("Accountant", "Junior", ["Excel"]);
        Assert.Equal(CompetencyResolutionMode.Unsupported, result.ResolutionMode);
        Assert.Contains("Backend Developer", result.SupportedRoles);
    }

    [Fact]
    public async Task Resolve_FamilyPlusSkillOverlap_StillFramework()
    {
        var fw = new CompetencyFramework
        {
            Id = Guid.NewGuid(),
            RoleKey = "java-backend",
            DisplayRole = "Java Backend",
            TargetLevel = "Junior",
            RoleFamilyKey = "backend",
            Technology = "Spring",
            Status = CompetencyFrameworkStatus.Active,
            Skills =
            {
                new CompetencyFrameworkSkill { Skill = "Spring" },
                new CompetencyFrameworkSkill { Skill = "SQL" }
            }
        };
        var (resolver, fwResolver) = Build(fw, families: [Family("backend", "Backend Developer")],
            aliases:
            [
                new CompetencyRoleFamilyAlias
                {
                    FamilyKey = "backend",
                    Alias = "backend",
                    MatchKind = CompetencyRoleAliasMatchKind.Contains
                }
            ]);
        fwResolver.Setup(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new FrameworkResolution(null, null, "Junior", false));
        var result = await resolver.ResolveAsync("Backend Developer", "Junior", ["Spring", "SQL"]);
        Assert.Equal(CompetencyResolutionMode.Framework, result.ResolutionMode);
    }

    private static (CompetencyResolver Resolver, Mock<ICompetencyFrameworkResolver> Fw) Build(
        CompetencyFramework? fw,
        List<CompetencyRoleFamily>? families = null,
        List<CompetencyRoleFamilyAlias>? aliases = null)
    {
        var fwResolver = new Mock<ICompetencyFrameworkResolver>();
        fwResolver.Setup(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new FrameworkResolution(fw, fw?.RoleKey, fw?.TargetLevel ?? "Junior", false));

        var frameworks = new Mock<ICompetencyFrameworkRepository>();
        frameworks.Setup(r => r.ListActiveWithSkillsAsync())
            .ReturnsAsync(fw is null ? [] : [fw]);

        var familyRepo = new Mock<ICompetencyRoleFamilyRepository>();
        familyRepo.Setup(r => r.ListActiveAsync()).ReturnsAsync(families ?? []);
        familyRepo.Setup(r => r.ListAliasesAsync()).ReturnsAsync(aliases ?? []);

        return (new CompetencyResolver(fwResolver.Object, frameworks.Object, familyRepo.Object), fwResolver);
    }
}
