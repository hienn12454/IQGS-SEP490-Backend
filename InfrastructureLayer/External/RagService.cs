using System.Diagnostics;
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

    public async Task<ParseCvResult> ParseCvAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("/internal/rag/parse-cv", content, ct);
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

        return (await response.Content.ReadFromJsonAsync<ParseCvResult>(JsonOptions, ct))!;
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

    public async Task<GenerateQuestionsFromPlanResult> GenerateQuestionsAsync(
        GeneratePlanRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("/internal/rag/generate-questions", request, JsonOptions, ct);
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
            throw new ServerFailureException(result?.Error ?? "RAG generate-questions thất bại.");
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
            request.HrNote,
            request.Language,
            DocumentIds = request.DocumentIds
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

    public async Task<GeneratePlanResult> GenerateCandidatePlanAsync(
        GeneratePlanRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "/internal/rag/candidate/generate-plan", request, JsonOptions, ct);
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

    public async Task<GenerateQuestionsFromPlanResult> GenerateCandidateQuestionsFromPlanAsync(
        GenerateQuestionsFromPlanRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "/internal/rag/candidate/generate-questions-from-plan", request, JsonOptions, ct);
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
            throw new ServerFailureException(result?.Error ?? "RAG candidate generate-questions-from-plan thất bại.");
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
            request.HrNote,
            request.Language
        };
        return PostAsyncAcceptedAsync("/internal/rag/generate-questions-from-plan/async", body, ct);
    }

    public async Task<QuestionAssistResult> AskQuestionAssistAsync(
        QuestionAssistRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("/internal/rag/question-assist", request, JsonOptions, ct);
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

        var result = await response.Content.ReadFromJsonAsync<QuestionAssistResult>(JsonOptions, ct);
        return result ?? new QuestionAssistResult
        {
            Success = false,
            Error = "RAG question-assist trả về response rỗng."
        };
    }

    /// <summary>Gọi RAG chấm điểm câu trả lời Candidate (SCRUM-281). Timeout/unavailable → throw để BE bắt và lưu Failed.</summary>
    public async Task<EvaluateAnswerResult> EvaluateAnswerAsync(
        EvaluateAnswerRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("/internal/rag/evaluate-answer", request, JsonOptions, ct);
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

        var result = await response.Content.ReadFromJsonAsync<EvaluateAnswerResult>(JsonOptions, ct);
        return result ?? new EvaluateAnswerResult
        {
            Success = false,
            Error = "RAG evaluate-answer trả về response rỗng."
        };
    }

    public async Task<EvaluateQuestionSetResult> EvaluateQuestionSetAsync(
        EvaluateQuestionSetRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("/internal/rag/evaluate-question-set", request, JsonOptions, ct);
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

        var result = await response.Content.ReadFromJsonAsync<EvaluateQuestionSetResult>(JsonOptions, ct);
        return result ?? new EvaluateQuestionSetResult
        {
            Success = false,
            Error = "RAG evaluate-question-set trả về response rỗng."
        };
    }

    /// <summary>Gọi RAG sinh AI Insight tổng quan phiên practice (SCRUM-305).</summary>
    public async Task<PracticeSessionInsightResult> GeneratePracticeSessionInsightAsync(
        PracticeSessionInsightRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "/internal/rag/practice-session-insight", request, JsonOptions, ct);
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

        var result = await response.Content.ReadFromJsonAsync<PracticeSessionInsightResult>(JsonOptions, ct);
        return result ?? new PracticeSessionInsightResult
        {
            Success = false,
            Error = "RAG practice-session-insight trả về response rỗng."
        };
    }

    public async Task<RagHealthStatusDto> GetHealthStatusAsync(CancellationToken ct = default)
    {
        var serviceUrl = _settings.BaseUrl.TrimEnd('/');
        var checkedAt = DateTimeOffset.UtcNow;
        var timeoutSeconds = _settings.HealthCheckTimeoutSeconds;

        using var healthCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        healthCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var stopwatch = Stopwatch.StartNew();
        RagHealthRawResponse? raw = null;
        var connectionFailed = false;
        string? connectionFailMessage = null;

        try
        {
            var response = await _httpClient.GetAsync("/health", healthCts.Token);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                connectionFailed = true;
                connectionFailMessage = $"Dịch vụ RAG phản hồi lỗi (HTTP {(int)response.StatusCode})";
            }
            else
            {
                raw = await response.Content.ReadFromJsonAsync<RagHealthRawResponse>(JsonOptions, healthCts.Token);
                if (raw is null)
                {
                    connectionFailed = true;
                    connectionFailMessage = "Dịch vụ RAG phản hồi nhưng không đọc được dữ liệu trạng thái";
                }
            }
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();
            connectionFailed = true;
            connectionFailMessage = $"Không kết nối được — hết thời gian chờ sau {timeoutSeconds} giây";
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            connectionFailed = true;
            connectionFailMessage = $"Không kết nối được — hết thời gian chờ sau {timeoutSeconds} giây";
        }

        var responseTimeMs = connectionFailed ? (long?)null : stopwatch.ElapsedMilliseconds;
        return BuildHealthStatus(
            serviceUrl, checkedAt, raw, responseTimeMs, connectionFailed, connectionFailMessage, timeoutSeconds);
    }

    private const string CheckConnection = "Kết nối tới RAG";
    private const string CheckConfig = "Cấu hình RAG";
    private const string CheckDatabase = "Cơ sở dữ liệu vector";

    private sealed class RagHealthRawResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string Config { get; set; } = string.Empty;
    }

    private static RagHealthStatusDto BuildHealthStatus(
        string serviceUrl,
        DateTimeOffset checkedAt,
        RagHealthRawResponse? raw,
        long? responseTimeMs,
        bool connectionFailed,
        string? connectionFailMessage,
        int timeoutSeconds)
    {
        List<RagHealthCheckItemDto> checks;

        if (connectionFailed)
        {
            checks =
            [
                new RagHealthCheckItemDto
                {
                    Name = CheckConnection,
                    Status = "fail",
                    Message = connectionFailMessage
                        ?? $"Không kết nối được — hết thời gian chờ sau {timeoutSeconds} giây"
                },
                new RagHealthCheckItemDto
                {
                    Name = CheckConfig,
                    Status = "warn",
                    Message = "Không kiểm tra được vì không kết nối tới RAG"
                },
                new RagHealthCheckItemDto
                {
                    Name = CheckDatabase,
                    Status = "warn",
                    Message = "Không kiểm tra được vì không kết nối tới RAG"
                }
            ];
        }
        else
        {
            var configOk = string.Equals(raw!.Config, "valid", StringComparison.OrdinalIgnoreCase);
            var databaseOk = string.Equals(raw.Database, "up", StringComparison.OrdinalIgnoreCase);

            checks =
            [
                new RagHealthCheckItemDto
                {
                    Name = CheckConnection,
                    Status = "pass",
                    Message = $"Dịch vụ RAG phản hồi trong {responseTimeMs}ms"
                },
                new RagHealthCheckItemDto
                {
                    Name = CheckConfig,
                    Status = configOk ? "pass" : "fail",
                    Message = configOk
                        ? "Cấu hình hợp lệ (API key, database URL, embedding dimension)"
                        : "Cấu hình thiếu hoặc không hợp lệ (API key, database URL, embedding dimension)"
                },
                new RagHealthCheckItemDto
                {
                    Name = CheckDatabase,
                    Status = databaseOk ? "pass" : "fail",
                    Message = databaseOk
                        ? "PostgreSQL pgvector hoạt động bình thường"
                        : "PostgreSQL pgvector không phản hồi"
                }
            ];
        }

        var isHealthy = checks.All(c => c.Status == "pass");
        var (summary, wrapperMessage) = BuildHealthMessages(checks, isHealthy);

        return new RagHealthStatusDto
        {
            IsHealthy = isHealthy,
            Summary = summary,
            WrapperMessage = wrapperMessage,
            Checks = checks,
            ServiceUrl = serviceUrl,
            ResponseTimeMs = responseTimeMs,
            CheckedAt = checkedAt,
            Technical = connectionFailed || raw is null
                ? null
                : new RagHealthTechnicalDto
                {
                    RagStatus = raw.Status,
                    Database = raw.Database,
                    Config = raw.Config
                }
        };
    }

    private static (string Summary, string WrapperMessage) BuildHealthMessages(
        List<RagHealthCheckItemDto> checks, bool isHealthy)
    {
        if (isHealthy)
        {
            return (
                "Tất cả hạng mục kiểm tra đều ổn. Có thể ingest tài liệu và sinh câu hỏi.",
                "Dịch vụ RAG đang hoạt động bình thường");
        }

        var connectionFailed = checks.Any(c =>
            c.Name == CheckConnection && c.Status == "fail");

        if (connectionFailed)
        {
            return (
                "Backend không nhận được phản hồi từ RAG trong thời gian cho phép. Kiểm tra RAG có đang chạy và BaseUrl trong appsettings có đúng không.",
                "Không thể kết nối tới dịch vụ RAG");
        }

        var configFailed = checks.Any(c =>
            c.Name == CheckConfig && c.Status == "fail");
        var databaseFailed = checks.Any(c =>
            c.Name == CheckDatabase && c.Status == "fail");

        if (configFailed && !databaseFailed)
        {
            return (
                "RAG thiếu hoặc sai cấu hình (API key, database URL, embedding dimension...). Liên hệ người vận hành RAG.",
                "Dịch vụ RAG đang gặp sự cố");
        }

        if (databaseFailed)
        {
            return (
                "RAG phản hồi nhưng không kết nối được cơ sở dữ liệu vector. Ingest và sinh câu hỏi có thể thất bại.",
                "Dịch vụ RAG đang gặp sự cố");
        }

        return (
            "Một hoặc nhiều hạng mục kiểm tra chưa đạt. Xem chi tiết trong danh sách checks.",
            "Dịch vụ RAG đang gặp sự cố");
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
                Error = ragError.Error ?? "Lỗi từ dịch vụ RAG",
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
