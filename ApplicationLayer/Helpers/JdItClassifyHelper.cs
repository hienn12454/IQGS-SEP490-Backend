using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Studio.Helpers;
using DomainLayer.Exceptions;
using DomainLayer.Studio;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-466: L2 classify JD (analyze-jd) cho QuestionSet / Personal set —
/// tái dùng StudioJdClassifyGate sau JobDescriptionValidator L1.
/// </summary>
public static class JdItClassifyHelper
{
    public static async Task EnsureItJobPostingAsync(
        IRagService rag,
        string jobDescription,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        AnalyzeJdResult result;
        try
        {
            result = await rag.AnalyzeJdAsync(
                new AnalyzeJdRequest { JobDescription = jobDescription },
                ct);
        }
        catch (StudioBusinessException)
        {
            throw;
        }
        catch (RagServiceException ex)
        {
            logger?.LogWarning(
                "analyze-jd reject: status={Status} stage={Stage}",
                ex.HttpStatusCode, ex.Payload.Stage);
            throw MapStudioToStructured(StudioJdClassifyGate.FromRagFailure(
                ex.Payload.Stage,
                ex.HttpStatusCode,
                ex.Payload.Detail,
                ex.Payload.Errors));
        }
        catch (StructuredHttpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "analyze-jd infra fail — fail-closed");
            throw StructuredHttpException.FromBe(
                "Không phân loại được JD",
                StudioJdClassifyGate.StageClassify,
                [StudioJdClassifyGate.DefaultClassifyFailed],
                StudioJdClassifyGate.DefaultClassifyFailed,
                StatusCodes.Status502BadGateway);
        }

        if (!result.Success)
        {
            throw MapStudioToStructured(StudioJdClassifyGate.FromRagFailure(
                result.Stage,
                string.Equals(result.Stage, StudioJdClassifyGate.StageClassify, StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status422UnprocessableEntity
                    : StatusCodes.Status502BadGateway,
                result.Detail ?? result.Error,
                result.Errors,
                result.DocumentType,
                result.IsItRole));
        }

        var reject = StudioJdClassifyGate.TryReject(
            result.DocumentType,
            result.IsItRole,
            result.RejectReason);
        if (reject is not null)
            throw MapStudioToStructured(reject);
    }

    private static StructuredHttpException MapStudioToStructured(StudioBusinessException ex)
        => StructuredHttpException.FromBe(
            ex.Message,
            StudioJdClassifyGate.StageClassify,
            [ex.Message],
            ex.Message,
            ex.StatusCode);
}
