namespace DomainLayer.Entities;

/// <summary>
/// Cấu hình toàn hệ thống — chỉ có đúng 1 dòng (singleton, seed sẵn qua migration).
/// Admin chỉnh runtime qua API thay vì sửa appsettings.json + redeploy.
/// </summary>
public class PlatformSettings : BaseEntity
{
    /// <summary>Id cố định của dòng singleton duy nhất — seed sẵn, không tạo thêm dòng khác.</summary>
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Số câu hỏi tối thiểu để HR publish 1 bộ câu hỏi lên marketplace — Admin hạ xuống để tiện test. Mặc định 10.</summary>
    public int MinQuestionsToPublish { get; set; } = 10;
}
