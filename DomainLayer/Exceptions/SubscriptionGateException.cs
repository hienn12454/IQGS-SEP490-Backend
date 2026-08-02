namespace DomainLayer.Exceptions;

/// <summary>Lỗi feature gate subscription — middleware map HTTP 403 + errorCode.</summary>
public sealed class SubscriptionGateException : Exception
{
    public SubscriptionGateException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = 403;
    }

    public string ErrorCode { get; }
    public int StatusCode { get; }
}
