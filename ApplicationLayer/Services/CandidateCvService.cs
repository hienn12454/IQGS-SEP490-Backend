using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Settings;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services;

public class CandidateCvService : ICandidateCvService
{
    private readonly ICandidateProfileRepository _candidateProfileRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly CvSettings _cvSettings;
    private readonly BlobStorageSettings _blobSettings;

    public CandidateCvService(
        ICandidateProfileRepository candidateProfileRepository,
        IBlobStorageService blobStorage,
        IOptions<CvSettings> cvSettings,
        IOptions<BlobStorageSettings> blobSettings)
    {
        _candidateProfileRepository = candidateProfileRepository;
        _blobStorage = blobStorage;
        _cvSettings = cvSettings.Value;
        _blobSettings = blobSettings.Value;
    }

    public async Task<CvUploadResponseDto> UploadAsync(
        Stream fileStream, string fileName, string contentType, long fileLength, Guid userId)
    {
        ValidateUpload(fileName, fileLength);

        var profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
        var oldBlobPath = profile?.CvBlobPath;

        var safeName = BlobPathHelper.SanitizeFileName(fileName);
        var newBlobPath = $"cv/{userId}/{Guid.NewGuid()}/{safeName}";

        await _blobStorage.UploadAsync(fileStream, contentType, newBlobPath);

        var uploadedAt = DateTime.UtcNow;

        if (profile is null)
        {
            profile = new CandidateProfile
            {
                UserId = userId,
                CvFileName = fileName,
                CvBlobPath = newBlobPath,
                CvContentType = contentType,
                CvUploadedAt = uploadedAt
            };
            await _candidateProfileRepository.AddAsync(profile);
        }
        else
        {
            profile.CvFileName = fileName;
            profile.CvBlobPath = newBlobPath;
            profile.CvContentType = contentType;
            profile.CvUploadedAt = uploadedAt;
            await _candidateProfileRepository.UpdateAsync(profile);
        }

        // Xóa CV cũ SAU KHI đã lưu CV mới thành công — tránh mất CV nếu upload/lưu DB lỗi giữa chừng.
        if (!string.IsNullOrEmpty(oldBlobPath) && oldBlobPath != newBlobPath)
        {
            try { await _blobStorage.DeleteAsync(oldBlobPath); }
            catch { /* best-effort — CV mới đã lưu thành công, không chặn luồng chính vì blob cũ mồ côi */ }
        }

        return new CvUploadResponseDto
        {
            FileName = fileName,
            UploadedAt = uploadedAt
        };
    }

    public async Task<CvDownloadResponseDto> GetAsync(Guid userId)
    {
        var profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
        if (profile?.CvBlobPath is null)
            throw new NotFoundException("Bạn chưa tải lên CV nào.");

        var url = await _blobStorage.GenerateReadSasUrlAsync(
            profile.CvBlobPath, TimeSpan.FromMinutes(_blobSettings.SasExpiryMinutes));

        return new CvDownloadResponseDto
        {
            FileName = profile.CvFileName ?? "cv",
            UploadedAt = profile.CvUploadedAt ?? profile.UpdatedAt ?? profile.CreatedAt,
            DownloadUrl = url
        };
    }

    public async Task DeleteAsync(Guid userId)
    {
        var profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
        if (profile?.CvBlobPath is null)
            throw new NotFoundException("Bạn chưa tải lên CV nào.");

        await _blobStorage.DeleteAsync(profile.CvBlobPath);

        profile.CvFileName = null;
        profile.CvBlobPath = null;
        profile.CvContentType = null;
        profile.CvUploadedAt = null;
        await _candidateProfileRepository.UpdateAsync(profile);
    }

    private void ValidateUpload(string fileName, long fileLength)
    {
        if (fileLength == 0)
            throw new BadRequestException("File CV trống hoặc không hợp lệ.");

        var maxBytes = (long)_cvSettings.MaxFileSizeMb * 1024 * 1024;
        if (fileLength > maxBytes)
            throw new BadRequestException($"File CV vượt quá dung lượng cho phép ({_cvSettings.MaxFileSizeMb} MB).");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!_cvSettings.AllowedExtensions.Contains(ext))
            throw new BadRequestException(
                $"Định dạng file không được hỗ trợ. Chỉ chấp nhận: {string.Join(", ", _cvSettings.AllowedExtensions)}.");
    }
}
