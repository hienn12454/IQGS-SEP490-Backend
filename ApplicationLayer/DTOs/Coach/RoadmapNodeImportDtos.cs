namespace ApplicationLayer.DTOs.Coach;

public class RoadmapNodeImportDto
{
    public string RoleKey { get; set; } = string.Empty;
    public string? Technology { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string? Subtopic { get; set; }
    public double Importance { get; set; } = 0.5;
    public List<string> Prerequisites { get; set; } = new();
    public List<string> NextTopics { get; set; } = new();
    public string? SourceTitle { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceVersion { get; set; }
    public int SortOrder { get; set; }
    /// <summary>SCRUM-486: gắn KB document làm nguồn xem cho Candidate.</summary>
    public Guid? KnowledgeDocumentId { get; set; }
}

public class RoadmapNodeImportRequestDto
{
    public List<RoadmapNodeImportDto> Nodes { get; set; } = new();
}

public class RoadmapNodeImportResultDto
{
    public bool Success { get; set; }
    public int Upserted { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class RoadmapNodeAdminDto
{
    public Guid Id { get; set; }
    public string RoleKey { get; set; } = string.Empty;
    public string? Technology { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string? Subtopic { get; set; }
    public double Importance { get; set; }
    public string? SourceTitle { get; set; }
    public string? SourceUrl { get; set; }
    public int SortOrder { get; set; }
    /// <summary>SCRUM-486</summary>
    public Guid? KnowledgeDocumentId { get; set; }
}

/// <summary>SCRUM-486: PATCH gắn KnowledgeDocumentId cho một node.</summary>
public class UpdateRoadmapNodeLinkDto
{
    public Guid? KnowledgeDocumentId { get; set; }
    /// <summary>true = gỡ link (set null).</summary>
    public bool ClearKnowledgeDocumentId { get; set; }
}

/// <summary>SCRUM-486: gắn doc theo FileName khớp cuối SourceUrl của nodes.</summary>
public class LinkRoadmapNodesByFilenameDto
{
    public Guid KnowledgeDocumentId { get; set; }
}

public class LinkRoadmapNodesByFilenameResultDto
{
    public int LinkedCount { get; set; }
    public List<Guid> LinkedNodeIds { get; set; } = new();
}
