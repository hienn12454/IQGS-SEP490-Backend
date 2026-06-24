namespace ApplicationLayer.Settings;

public class WatchdogSettings
{
    public const string SectionName = "Watchdog";

    /// <summary>Document PROCESSING quá N phút → FAILED.</summary>
    public int StuckDocumentProcessingMinutes { get; set; } = 90;

    /// <summary>Job PLAN/QUESTION_PROCESSING quá N phút → FAILED.</summary>
    public int StuckQuestionGenerationMinutes { get; set; } = 30;
}
