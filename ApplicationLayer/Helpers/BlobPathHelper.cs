namespace ApplicationLayer.Helpers;

public static class BlobPathHelper
{
    public static string BuildBlobPath(string scope, Guid documentId, string fileName)
    {
        var safeName = SanitizeFileName(fileName);
        return $"knowledge-base/{scope.ToLowerInvariant()}/{documentId}/{safeName}";
    }

    /// <summary>SCRUM-396: path ảnh đính kèm câu hỏi Studio.</summary>
    public static string BuildQuestionImagePath(Guid projectId, Guid questionId, string fileName)
    {
        var safeName = SanitizeFileName(fileName);
        return $"question-media/{projectId:D}/{questionId:D}/{safeName}";
    }

    /// <summary>SCRUM-396: path ảnh question-set: question-set-media/{setId}/{questionId}/{file}</summary>
    public static string BuildQuestionSetImagePath(Guid questionSetId, Guid questionId, string fileName)
    {
        var safeName = SanitizeFileName(fileName);
        return $"question-set-media/{questionSetId:D}/{questionId:D}/{safeName}";
    }

    /// <summary>SCRUM-465: file JD gốc (Studio project).</summary>
    public static string BuildStudioJobDescriptionPath(Guid projectId, string fileName)
        => $"job-descriptions/studio/{projectId:N}/{Guid.NewGuid():N}-{SanitizeFileName(fileName)}";

    /// <summary>SCRUM-465: file JD gốc (QuestionSet History upload).</summary>
    public static string BuildQuestionSetJobDescriptionPath(Guid questionSetId, string fileName)
        => $"job-descriptions/question-sets/{questionSetId:N}/{Guid.NewGuid():N}-{SanitizeFileName(fileName)}";

    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "document" : name;
    }
}
