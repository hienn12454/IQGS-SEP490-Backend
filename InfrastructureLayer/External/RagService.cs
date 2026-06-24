using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DomainLayer.Common;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.External;

public class RagService : IRagService
{
    private readonly HttpClient _httpClient;
    private readonly RagServiceSettings _settings;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public RagService(HttpClient httpClient, IOptions<RagServiceSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<RagIngestResult> IngestAsync(RagIngestRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/internal/rag/ingest", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            throw await BuildRagExceptionAsync(response, ct);

        return (await response.Content.ReadFromJsonAsync<RagIngestResult>(JsonOptions, ct))!;
    }

    public Task<RagAsyncAcceptedResult> EnqueueIngestAsync(RagIngestRequest request, CancellationToken ct = default)
        => PostAsyncAcceptedAsync("/internal/rag/ingest/async", request, ct);

    public async Task<RagDeleteResult> DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"/internal/rag/documents/{documentId}", ct);
        if (!response.IsSuccessStatusCode)
            throw await BuildRagExceptionAsync(response, ct);

        return (await response.Content.ReadFromJsonAsync<RagDeleteResult>(JsonOptions, ct))!;
    }

    public async Task<ParseJdResult> ParseJdAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("/internal/rag/parse-jd", content, ct);
        }
        catch (HttpRequestException ex)
        {
            throw CreateRagUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw CreateRagUnavailableException(ex);
        }

        if (!response.IsSuccessStatusCode)
            throw await BuildRagExceptionAsync(response, ct);

        return (await response.Content.ReadFromJsonAsync<ParseJdResult>(JsonOptions, ct))!;
    }

    public async Task<ValidateJdResult> ValidateJdAsync(ValidateJdRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("/internal/rag/validate-jd", request, JsonOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            throw CreateRagUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw CreateRagUnavailableException(ex);
        }

        if (!response.IsSuccessStatusCode)
            throw await BuildRagExceptionAsync(response, ct);

        return (await response.Content.ReadFromJsonAsync<ValidateJdResult>(JsonOptions, ct))!;
    }

    public async Task<GeneratePlanResult> GeneratePlanAsync(GeneratePlanRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("/internal/rag/generate-plan", request, JsonOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            throw CreateRagUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw CreateRagUnavailableException(ex);
        }

        if (!response.IsSuccessStatusCode)
            throw await BuildRagExceptionAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<GeneratePlanResult>(JsonOptions, ct);
        if (result is null || !result.Success)
            throw BuildPlanFailureException(result);
        return result;
    }

    public Task<RagAsyncAcceptedResult> EnqueueGeneratePlanAsync(
        Guid jobId, GeneratePlanRequest request, CancellationToken ct = default)
    {
        var body = new
        {
            jobId,
            request.OwnerId,
            request.JobDescription,
            request.NumberOfQuestions,
            request.Difficulty,
            request.QuestionTypes,
            request.Skills,
            request.HrNote
        };
        return PostAsyncAcceptedAsync("/internal/rag/generate-plan/async", body, ct);
    }

    public async Task<GenerateQuestionsFromPlanResult> GenerateQuestionsFromPlanAsync(
        GenerateQuestionsFromPlanRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("/internal/rag/generate-questions-from-plan", request, JsonOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            throw CreateRagUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw CreateRagUnavailableException(ex);
        }

        if (!response.IsSuccessStatusCode)
            throw await BuildRagExceptionAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<GenerateQuestionsFromPlanResult>(JsonOptions, ct);
        if (result is null || !result.Success)
            throw new ServerFailureException(result?.Error ?? "RAG generate-questions-from-plan thất bại.");
        return result;
    }

    public Task<RagAsyncAcceptedResult> EnqueueGenerateQuestionsFromPlanAsync(
        Guid jobId, GenerateQuestionsFromPlanRequest request, CancellationToken ct = default)
    {
        var body = new
        {
            jobId,
            request.OwnerId,
            request.JobDescription,
            request.ApprovedPlan,
            request.HrNote
        };
        return PostAsyncAcceptedAsync("/internal/rag/generate-questions-from-plan/async", body, ct);
    }

    private async Task<RagAsyncAcceptedResult> PostAsyncAcceptedAsync(
        string path, object body, CancellationToken ct)
    {
        using var dispatchCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        dispatchCts.CancelAfter(TimeSpan.FromSeconds(_settings.DispatchTimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(path, body, JsonOptions, dispatchCts.Token);
        }
        catch (HttpRequestException ex)
        {
            throw CreateRagUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw CreateRagUnavailableException(ex);
        }

        if (response.StatusCode != HttpStatusCode.Accepted)
            throw await BuildRagExceptionAsync(response, dispatchCts.Token);

        var result = await response.Content.ReadFromJsonAsync<RagAsyncAcceptedResult>(JsonOptions, dispatchCts.Token);
        if (result is null || !result.Accepted)
            throw StructuredHttpException.FromBe(
                "RAG không chấp nhận job async",
                ErrorStage.RagUnavailable,
                ["RAG không trả về accepted=true."],
                null,
                StatusCodes.Status502BadGateway);

        return result;
    }

    private static StructuredHttpException CreateRagUnavailableException(Exception ex)
    {
        return StructuredHttpException.FromBe(
            "Dịch vụ xử lý JD tạm thời không khả dụng",
            ErrorStage.RagUnavailable,
            ["Dịch vụ xử lý JD tạm thời không khả dụng. Vui lòng thử lại sau."],
            ex.Message,
            StatusCodes.Status503ServiceUnavailable);
    }

    private static RagServiceException BuildPlanFailureException(GeneratePlanResult? result)
    {
        var errors = result?.Errors ?? [];
        if (errors.Count == 0 && !string.IsNullOrWhiteSpace(result?.Error))
            errors = [result!.Error!];

        return new RagServiceException(new StructuredErrorPayload
        {
            Error = result?.Error ?? "Lỗi sinh plan",
            Detail = result?.Detail ?? result?.Error ?? "RAG generate-plan thất bại.",
            Stage = result?.Stage ?? ErrorStage.PlanGeneration,
            Source = "RAG",
            Errors = errors
        }, StatusCodes.Status502BadGateway);
    }

    private static async Task<Exception> BuildRagExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(ct);
        var ragError = TryDeserializeRagError(body);

        if (ragError is not null)
        {
            var errors = ragError.Errors ?? [];
            if (errors.Count == 0 && !string.IsNullOrWhiteSpace(ragError.Detail))
                errors = [ragError.Detail];
            if (errors.Count == 0 && !string.IsNullOrWhiteSpace(ragError.Error))
                errors = [ragError.Error];

            var beStatus = status switch
            {
                422 or 400 => StatusCodes.Status400BadRequest,
                503 or 504 => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status502BadGateway
            };

            if (beStatus == StatusCodes.Status503ServiceUnavailable)
            {
                return StructuredHttpException.FromBe(
                    ragError.Error ?? "Dịch vụ RAG tạm thời không khả dụng",
                    ErrorStage.RagUnavailable,
                    errors.Count > 0 ? errors : ["Dịch vụ RAG tạm thời không khả dụng. Vui lòng thử lại sau."],
                    ragError.Detail,
                    beStatus);
            }

            return new RagServiceException(new StructuredErrorPayload
            {
                Error = ragError.Error ?? "RAG error",
                Detail = ragError.Detail ?? errors.FirstOrDefault() ?? ragError.Error,
                Stage = ragError.Stage,
                Source = "RAG",
                Errors = errors
            }, beStatus);
        }

        return StructuredHttpException.FromBe(
            "Lỗi RAG không xác định",
            ErrorStage.RagUnavailable,
            [$"RAG HTTP {status}: {body}"],
            body,
            status >= 500 ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status502BadGateway);
    }

    private static RagErrorResponse? TryDeserializeRagError(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<RagErrorResponse>(body, JsonOptions);
        }
        catch (JsonException)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("detail", out var detailEl))
                {
                    var detail = detailEl.ValueKind == JsonValueKind.String
                        ? detailEl.GetString()
                        : detailEl.ToString();
                    return new RagErrorResponse
                    {
                        Error = detail,
                        Detail = detail,
                        Stage = "VALIDATION",
                        Errors = detail is null ? [] : [detail]
                    };
                }
            }
            catch (JsonException)
            {
                // ignore
            }
        }

        return null;
    }
}
