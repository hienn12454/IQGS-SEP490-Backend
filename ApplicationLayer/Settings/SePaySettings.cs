namespace ApplicationLayer.Settings;

public class SePaySettings
{
    public const string SectionName = "SePay";

    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://my.sepay.vn";
    /// <summary>SePay User API v2 (bank accounts / transactions). Không lộ ra FE.</summary>
    public string UserApiBaseUrl { get; set; } = "https://userapi.sepay.vn";
    public string VietQrBaseUrl { get; set; } = "https://vietqr.app/img";
    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public int OrderTtlMinutes { get; set; } = 30;
    public string BankName { get; set; } = "Vietcombank";
    public string BankAccountName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string QrTemplate { get; set; } = "compact";
    public bool QrShowInfo { get; set; } = true;
    public bool QrFullAccountNumber { get; set; } = false;
    public bool StrictSignatureValidation { get; set; } = true;
}
