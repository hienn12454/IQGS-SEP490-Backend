namespace ApplicationLayer.Settings;

public class CvSettings
{
    public const string SectionName = "Cv";

    public int MaxFileSizeMb { get; set; } = 5;
    public string[] AllowedExtensions { get; set; } = [".pdf", ".docx", ".jpg", ".jpeg", ".png"];
}
