using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.External;

/// <summary>
/// Gateway SePay (SCRUM-385):
/// - Tạo thông tin chuyển khoản + URL ảnh VietQR chuẩn SePay.
/// - Verify webhook HMAC-SHA256 theo docs SePay (timestamp + "." + rawBody).
/// - Gọi User API v2: bank-accounts / transactions (admin overview).
/// </summary>
public class SePayGateway : ISePayGateway
{
    private readonly SePaySettings _settings;
    private readonly HttpClient _http;
    private readonly ILogger<SePayGateway> _logger;

    public SePayGateway(
        IOptions<SePaySettings> settings,
        HttpClient http,
        ILogger<SePayGateway> logger)
    {
        _settings = settings.Value;
        _http = http;
        _logger = logger;
    }

    public Task<SePayCreateOrderResult> CreateUpgradeOrderAsync(
        SePayCreateOrderRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var expiresAt = DateTime.UtcNow.AddMinutes(Math.Max(_settings.OrderTtlMinutes, 5));
        var transferContent = $"IQGS {request.OrderCode}";
        var qrContent = BuildQrContent(transferContent, request.Amount);

        var result = new SePayCreateOrderResult
        {
            Provider = "SePay",
            OrderCode = request.OrderCode,
            Amount = request.Amount,
            Currency = request.Currency,
            ExpiresAt = expiresAt,
            PaymentUrl = string.IsNullOrWhiteSpace(_settings.BaseUrl)
                ? null
                : $"{_settings.BaseUrl.TrimEnd('/')}/payment/{request.OrderCode}",
            QrImageUrl = BuildVietQrImageUrl(transferContent, request.Amount),
            QrContent = qrContent,
            BankName = _settings.BankName,
            BankAccountName = _settings.BankAccountName,
            BankAccountNumber = _settings.BankAccountNumber,
            TransferContent = transferContent,
            ExternalTransactionId = null,
            RawPayloadJson = null
        };

        return Task.FromResult(result);
    }

