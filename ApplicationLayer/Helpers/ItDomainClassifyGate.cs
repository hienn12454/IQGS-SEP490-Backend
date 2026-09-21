using DomainLayer.Constants;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-466: Gate LLM classify cho CV / Knowledge — fail-closed (422 / 502).
/// JD tái dùng StudioJdClassifyGate.
/// </summary>
public static class ItDomainClassifyGate
{
    public const string StageCvClassify = "CV_CLASSIFY";
    public const string StageKbClassify = "KB_CLASSIFY";
    public const string StageDocClassify = "DOC_CLASSIFY";

    public const string ErrorCvNotResume = "CV_NOT_RESUME";
    public const string ErrorCvNotItRole = "CV_NOT_IT_ROLE";
    public const string ErrorCvClassifyFailed = "CV_CLASSIFY_FAILED";
    public const string ErrorCvNoSkills = "CV_NO_IT_SKILLS";

    public const string ErrorKbNotItDocument = "KB_NOT_IT_DOCUMENT";
    public const string ErrorKbClassifyFailed = "KB_CLASSIFY_FAILED";

    public const string DefaultCvNotResume =
        "Đây không phải CV/hồ sơ ứng viên IT. " +
        "Vui lòng tải lên CV kỹ thuật/phần mềm.";

    public const string DefaultCvNotItRole =
        "CV không thuộc lĩnh vực IT/phần mềm. " +
        "Hệ thống chỉ nhận hồ sơ kỹ thuật.";

    public const string DefaultCvClassifyFailed =
        "Không phân loại được CV. Vui lòng thử lại sau.";

    public const string DefaultCvNoSkills =
        "Không tìm thấy kỹ năng IT trong CV. " +
        "Vui lòng tải lên CV kỹ thuật có tech stack rõ ràng.";

    public const string DefaultKbNotIt =
        "Tài liệu không thuộc lĩnh vực IT/phần mềm. " +
        "Hệ thống chỉ nhận tài liệu kỹ thuật phục vụ phỏng vấn IT.";

    public const string DefaultKbClassifyFailed =
        "Không phân loại được tài liệu. Vui lòng thử lại sau.";

    /// <summary>Chuẩn hóa documentType từ LLM (đồng bộ StudioJdClassifyGate).</summary>
    public static string? NormalizeDocumentType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var key = raw.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return key switch
        {
            "job_description" or "jobdescription" or "jd" or "job_posting" or "job_post" => "job_description",
            "resume" or "cv" or "curriculum_vitae" => "resume",
            "article" or "blog" or "tutorial" => "article",
            "documentation" or "docs" or "readme" or "policy" or "rubric" or "roadmap" => "documentation",
            "other" => "other",
            _ => "other"
        };
    }

    /// <summary>Pass CV khi documentType=resume và isItRole=true. Fail → StructuredHttpException 422.</summary>
    public static void EnsureCvPass(string? documentType, bool? isItRole, string? rejectReason)
    {
        var type = NormalizeDocumentType(documentType);
        if (type != "resume")
        {
            throw StructuredHttpException.FromBe(
                "CV không hợp lệ",
                StageCvClassify,
                [Pick(rejectReason, DefaultCvNotResume)],
                Pick(rejectReason, DefaultCvNotResume),
                StatusCodes.Status422UnprocessableEntity);
        }

        if (isItRole != true)
        {
            throw StructuredHttpException.FromBe(
                "CV không thuộc IT",
                StageCvClassify,
                [Pick(rejectReason, DefaultCvNotItRole)],
                Pick(rejectReason, DefaultCvNotItRole),
                StatusCodes.Status422UnprocessableEntity);
        }
    }

    public static void EnsureCvHasSkills(IReadOnlyList<string>? skills)
    {
        if (skills is null || skills.Count == 0)
        {
            throw StructuredHttpException.FromBe(
                "CV không có kỹ năng IT",
                StageCvClassify,
                [DefaultCvNoSkills],
                DefaultCvNoSkills,
                StatusCodes.Status422UnprocessableEntity);
        }
    }

    /// <summary>Pass KB khi documentation|article và isItRole=true.</summary>
    public static void EnsureKbPass(string? documentType, bool? isItRole, string? rejectReason)
    {
        var type = NormalizeDocumentType(documentType);
        if (type is not ("documentation" or "article"))
        {
            throw StructuredHttpException.FromBe(
                "Tài liệu không hợp lệ",
                StageKbClassify,
                [Pick(rejectReason, DefaultKbNotIt)],
                Pick(rejectReason, DefaultKbNotIt),
                StatusCodes.Status422UnprocessableEntity);
        }

        if (isItRole != true)
        {
            throw StructuredHttpException.FromBe(
                "Tài liệu không thuộc IT",
                StageKbClassify,
                [Pick(rejectReason, DefaultKbNotIt)],
                Pick(rejectReason, DefaultKbNotIt),
                StatusCodes.Status422UnprocessableEntity);
        }
    }

    /// <summary>L1 keyword — throw 422 nếu non-IT.</summary>
    public static void EnsureItDomainL1(string text, string stage, string label = "Tài liệu")
    {
        var err = JobDescriptionValidator.ValidateItDomain(text);
        if (err is null) return;

        throw StructuredHttpException.FromBe(
            $"{label} không thuộc IT",
            stage,
            [err],
            err,
            StatusCodes.Status422UnprocessableEntity);
    }

    public static StructuredHttpException CvClassifyFailed(string? detail = null)
        => StructuredHttpException.FromBe(
            "Phân loại CV thất bại",
            StageCvClassify,
            [Pick(detail, DefaultCvClassifyFailed)],
            Pick(detail, DefaultCvClassifyFailed),
            StatusCodes.Status502BadGateway);

    public static StructuredHttpException KbClassifyFailed(string? detail = null)
        => StructuredHttpException.FromBe(
            "Phân loại tài liệu thất bại",
            StageKbClassify,
            [Pick(detail, DefaultKbClassifyFailed)],
            Pick(detail, DefaultKbClassifyFailed),
            StatusCodes.Status502BadGateway);

    /// <summary>Map lỗi RAG classify → StructuredHttpException.</summary>
    public static StructuredHttpException FromRagCvFailure(
        string? stage,
        int httpStatus,
        string? detail,
        IReadOnlyList<string>? errors,
        string? documentType = null,
        bool? isItRole = null)
    {
        var isClassify =
            string.Equals(stage, StageCvClassify, StringComparison.OrdinalIgnoreCase)
            || string.Equals(stage, StageDocClassify, StringComparison.OrdinalIgnoreCase)
            || httpStatus is StatusCodes.Status422UnprocessableEntity or StatusCodes.Status400BadRequest;

        if (!isClassify)
            return CvClassifyFailed(detail ?? errors?.FirstOrDefault());

        var type = NormalizeDocumentType(documentType);
        var message = Pick(detail, errors?.FirstOrDefault(),
            type != "resume" ? DefaultCvNotResume : DefaultCvNotItRole);
        return StructuredHttpException.FromBe(
            "CV không hợp lệ",
            StageCvClassify,
            [message],
            message,
            StatusCodes.Status422UnprocessableEntity);
    }

    private static string Pick(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return DefaultKbNotIt;
    }
}
