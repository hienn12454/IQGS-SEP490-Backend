using System.Text.Json;
using DomainLayer.Common;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Helpers;

public static class JobErrorSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(StructuredErrorPayload payload)
        => JsonSerializer.Serialize(payload, JsonOptions);

    public static string SerializeFromException(Exception ex)
    {
        if (ex is StructuredHttpException structured)
            return Serialize(structured.Payload);

        return Serialize(new StructuredErrorPayload
        {
            Error = "Lỗi xử lý job",
            Detail = ex.Message,
            Stage = ex.Data.Contains("stage") ? ex.Data["stage"]?.ToString() : null,
            Source = "BE",
            Errors = [ex.Message]
        });
    }

    public static StructuredErrorPayload? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<StructuredErrorPayload>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return new StructuredErrorPayload
            {
                Error = json,
                Detail = json,
                Source = "BE",
                Errors = [json]
            };
        }
    }
}
