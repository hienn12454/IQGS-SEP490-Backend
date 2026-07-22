namespace DomainLayer.Constants;

/// <summary>
/// Tên các field profile candidate mà CV có thể tự động đồng bộ vào (feature CV-profile-sync).
/// Dùng chung giữa CandidateCvService (áp field trích xuất từ CV) và UserService (khóa field khi
/// candidate tự tay chỉnh qua PUT profile) để 2 nơi luôn khớp tên chuỗi, không lệch do gõ tay.
/// </summary>
public static class CvSyncableProfileFields
{
    public const string FullName = "FullName";
    public const string PhoneNumber = "PhoneNumber";
    public const string Address = "Address";
    public const string GithubUrl = "GithubUrl";
    public const string LinkedInUrl = "LinkedInUrl";
}
