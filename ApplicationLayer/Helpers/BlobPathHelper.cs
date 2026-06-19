namespace ApplicationLayer.Helpers;

public static class BlobPathHelper
{
    public static string BuildBlobPath(string scope, Guid documentId, string fileName)
    {
        var safeName = SanitizeFileName(fileName);
        return $"knowledge-base/{scope.ToLowerInvariant()}/{documentId}/{safeName}";
    }

    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "document" : name;
    }
}
