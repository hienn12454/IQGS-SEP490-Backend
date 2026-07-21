using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Admin;

public class PlatformSettingsDto
{
    public int MinQuestionsToPublish { get; set; }
}

public class UpdatePlatformSettingsDto
{
    /// <summary>Số câu hỏi tối thiểu để HR publish 1 bộ câu hỏi lên marketplace — hạ xuống để tiện test trên web.</summary>
    [Range(1, 100, ErrorMessage = "Số câu hỏi tối thiểu để publish phải từ 1 đến 100.")]
    public int MinQuestionsToPublish { get; set; }
}
