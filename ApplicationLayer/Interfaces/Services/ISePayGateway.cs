namespace ApplicationLayer.Interfaces.Services;

public class SePayCreateOrderRequest
{
    public string OrderCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Description { get; set; } = string.Empty;
}

public class SePayCreateOrderResult
{
    public string Provider { get; set; } = "SePay";
    public string OrderCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime ExpiresAt { get; set; }
    public string? PaymentUrl { get; set; }
    public string? QrImageUrl { get; set; }
    public string? QrContent { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? TransferContent { get; set; }
    public string? ExternalTransactionId { get; set; }
    public string? RawPayloadJson { get; set; }
}

/// <summary>TK ngân hàng từ SePay User API v2.</summary>
public class SePayBankAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Accumulated { get; set; }
    public string? LastTransaction { get; set; }
    public string? Label { get; set; }
    public bool Active { get; set; }
    public string BankShortName { get; set; } = string.Empty;
    public string BankFullName { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
}

/// <summary>Giao dịch ngân hàng từ SePay User API v2.</summary>
public class SePayLiveTransactionDto
{
    public string Id { get; set; } = string.Empty;
    public string? TransactionDate { get; set; }
    public string? AccountNumber { get; set; }
    public string? TransferType { get; set; }
    public decimal AmountIn { get; set; }
    public decimal AmountOut { get; set; }
    public decimal Accumulated { get; set; }
    public string? TransactionContent { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Code { get; set; }
    public string? BankBrandName { get; set; }
    public int? WebhookSuccess { get; set; }
}

public class SePayListTransactionsRequest
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? TransferType { get; set; } = "in";
    public string? Query { get; set; }
    public int PerPage { get; set; } = 50;
}

public interface ISePayGateway
{
    Task<SePayCreateOrderResult> CreateUpgradeOrderAsync(SePayCreateOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Verify chữ ký SePay chuẩn: HMAC-SHA256(timestamp + "." + rawBody), header X-SePay-Signature.
    /// </summary>
    bool VerifyWebhookSignature(string rawBody, string? timestamp, string? signatureHeader);

    /// <summary>GET /v2/bank-accounts — trả list rỗng nếu API lỗi (degrade).</summary>
    Task<List<SePayBankAccountDto>> ListBankAccountsAsync(CancellationToken ct = default);

    /// <summary>GET /v2/transactions — trả list rỗng nếu API lỗi (degrade).</summary>
    Task<List<SePayLiveTransactionDto>> ListTransactionsAsync(
        SePayListTransactionsRequest request,
        CancellationToken ct = default);
}
