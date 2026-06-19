using System.Text.Json;

namespace ApplicationLayer.Debugging;

/// <summary>NDJSON debug log cho agent session — không log secret/PII.</summary>
public static class AgentDebugLog
{
    private const string SessionId = "646770";
    private static readonly string LogPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "debug-646770.log"));

    public static void Write(string hypothesisId, string location, string message, object? data = null, string runId = "pre-fix")
    {
        try
        {
            var entry = new Dictionary<string, object?>
            {
                ["sessionId"] = SessionId,
                ["runId"] = runId,
                ["hypothesisId"] = hypothesisId,
                ["location"] = location,
                ["message"] = message,
                ["data"] = data,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
            File.AppendAllText(LogPath, line);
        }
        catch
        {
            // Không làm hỏng request vì debug log
        }
    }
}
