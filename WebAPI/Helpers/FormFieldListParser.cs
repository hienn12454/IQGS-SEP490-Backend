using System.Text.Json;

namespace WebAPI.Helpers;

/// <summary>
/// Parse field multipart dạng chuỗi — hỗ trợ "A, B" hoặc JSON array legacy.
/// </summary>
public static class FormFieldListParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(trimmed, JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                // Client gửi JSON lỗi — fallback tách bằng dấu phẩy
            }
        }

        return trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}
