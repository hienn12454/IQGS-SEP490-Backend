using AutoWrapper.Wrappers;
using Microsoft.AspNetCore.Http;

namespace DomainLayer.Exceptions;

public class BadRequestException : BaseHttpException
{
    private static readonly int statusCode = StatusCodes.Status400BadRequest;

    public BadRequestException(object customError) : base(customError, statusCode)
    {
    }

    public BadRequestException(IEnumerable<ValidationError> errors) : base(errors, statusCode)
    {
    }

    public BadRequestException(Exception ex) : base(ex, statusCode)
    {
    }

    public BadRequestException(string message = "Yêu cầu không hợp lệ.", string? errorCode = null, string? refLink = null) : base(message, statusCode, errorCode, refLink)
    {
    }
}
