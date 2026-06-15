using AutoWrapper.Wrappers;
using Microsoft.AspNetCore.Http;

namespace DomainLayer.Exceptions;

public class ForbiddenException : BaseHttpException
{
    private static readonly int statusCode = StatusCodes.Status403Forbidden;

    public ForbiddenException(object customError) : base(customError, statusCode)
    {
    }

    public ForbiddenException(IEnumerable<ValidationError> errors) : base(errors, statusCode)
    {
    }

    public ForbiddenException(Exception ex) : base(ex, statusCode)
    {
    }

    public ForbiddenException(string message = "Forbidden", string? errorCode = null, string? refLink = null) : base(message, statusCode, errorCode, refLink)
    {
    }
}
