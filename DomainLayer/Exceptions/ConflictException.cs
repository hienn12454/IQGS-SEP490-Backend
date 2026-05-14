using AutoWrapper.Wrappers;
using Microsoft.AspNetCore.Http;

namespace DomainLayer.Exceptions;

public class ConflictException : BaseHttpException
{
    private static readonly int statusCode = StatusCodes.Status409Conflict;

    public ConflictException(object customError) : base(customError, statusCode)
    {
    }

    public ConflictException(IEnumerable<ValidationError> errors) : base(errors, statusCode)
    {
    }

    public ConflictException(Exception ex) : base(ex, statusCode)
    {
    }

    public ConflictException(string message = "Conflict", string? errorCode = null, string? refLink = null) : base(message, statusCode, errorCode, refLink)
    {
    }
}
