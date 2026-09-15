using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Admin;

public class PlatformSettingsDto
{
    public int MinQuestionsToPublish { get; set; }

    /// <summary>SCRUM-404: số bộ tối đa được ghim cùng lúc.</summary>
    public int MaxPinnedSets { get; set; }

    /// <summary>SCRUM-404: ngưỡng lượt practice để badge Trending.</summary>
    public int MinAttemptsForTrending { get; set; }

    /// <summary>SCRUM-446: bật/tắt chống gian lận toàn hệ thống.</summary>
    public bool AntiCheatEnabled { get; set; }

    /// <summary>SCRUM-446: số lần rời tab tối đa trước khi tự nộp.</summary>
    public int AntiCheatMaxTabLeaves { get; set; }
}

public class UpdatePlatformSettingsDto
{
    /// <summary>Số câu hỏi tối thiểu để HR publish 1 bộ câu hỏi lên marketplace — hạ xuống để tiện test trên web.</summary>
    [Range(1, 100, ErrorMessage = "Số câu hỏi tối thiểu để publish phải từ 1 đến 100.")]
    public int MinQuestionsToPublish { get; set; }

    /// <summary>SCRUM-404: giới hạn số bộ Admin được ghim.</summary>
    [Range(0, 50, ErrorMessage = "Số bộ ghim tối đa phải từ 0 đến 50.")]
    public int MaxPinnedSets { get; set; }

    /// <summary>SCRUM-404: ngưỡng AttemptCount cho badge Trending.</summary>
    [Range(1, 10000, ErrorMessage = "Ngưỡng Trending phải từ 1 đến 10000.")]
    public int MinAttemptsForTrending { get; set; }

    /// <summary>SCRUM-446: bật/tắt chống gian lận — áp dụng cho phiên practice mới.</summary>
    public bool AntiCheatEnabled { get; set; }

    /// <summary>SCRUM-446: số lần rời tab tối đa (1–20).</summary>
    [Range(1, 20, ErrorMessage = "Số lần rời tab tối đa phải từ 1 đến 20.")]
    public int AntiCheatMaxTabLeaves { get; set; }
}
