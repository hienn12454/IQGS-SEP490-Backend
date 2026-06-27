using AutoWrapper.Wrappers;
using Microsoft.AspNetCore.Http;

namespace DomainLayer.Exceptions;

public class ServerFailureException : BaseHttpException
{
    private static readonly int statusCode = StatusCodes.Status500InternalServerError;

    public ServerFailureException(object customError) : base(customError, statusCode)
    {
    }

    public ServerFailureException(IEnumerable<ValidationError> errors) : base(errors, statusCode)
    {
    }

    public ServerFailureException(Exception ex) : base(ex, statusCode)
    {
    }

    public ServerFailureException(string message = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.", string? errorCode = null, string? refLink = null) : base(message, statusCode, errorCode, refLink)
    {
    }
}
