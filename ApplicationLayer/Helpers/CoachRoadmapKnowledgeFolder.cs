using Microsoft.Extensions.Configuration;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Folder SYSTEM dùng khi RAG lộ trình Coach (reorder/giải thích topic).
/// Config: Coach:RoadmapKnowledgeFolder — mặc định coach-roadmap.
/// </summary>
public static class CoachRoadmapKnowledgeFolder
{
    public const string DefaultFolder = "coach-roadmap";
    public const string ConfigKey = "Coach:RoadmapKnowledgeFolder";

    public const string KbSourceSystem = "system";
    public const string KbSourceInferred = "inferred";

    public static string Resolve(IConfiguration? config)
    {
        var raw = config?[ConfigKey];
        try
        {
            var normalized = KnowledgeFolderHelper.Normalize(raw);
            return normalized ?? DefaultFolder;
        }
        catch
        {
            return DefaultFolder;
        }
    }
}
