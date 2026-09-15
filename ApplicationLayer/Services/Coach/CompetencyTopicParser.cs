using System.Text.Json;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-453: đọc TopicsJson của framework skill.
/// Có 2 dạng dữ liệu cùng tồn tại (không backfill thủ công):
///  - dạng cũ:  ["OOP", "LINQ"]
///  - dạng mới: [{ "topic": "OOP", "subtopics": ["Inheritance", "Polymorphism"] }]
/// </summary>
public static class CompetencyTopicParser
{
    public sealed record TopicNode(string Topic, List<string> Subtopics);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static List<TopicNode> Parse(string? json)
    {
        var result = new List<TopicNode>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var name = element.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(name))
                        result.Add(new TopicNode(name, new List<string>()));
                    continue;
                }

                if (element.ValueKind != JsonValueKind.Object) continue;

                var topic = element.TryGetProperty("topic", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString()?.Trim()
                    : null;
                if (string.IsNullOrEmpty(topic)) continue;

                var subtopics = new List<string>();
                if (element.TryGetProperty("subtopics", out var subs) && subs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in subs.EnumerateArray())
                    {
                        var value = s.ValueKind == JsonValueKind.String ? s.GetString()?.Trim() : null;
                        if (!string.IsNullOrEmpty(value)) subtopics.Add(value);
                    }
                }
                result.Add(new TopicNode(topic, subtopics));
            }
        }
        catch (JsonException)
        {
            return new List<TopicNode>();
        }
        return result;
    }

    /// <summary>Chỉ lấy tên topic — dùng cho blueprint diagnostic.</summary>
    public static List<string> ParseTopicNames(string? json)
        => Parse(json).Select(n => n.Topic).ToList();

    public static string Serialize(IEnumerable<TopicNode> nodes)
        => JsonSerializer.Serialize(
            nodes.Select(n => new { topic = n.Topic, subtopics = n.Subtopics }),
            JsonOpts);
}
