using AutoWrapper.Wrappers;

namespace DomainLayer.Exceptions;

public abstract class BaseHttpException : ApiException
{
    /// <summary>HTTP status code để middleware đọc mà không cần import AutoWrapper.</summary>
    public int HttpStatusCode { get; }

    protected BaseHttpException(object customError, int statusCode = 400)
        : base(customError, statusCode) => HttpStatusCode = statusCode;

    protected BaseHttpException(IEnumerable<ValidationError> errors, int statusCode = 400)
        : base(errors, statusCode) => HttpStatusCode = statusCode;

    protected BaseHttpException(Exception ex, int statusCode = 500)
        : base(ex, statusCode) => HttpStatusCode = statusCode;

    protected BaseHttpException(string message, int statusCode = 400, string? errorCode = null, string? refLink = null)
        : base(message, statusCode, errorCode!, refLink!) => HttpStatusCode = statusCode;
}
