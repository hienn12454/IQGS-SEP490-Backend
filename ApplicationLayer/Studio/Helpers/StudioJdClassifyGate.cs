using Microsoft.AspNetCore.Http;
using DomainLayer.Studio;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// SCRUM-432: Gate LLM classify — chỉ pass khi documentType=job_description và isItRole=true.
/// </summary>
public static class StudioJdClassifyGate
{
    public const string StageClassify = "JD_CLASSIFY";
    public const string ErrorNotJobPosting = "JD_NOT_JOB_POSTING";
    public const string ErrorNotItRole = "JD_NOT_IT_ROLE";
    public const string ErrorClassifyFailed = "JD_CLASSIFY_FAILED";

    public const string DefaultNotJobPosting =
        "Đây không phải tin tuyển dụng (Job Description). " +
        "Vui lòng dán/upload JD đang tuyển vị trí IT/phần mềm.";

    public const string DefaultNotItRole =
        "Vị trí không thuộc lĩnh vực IT/phần mềm. " +
        "Hệ thống chỉ nhận tin tuyển dụng kỹ thuật.";

    public const string DefaultClassifyFailed =
        "Không phân loại được JD. Vui lòng thử lại sau.";

    /// <summary>Chuẩn hóa documentType từ LLM (lowercase, underscore).</summary>
    public static string? NormalizeDocumentType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var key = raw.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return key switch
        {
            "job_description" or "jobdescription" or "jd" or "job_posting" or "job_post" => "job_description",
            "resume" or "cv" or "curriculum_vitae" => "resume",
            "article" or "blog" or "tutorial" => "article",
            "documentation" or "docs" or "readme" => "documentation",
            "other" => "other",
            _ => "other"
        };
    }

    /// <summary>
    /// Kiểm tra classify. Pass → null. Fail → StudioBusinessException 422.
    /// </summary>
    public static StudioBusinessException? TryReject(
        string? documentType,
        bool? isItRole,
        string? rejectReason)
    {
        var type = NormalizeDocumentType(documentType);
        if (type != "job_description")
        {
            return new StudioBusinessException(
                ErrorNotJobPosting,
                StatusCodes.Status422UnprocessableEntity,
                PickMessage(rejectReason, DefaultNotJobPosting));
        }

        if (isItRole != true)
        {
            return new StudioBusinessException(
                ErrorNotItRole,
                StatusCodes.Status422UnprocessableEntity,
                PickMessage(rejectReason, DefaultNotItRole));
        }

        return null;
    }

    /// <summary>Map lỗi RAG (stage/status) → StudioBusinessException.</summary>
    public static StudioBusinessException FromRagFailure(
        string? stage,
        int httpStatus,
        string? detail,
        IReadOnlyList<string>? errors,
        string? documentType = null,
        bool? isItRole = null)
    {
        var isClassify =
            string.Equals(stage, StageClassify, StringComparison.OrdinalIgnoreCase)
            || httpStatus is StatusCodes.Status422UnprocessableEntity or StatusCodes.Status400BadRequest;

        if (!isClassify)
        {
            return new StudioBusinessException(
                ErrorClassifyFailed,
                StatusCodes.Status502BadGateway,
                PickMessage(detail, errors?.FirstOrDefault(), DefaultClassifyFailed));
        }

        var type = NormalizeDocumentType(documentType);
        var message = PickMessage(
            detail,
            errors?.FirstOrDefault(),
            FallbackClassifyMessage(type, isItRole));
        var code = ResolveErrorCode(type, isItRole, message);
        return new StudioBusinessException(code, StatusCodes.Status422UnprocessableEntity, message);
    }

    private static string FallbackClassifyMessage(string? documentType, bool? isItRole)
    {
        if (documentType is not null && documentType != "job_description")
            return DefaultNotJobPosting;
        if (isItRole == false)
            return DefaultNotItRole;
        return DefaultNotJobPosting;
    }

    private static string ResolveErrorCode(string? documentType, bool? isItRole, string message)
    {
        if (documentType is not null && documentType != "job_description")
            return ErrorNotJobPosting;
        if (isItRole == false)
            return ErrorNotItRole;
        if (message.Contains("không phải tin tuyển", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not a job", StringComparison.OrdinalIgnoreCase))
            return ErrorNotJobPosting;
        if (message.Contains("không thuộc", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IT/phần mềm", StringComparison.OrdinalIgnoreCase))
            return ErrorNotItRole;
        return ErrorNotJobPosting;
    }

    private static string PickMessage(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return DefaultNotJobPosting;
    }
}
