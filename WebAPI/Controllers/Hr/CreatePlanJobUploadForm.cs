namespace WebAPI.Controllers.Hr;

public class CreatePlanJobUploadForm
{
    public string? JobDescription { get; set; }
    public string? HrNote { get; set; }
    public IFormFile? File { get; set; }
    public int NumberOfQuestions { get; set; }
    public string Difficulty { get; set; } = "medium";
    /// <summary>Loại câu hỏi — chuỗi phân tách bằng dấu phẩy, VD: Technical, Behavioral</summary>
    public string? QuestionTypes { get; set; }
    /// <summary>Kỹ năng — chuỗi phân tách bằng dấu phẩy, VD: C#, PostgreSQL</summary>
    public string? Skills { get; set; }
}
