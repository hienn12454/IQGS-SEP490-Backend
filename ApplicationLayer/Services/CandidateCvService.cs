using System.Text.Json;
using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Rag;
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
    private readonly IRagService _ragService;
    private readonly CvSettings _cvSettings;
    private readonly BlobStorageSettings _blobSettings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public CandidateCvService(
        ICandidateProfileRepository candidateProfileRepository,
        IBlobStorageService blobStorage,
        IRagService ragService,
        IOptions<CvSettings> cvSettings,
        IOptions<BlobStorageSettings> blobSettings)
    {
        _candidateProfileRepository = candidateProfileRepository;
        _blobStorage = blobStorage;
        _ragService = ragService;
        _cvSettings = cvSettings.Value;
        _blobSettings = blobSettings.Value;
    }

    public async Task<CvEvaluationResponseDto> UploadAsync(
        Stream fileStream, string fileName, string contentType, long fileLength, Guid userId,
        CancellationToken ct = default)
    {
        ValidateUpload(fileName, fileLength);

        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, ct);
        var fileBytes = buffer.ToArray();

        var profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
        var oldBlobPath = profile?.CvBlobPath;

        var safeName = BlobPathHelper.SanitizeFileName(fileName);
        var newBlobPath = $"cv/{userId}/{Guid.NewGuid()}/{safeName}";

        using (var blobStream = new MemoryStream(fileBytes))
            await _blobStorage.UploadAsync(blobStream, contentType, newBlobPath, ct);

        var isNewProfile = profile is null;
        profile ??= new CandidateProfile { UserId = userId };
        profile.CvFileName = fileName;
        profile.CvBlobPath = newBlobPath;
        profile.CvContentType = contentType;
        profile.CvUploadedAt = DateTime.UtcNow;

        if (isNewProfile)
            await _candidateProfileRepository.AddAsync(profile);
        else
            await _candidateProfileRepository.UpdateAsync(profile);

        // Xóa CV cũ SAU KHI đã lưu CV mới thành công — tránh mất CV nếu upload/lưu DB lỗi giữa chừng.
        if (!string.IsNullOrEmpty(oldBlobPath) && oldBlobPath != newBlobPath)
        {
            try { await _blobStorage.DeleteAsync(oldBlobPath, ct); }
            catch { /* best-effort — CV mới đã lưu thành công, không chặn luồng chính vì blob cũ mồ côi */ }
        }

        // Gọi RAG parse-cv — nếu lỗi/timeout, RagService đã tự bọc thành exception tiếng Việt rõ ràng (giống ParseJdAsync)
        // và ném ra ngoài — CV vẫn đã lưu thành công ở bước trên, chỉ TechStack/đánh giá giữ nguyên giá trị cũ (AC-06).
        ParseCvResult parseResult;
        using (var parseStream = new MemoryStream(fileBytes))
            parseResult = await _ragService.ParseCvAsync(parseStream, fileName, ct);

        profile.TechStack = parseResult.Skills.ToArray();
        profile.CvEvaluationJson = JsonSerializer.Serialize(
            new { skills = parseResult.Skills, summary = parseResult.Summary }, JsonOptions);
        profile.CvParsedAt = DateTime.UtcNow;
        await _candidateProfileRepository.UpdateAsync(profile);

        var downloadUrl = await _blobStorage.GenerateReadSasUrlAsync(
            newBlobPath, TimeSpan.FromMinutes(_blobSettings.SasExpiryMinutes), ct);

        return new CvEvaluationResponseDto
        {
            CvFileName = fileName,
            Skills = parseResult.Skills,
            Summary = parseResult.Summary,
            TechStack = profile.TechStack.ToList(),
            ParsedAt = profile.CvParsedAt,
            UploadedAt = profile.CvUploadedAt,
            DownloadUrl = downloadUrl
        };
    }

    public async Task<CvEvaluationResponseDto> GetAsync(Guid userId)
    {
        var profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
        if (profile?.CvBlobPath is null)
            throw new NotFoundException("Bạn chưa tải lên CV nào.");

        var (skills, summary) = ParseEvaluation(profile.CvEvaluationJson);

        var downloadUrl = await _blobStorage.GenerateReadSasUrlAsync(
            profile.CvBlobPath, TimeSpan.FromMinutes(_blobSettings.SasExpiryMinutes));

        return new CvEvaluationResponseDto
        {
            CvFileName = profile.CvFileName ?? "cv",
            Skills = skills,
            Summary = summary,
            TechStack = profile.TechStack.ToList(),
            ParsedAt = profile.CvParsedAt,
            UploadedAt = profile.CvUploadedAt,
            DownloadUrl = downloadUrl
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
        profile.CvParsedAt = null;
        profile.CvEvaluationJson = null;
        await _candidateProfileRepository.UpdateAsync(profile);
    }

    private static (List<string> Skills, string? Summary) ParseEvaluation(string? evaluationJson)
    {
        if (string.IsNullOrWhiteSpace(evaluationJson))
            return (new List<string>(), null);

        try
        {
            using var doc = JsonDocument.Parse(evaluationJson);
            var skills = doc.RootElement.TryGetProperty("skills", out var skillsEl)
                ? JsonSerializer.Deserialize<List<string>>(skillsEl.GetRawText(), JsonOptions) ?? new()
                : new List<string>();
            var summary = doc.RootElement.TryGetProperty("summary", out var summaryEl)
                ? summaryEl.GetString()
                : null;
            return (skills, summary);
        }
        catch (JsonException)
        {
            return (new List<string>(), null);
        }
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
