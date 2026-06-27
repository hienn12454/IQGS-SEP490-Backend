using ApplicationLayer.DTOs.Candidate;

namespace ApplicationLayer.Interfaces.Services;

public interface ICandidateCvService
{
    Task<CvUploadResponseDto> UploadAsync(
        Stream fileStream, string fileName, string contentType, long fileLength, Guid userId);
    Task<CvDownloadResponseDto> GetAsync(Guid userId);
    Task DeleteAsync(Guid userId);
}
