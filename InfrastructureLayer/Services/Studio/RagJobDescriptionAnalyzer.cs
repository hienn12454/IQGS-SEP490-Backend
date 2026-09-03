using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Helpers;
using ApplicationLayer.Studio.Interfaces;
using DomainLayer.Exceptions;
using DomainLayer.Studio;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Services.Studio;

/// <summary>
/// SCRUM-416/432: phân tích JD qua RAG LLM + classify IT job posting trước khi lưu.
/// Fail-closed: classify reject → 422; RAG down → 502.
/// </summary>
public sealed class RagJobDescriptionAnalyzer(
    IRagService ragService,
    ILogger<RagJobDescriptionAnalyzer> logger) : IJobDescriptionAnalyzer
{
    public async Task<AnalyzeJobDescriptionResponse> AnalyzeAsync(string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(content))
            throw new StudioBusinessException(
                StudioJdClassifyGate.ErrorClassifyFailed,
                StatusCodes.Status422UnprocessableEntity,
                "Nội dung JD không được rỗng.");

        AnalyzeJdResult result;
        try
        {
            result = await ragService.AnalyzeJdAsync(
                new AnalyzeJdRequest { JobDescription = content },
                cancellationToken);
        }
        catch (StudioBusinessException)
        {
            throw;
        }
        catch (RagServiceException ex)
        {
            logger.LogWarning(
                "RAG analyze-jd reject: status={Status} stage={Stage} detail={Detail}",
                ex.HttpStatusCode, ex.Payload.Stage, ex.Payload.Detail);
            throw StudioJdClassifyGate.FromRagFailure(
                ex.Payload.Stage,
                ex.HttpStatusCode,
                ex.Payload.Detail,
                ex.Payload.Errors);
        }
        catch (StructuredHttpException ex)
        {
            logger.LogWarning(
                "RAG analyze-jd structured fail: status={Status} stage={Stage}",
                ex.HttpStatusCode, ex.Payload.Stage);
            throw StudioJdClassifyGate.FromRagFailure(
                ex.Payload.Stage,
                ex.HttpStatusCode,
                ex.Payload.Detail,
                ex.Payload.Errors);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RAG analyze-jd lỗi hạ tầng — fail-closed, không lưu JD.");
            throw new StudioBusinessException(
                StudioJdClassifyGate.ErrorClassifyFailed,
                StatusCodes.Status502BadGateway,
                StudioJdClassifyGate.DefaultClassifyFailed);
        }

        if (!result.Success)
        {
            throw StudioJdClassifyGate.FromRagFailure(
                result.Stage,
                string.Equals(result.Stage, StudioJdClassifyGate.StageClassify, StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status422UnprocessableEntity
                    : StatusCodes.Status502BadGateway,
                result.Detail ?? result.Error,
                result.Errors,
                result.DocumentType,
                result.IsItRole);
        }

        // Phòng khi RAG trả success nhưng classify field lệch
        var classifyReject = StudioJdClassifyGate.TryReject(
            result.DocumentType,
            result.IsItRole,
            result.RejectReason);
        if (classifyReject is not null)
            throw classifyReject;

        var skills = result.Skills?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray() ?? [];

        return new AnalyzeJobDescriptionResponse(
            NullIfEmpty(result.DetectedRole),
            NullIfEmpty(result.DetectedSeniority),
            NullIfEmpty(result.DetectedLanguage),
            skills,
            NullIfEmpty(result.Position) ?? NullIfEmpty(result.JobTitle) ?? NullIfEmpty(result.DetectedRole),
            JobTitle: NullIfEmpty(result.JobTitle) ?? NullIfEmpty(result.Position),
            ExperienceLevel: NullIfEmpty(result.ExperienceLevel) ?? StudioJdSeniority.ToRagExperienceLevel(result.DetectedSeniority),
            Responsibilities: result.Responsibilities?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Take(10)
                .ToArray() ?? [],
            Summary: NullIfEmpty(result.Summary));
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
