namespace DomainLayer.Studio;

public sealed class StudioBusinessException : Exception
{
    public StudioBusinessException(string errorCode, int statusCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public string ErrorCode { get; }
    public int StatusCode { get; }
}
