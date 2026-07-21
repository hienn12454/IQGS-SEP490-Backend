using ApplicationLayer.DTOs.Candidate;

namespace ApplicationLayer.Interfaces.Services;

public interface ICandidateCvService
{
    Task<CvEvaluationResponseDto> UploadAsync(
        Stream fileStream, string fileName, string contentType, long fileLength, Guid userId,
        CancellationToken ct = default);
    Task<CvEvaluationResponseDto> GetAsync(Guid userId);
    Task DeleteAsync(Guid userId);

    /// <summary>Xem cài đặt tự đồng bộ thông tin cá nhân từ CV vào profile — mặc định true nếu candidate chưa từng đổi.</summary>
    Task<CvSyncSettingsDto> GetSyncSettingsAsync(Guid userId);

    /// <summary>Bật/tắt tự đồng bộ thông tin cá nhân từ CV vào profile cho các lần upload sau.</summary>
    Task<CvSyncSettingsDto> UpdateSyncSettingsAsync(Guid userId, CvSyncSettingsDto dto);
}
