using AutoWrapper.Wrappers;
using Microsoft.AspNetCore.Http;

namespace DomainLayer.Exceptions;

public class NotFoundException : BaseHttpException
{
    private static readonly int statusCode = StatusCodes.Status404NotFound;

    public NotFoundException(object customError) : base(customError, statusCode)
    {
    }

    public NotFoundException(IEnumerable<ValidationError> errors) : base(errors, statusCode)
    {
    }

    public NotFoundException(Exception ex) : base(ex, statusCode)
    {
    }

    public NotFoundException(string message = "Entity not found", string? errorCode = null, string? refLink = null) : base(message, statusCode, errorCode, refLink)
    {
    }
}
