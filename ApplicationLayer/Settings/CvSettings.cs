namespace ApplicationLayer.Settings;

public class CvSettings
{
    public const string SectionName = "Cv";

    public int MaxFileSizeMb { get; set; } = 10;
    public string[] AllowedExtensions { get; set; } = [".pdf", ".docx"];
}
