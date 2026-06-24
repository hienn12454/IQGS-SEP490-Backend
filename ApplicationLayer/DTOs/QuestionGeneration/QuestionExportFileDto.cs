namespace ApplicationLayer.DTOs.QuestionGeneration;

public class QuestionExportFileDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
