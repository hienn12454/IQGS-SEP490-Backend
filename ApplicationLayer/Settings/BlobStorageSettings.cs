namespace ApplicationLayer.Settings;

public class BlobStorageSettings
{
    public const string SectionName = "BlobStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "knowledge-documents";
    public int SasExpiryMinutes { get; set; } = 15;
}
