using System.Text.Json;
using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services;

public class CandidateCvService : ICandidateCvService
{
    private readonly ICandidateProfileRepository _candidateProfileRepository;
    private readonly IUserRepository _userRepository;
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
        IUserRepository userRepository,
        IBlobStorageService blobStorage,
        IRagService ragService,
        IOptions<CvSettings> cvSettings,
        IOptions<BlobStorageSettings> blobSettings)
    {
        _candidateProfileRepository = candidateProfileRepository;
        _userRepository = userRepository;
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

        // Browser đôi khi gửi octet-stream/rỗng cho PDF — gán MIME đúng để iframe preview được.
        contentType = NormalizeCvContentType(fileName, contentType);

        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, ct);
        var fileBytes = buffer.ToArray();

        // SCRUM-466: parse + classify TRƯỚC khi lưu Blob/DB — fail giữ CV cũ.
        ParseCvResult parseResult;
        try
        {
            using var parseStream = new MemoryStream(fileBytes);
            parseResult = await _ragService.ParseCvAsync(parseStream, fileName, ct);
        }
        catch (StructuredHttpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ItDomainClassifyGate.CvClassifyFailed(ex.Message);
        }

        if (!parseResult.Success)
        {
            throw ItDomainClassifyGate.FromRagCvFailure(
                parseResult.Stage,
                StatusCodes.Status422UnprocessableEntity,
                parseResult.Detail ?? parseResult.Error,
                parseResult.Errors,
                parseResult.DocumentType,
                parseResult.IsItRole);
        }

        ItDomainClassifyGate.EnsureCvPass(
            parseResult.DocumentType,
            parseResult.IsItRole,
            parseResult.RejectReason);
        ItDomainClassifyGate.EnsureCvHasSkills(parseResult.Skills);

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

        profile.TechStack = parseResult.Skills.ToArray();
        profile.CvEvaluationJson = JsonSerializer.Serialize(
            new
            {
                skills = parseResult.Skills,
                summary = parseResult.Summary,
                suggestedRole = parseResult.SuggestedRole,
                yearsOfExperienceHint = parseResult.YearsOfExperienceHint
            }, JsonOptions);
        profile.CvParsedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(parseResult.SuggestedRole))
            profile.SuggestedRole = parseResult.SuggestedRole.Trim();
        if (parseResult.YearsOfExperienceHint is double years)
            profile.YearsOfExperience ??= years;
        // Upload CV mới → cần confirm lại context Coach
        profile.CoachContextConfirmed = false;
        profile.CoachContextConfirmedAt = null;

        var syncedFields = await ApplyCvProfileSyncAsync(profile, parseResult, userId);

        if (isNewProfile)
            await _candidateProfileRepository.AddAsync(profile);
        else
            await _candidateProfileRepository.UpdateAsync(profile);

        // Xóa CV cũ SAU KHI đã lưu CV mới thành công
        if (!string.IsNullOrEmpty(oldBlobPath) && oldBlobPath != newBlobPath)
        {
            try { await _blobStorage.DeleteAsync(oldBlobPath, ct); }
            catch { /* best-effort */ }
        }

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
            DownloadUrl = downloadUrl,
            AutoSyncProfileFromCv = profile.AutoSyncProfileFromCv,
            ProfileFieldsSynced = syncedFields,
            LockedFromCvSync = profile.CvSyncLockedFields.ToList()
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
            DownloadUrl = downloadUrl,
            AutoSyncProfileFromCv = profile.AutoSyncProfileFromCv,
            LockedFromCvSync = profile.CvSyncLockedFields.ToList()
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

    public async Task<CvSyncSettingsDto> GetSyncSettingsAsync(Guid userId)
    {
        var profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
        return new CvSyncSettingsDto { AutoSyncProfileFromCv = profile?.AutoSyncProfileFromCv ?? true };
    }

    public async Task<CvSyncSettingsDto> UpdateSyncSettingsAsync(Guid userId, CvSyncSettingsDto dto)
    {
        var profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
        if (profile is null)
        {
            await _candidateProfileRepository.AddAsync(new CandidateProfile
            {
                UserId = userId,
                AutoSyncProfileFromCv = dto.AutoSyncProfileFromCv
            });
        }
        else
        {
            profile.AutoSyncProfileFromCv = dto.AutoSyncProfileFromCv;
            await _candidateProfileRepository.UpdateAsync(profile);
        }

        return new CvSyncSettingsDto { AutoSyncProfileFromCv = dto.AutoSyncProfileFromCv };
    }

    private async Task<List<string>> ApplyCvProfileSyncAsync(
        CandidateProfile profile, ParseCvResult parseResult, Guid userId)
    {
        var applied = new List<string>();
        if (!profile.AutoSyncProfileFromCv)
            return applied;

        var locked = profile.CvSyncLockedFields;
        User? user = null;

        if (!locked.Contains(CvSyncableProfileFields.FullName) && !string.IsNullOrWhiteSpace(parseResult.FullName))
        {
            user = await _userRepository.GetByIdAsync(userId);
            if (user is not null)
            {
                user.FullName = parseResult.FullName.Trim();
                applied.Add(CvSyncableProfileFields.FullName);
            }
        }

        if (!locked.Contains(CvSyncableProfileFields.PhoneNumber) && !string.IsNullOrWhiteSpace(parseResult.PhoneNumber))
        {
            user ??= await _userRepository.GetByIdAsync(userId);
            if (user is not null)
                user.PhoneNumber = parseResult.PhoneNumber.Trim();
            profile.PhoneNumber = parseResult.PhoneNumber.Trim();
            applied.Add(CvSyncableProfileFields.PhoneNumber);
        }

        if (!locked.Contains(CvSyncableProfileFields.Address) && !string.IsNullOrWhiteSpace(parseResult.Address))
        {
            profile.Address = parseResult.Address.Trim();
            applied.Add(CvSyncableProfileFields.Address);
        }

        if (!locked.Contains(CvSyncableProfileFields.GithubUrl) && !string.IsNullOrWhiteSpace(parseResult.GithubUrl))
        {
            profile.GithubUrl = parseResult.GithubUrl.Trim();
            applied.Add(CvSyncableProfileFields.GithubUrl);
        }

        if (!locked.Contains(CvSyncableProfileFields.LinkedInUrl) && !string.IsNullOrWhiteSpace(parseResult.LinkedInUrl))
        {
            profile.LinkedInUrl = parseResult.LinkedInUrl.Trim();
            applied.Add(CvSyncableProfileFields.LinkedInUrl);
        }

        if (user is not null)
            await _userRepository.UpdateAsync(user);

        return applied;
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

    private static string NormalizeCvContentType(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var ct = (contentType ?? "").Trim();
        if (ext == ".pdf" && (string.IsNullOrEmpty(ct)
            || ct.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)))
            return "application/pdf";
        return string.IsNullOrEmpty(ct) ? "application/octet-stream" : ct;
    }
}