    public bool VerifyWebhookSignature(string rawBody, string? timestamp, string? signatureHeader)
    {
        // Dev / SePay tắt xác thực HMAC → bỏ qua.
        if (!_settings.StrictSignatureValidation) return true;

        if (string.IsNullOrWhiteSpace(_settings.WebhookSecret)) return false;
        if (string.IsNullOrWhiteSpace(timestamp) || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        // SePay: expected = "sha256=" + hex(hmac_sha256(secret, timestamp + "." + rawBody))
        var signedPayload = $"{timestamp}.{rawBody ?? string.Empty}";
        var expected = "sha256=" + ComputeHmacSha256Hex(_settings.WebhookSecret, signedPayload);
        return string.Equals(expected, signatureHeader.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<SePayBankAccountDto>> ListBankAccountsAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = CreateUserApiRequest(HttpMethod.Get, "/v2/bank-accounts");
            if (req == null) return new List<SePayBankAccountDto>();

            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("SePay bank-accounts HTTP {Status}", (int)res.StatusCode);
                return new List<SePayBankAccountDto>();
            }

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var data = ExtractDataArray(doc.RootElement);
            var list = new List<SePayBankAccountDto>();
            foreach (var item in data)
            {
                list.Add(new SePayBankAccountDto
                {
                    Id = PickString(item, "id"),
                    AccountHolderName = PickString(item, "account_holder_name"),
                    AccountNumber = PickString(item, "account_number"),
                    Accumulated = PickDecimal(item, "accumulated"),
                    LastTransaction = PickStringOrNull(item, "last_transaction"),
                    Label = PickStringOrNull(item, "label"),
                    Active = PickInt(item, "active") == 1 || PickBool(item, "active"),
                    BankShortName = PickString(item, "bank_short_name", "bank_code"),
                    BankFullName = PickString(item, "bank_full_name"),
                    BankCode = PickString(item, "bank_code", "bank_short_name"),
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SePay ListBankAccountsAsync failed — degrade empty");
            return new List<SePayBankAccountDto>();
        }
    }

    public async Task<List<SePayLiveTransactionDto>> ListTransactionsAsync(
        SePayListTransactionsRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var qs = new Dictionary<string, string?>
            {
                ["per_page"] = Math.Clamp(request.PerPage, 1, 100).ToString(),
                ["transaction_date_sort"] = "desc",
            };
            if (!string.IsNullOrWhiteSpace(request.TransferType))
                qs["transfer_type"] = request.TransferType;
            if (!string.IsNullOrWhiteSpace(request.Query))
                qs["q"] = request.Query;
            if (request.From.HasValue)
                qs["transaction_date_from"] = request.From.Value.ToString("yyyy-MM-dd HH:mm:ss");
            if (request.To.HasValue)
                qs["transaction_date_to"] = request.To.Value.ToString("yyyy-MM-dd HH:mm:ss");

            var path = QueryHelpers.AddQueryString("/v2/transactions", qs!);
            using var req = CreateUserApiRequest(HttpMethod.Get, path);
            if (req == null) return new List<SePayLiveTransactionDto>();

            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("SePay transactions HTTP {Status}", (int)res.StatusCode);
                return new List<SePayLiveTransactionDto>();
            }

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var data = ExtractDataArray(doc.RootElement);
            var list = new List<SePayLiveTransactionDto>();
            foreach (var item in data)
            {
                list.Add(new SePayLiveTransactionDto
                {
                    Id = PickString(item, "id"),
                    TransactionDate = PickStringOrNull(item, "transaction_date"),
                    AccountNumber = PickStringOrNull(item, "account_number"),
                    TransferType = PickStringOrNull(item, "transfer_type"),
                    AmountIn = PickDecimal(item, "amount_in"),
                    AmountOut = PickDecimal(item, "amount_out"),
                    Accumulated = PickDecimal(item, "accumulated"),
                    TransactionContent = PickStringOrNull(item, "transaction_content"),
                    ReferenceNumber = PickStringOrNull(item, "reference_number"),
                    Code = PickStringOrNull(item, "code"),
                    BankBrandName = PickStringOrNull(item, "bank_brand_name"),
                    WebhookSuccess = PickNullableInt(item, "webhook_success"),
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SePay ListTransactionsAsync failed — degrade empty");
            return new List<SePayLiveTransactionDto>();
        }
    }

    private HttpRequestMessage? CreateUserApiRequest(HttpMethod method, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("SePay ApiKey trống — bỏ qua User API");
            return null;
        }

        var baseUrl = string.IsNullOrWhiteSpace(_settings.UserApiBaseUrl)
            ? "https://userapi.sepay.vn"
            : _settings.UserApiBaseUrl.TrimEnd('/');
        var url = relativePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : $"{baseUrl}{relativePath}";

        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return req;
    }

    private static IEnumerable<JsonElement> ExtractDataArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray();

        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
                return data.EnumerateArray();
        }

        return Array.Empty<JsonElement>();
    }

    private static string PickString(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (el.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? "";
            if (el.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.Number)
                return v.ToString();
        }
        return "";
    }

    private static string? PickStringOrNull(JsonElement el, params string[] keys)
    {
        var s = PickString(el, keys);
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static decimal PickDecimal(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out d)) return d;
        return 0;
    }

    private static int PickInt(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out i)) return i;
        return 0;
    }

    private static int? PickNullableInt(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out i)) return i;
        return null;
    }

    private static bool PickBool(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return false;
        return v.ValueKind == JsonValueKind.True;
    }

    private static string ComputeHmacSha256Hex(string key, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private string BuildQrContent(string transferContent, decimal amount)
    {
        var bank = Uri.EscapeDataString(_settings.BankName);
        var account = Uri.EscapeDataString(_settings.BankAccountNumber);
        var name = Uri.EscapeDataString(_settings.BankAccountName);
        var info = Uri.EscapeDataString(transferContent);
        return $"sepay://transfer?bank={bank}&account={account}&name={name}&amount={amount:0}&content={info}";
    }

    private string BuildVietQrImageUrl(string transferContent, decimal amount)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_settings.VietQrBaseUrl)
            ? "https://vietqr.app/img"
            : _settings.VietQrBaseUrl;

        var query = new Dictionary<string, string?>
        {
            ["acc"] = _settings.BankAccountNumber,
            ["bank"] = _settings.BankName,
            ["amount"] = ((long)Math.Round(amount, 0)).ToString(),
            ["des"] = transferContent,
            ["template"] = _settings.QrTemplate,
            ["showinfo"] = _settings.QrShowInfo ? "true" : "false",
            ["fullacc"] = _settings.QrFullAccountNumber ? "true" : "false",
            ["holder"] = _settings.BankAccountName
        };

        return QueryHelpers.AddQueryString(baseUrl, query!);
    }
}
