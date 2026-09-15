using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

/// <summary>
/// Canary: scoring/orchestration Coach không hardcode role cụ thể trong string literal.
/// Comment XML được bỏ qua vì được dùng để giải thích "không hardcode".
/// </summary>
public sealed class CoachRoleLiteralCanaryTests
{
    [Fact]
    public void CoachServices_HaveNoQuotedRoleLiterals()
    {
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ApplicationLayer", "Services", "Coach"));
        Assert.True(Directory.Exists(dir), dir);
        var banned = new[] { "dotnet-backend", "react-frontend", "java-backend", "\"react\"", "\".net\"" };
        foreach (var file in Directory.GetFiles(dir, "*.cs"))
        {
            var stripped = StripComments(File.ReadAllText(file));
            foreach (var token in banned)
            {
                Assert.False(
                    stripped.Contains(token, StringComparison.OrdinalIgnoreCase),
                    $"{Path.GetFileName(file)} chứa literal {token}");
            }
        }
    }

    private static string StripComments(string source)
    {
        var lines = source.Split('\n')
            .Select(l =>
            {
                var idx = l.IndexOf("//", StringComparison.Ordinal);
                return idx >= 0 ? l[..idx] : l;
            });
        var joined = string.Join('\n', lines);
        while (true)
        {
            var start = joined.IndexOf("/*", StringComparison.Ordinal);
            if (start < 0) break;
            var end = joined.IndexOf("*/", start + 2, StringComparison.Ordinal);
            if (end < 0) break;
            joined = joined.Remove(start, end + 2 - start);
        }
        return joined;
    }
}
