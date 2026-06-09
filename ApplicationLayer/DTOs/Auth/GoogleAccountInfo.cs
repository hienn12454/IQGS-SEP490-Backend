namespace ApplicationLayer.DTOs.Auth;

/// <summary>
/// Thông tin tài khoản Google trả về sau khi verify ID Token.
/// POCO không phụ thuộc Google.Apis.Auth — để service tier có thể mock.
/// </summary>
public class GoogleAccountInfo
{
    /// <summary>Google subject ID — duy nhất và không đổi theo tài khoản.</summary>
    public string Subject { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Picture { get; set; }
    public bool EmailVerified { get; set; }
}
