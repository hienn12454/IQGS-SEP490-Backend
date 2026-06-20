using DomainLayer.Common;
using Microsoft.AspNetCore.Http;
namespace DomainLayer.Exceptions;

/// <summary>
/// Lỗi có cấu trúc RAG → BE → FE (stage, errors[], detail).
/// </summary>
public class StructuredHttpException : BaseHttpException
{
    public StructuredErrorPayload Payload { get; }

    public StructuredHttpException(StructuredErrorPayload payload, int statusCode = StatusCodes.Status400BadRequest)
        : base(payload.Error, statusCode)
    {
        Payload = payload;
    }

    public static StructuredHttpException FromBe(
        string error,
        string stage,
        IEnumerable<string> errors,
        string? detail = null,
        int statusCode = StatusCodes.Status400BadRequest)
    {
        var list = errors.ToList();
        return new StructuredHttpException(new StructuredErrorPayload
        {
            Error = error,
            Detail = detail ?? list.FirstOrDefault() ?? error,
            Stage = stage,
            Source = "BE",
            Errors = list
        }, statusCode);
    }

    public static StructuredHttpException FromRag(
        string error,
        string? detail,
        string? stage,
        IEnumerable<string> errors,
        int statusCode = StatusCodes.Status400BadRequest)
    {
        var list = errors.ToList();
        return new StructuredHttpException(new StructuredErrorPayload
        {
            Error = error,
            Detail = detail ?? list.FirstOrDefault() ?? error,
            Stage = stage,
            Source = "RAG",
            Errors = list
        }, statusCode);
    }
}

public class RagServiceException : StructuredHttpException
{
    public RagServiceException(StructuredErrorPayload payload, int statusCode = StatusCodes.Status400BadRequest)
        : base(new StructuredErrorPayload
        {
            Error = payload.Error,
            Detail = payload.Detail,
            Stage = payload.Stage,
            Source = "RAG",
            Errors = payload.Errors
        }, statusCode)
    {
    }
}
