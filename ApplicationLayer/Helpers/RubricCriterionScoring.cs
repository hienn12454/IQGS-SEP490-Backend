using System.Text.Json;
using System.Text.Json.Serialization;
using ApplicationLayer.DTOs.Rag;
using static ApplicationLayer.Helpers.RubricNormalizer;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Chấm điểm theo từng tiêu chí rubric của HR (practice + hiring).
/// AI chỉ cho điểm 0–100 cho MỖI tiêu chí; điểm câu hỏi do Backend tính = Σ(trọng số × điểm tiêu chí) / 100.
/// Làm vậy để điểm luôn khớp trọng số HR đã đặt — hội đồng kiểm tra lại cũng tính ra đúng con số này.
/// </summary>
public static class RubricCriterionScoring
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Điểm 1 tiêu chí, lưu kèm nhãn + trọng số tại thời điểm chấm (HR sửa rubric sau này không đổi lịch sử).</summary>
    public sealed class StoredCriterionScore
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Weight { get; set; }
        public double Score { get; set; }
    }

    /// <summary>
    /// Rubric dùng được cho chấm theo tiêu chí: có ít nhất 1 tiêu chí và trọng số hợp lệ (tổng 100, mỗi tiêu chí &gt; 0).
    /// Không đạt → quay về chấm tổng thể như cũ (không đoán trọng số).
    /// </summary>
    public static bool IsUsable(RubricDocumentV1? doc)
        => doc?.Criteria is { Count: > 0 } && !IsWeightInvalid(doc);

    /// <summary>Gán code C1, C2... theo thứ tự để AI trả điểm đúng tiêu chí (id gốc có thể trùng/khó đọc).</summary>
    public static List<RubricCriterionInputDto> BuildRagCriteria(RubricDocumentV1? doc)
    {
        if (!IsUsable(doc))
            return [];

        var result = new List<RubricCriterionInputDto>();
        for (var i = 0; i < doc!.Criteria.Count; i++)
        {
            var criterion = doc.Criteria[i];
            result.Add(new RubricCriterionInputDto
            {
                Code = $"C{i + 1}",
                Label = criterion.Label,
                Weight = criterion.Weight,
                Anchors = criterion.Anchors
                    .OrderBy(kv => int.TryParse(kv.Key, out var n) ? n : 0)
                    .ToDictionary(kv => kv.Key, kv => kv.Value)
            });
        }

        return result;
    }

    /// <summary>AI phải trả đủ mọi code, điểm 0–100. Thiếu một tiêu chí là coi như chấm hỏng (không tự đoán).</summary>
    public static bool HasAllScores(EvaluateAnswerResult? result, IReadOnlyList<RubricCriterionInputDto> criteria)
    {
        if (result is null || !result.Success || result.CriterionScores is null)
            return false;

        foreach (var criterion in criteria)
        {
            if (!result.CriterionScores.TryGetValue(criterion.Code, out var score))
                return false;
            if (double.IsNaN(score) || score < 0 || score > 100)
                return false;
        }

        return true;
    }

    /// <summary>Điểm câu hỏi = Σ(weight × điểm tiêu chí) / tổng weight (rubric hợp lệ thì tổng = 100).</summary>
    public static double ComputeWeightedScore(IReadOnlyList<StoredCriterionScore> scores)
    {
        var totalWeight = scores.Sum(s => s.Weight);
        if (totalWeight <= 0)
            return 0;

        var weightedSum = scores.Sum(s => s.Weight * s.Score);
        return weightedSum / totalWeight;
    }

    public static List<StoredCriterionScore> BuildStoredScores(
        IReadOnlyList<RubricCriterionInputDto> criteria,
        Dictionary<string, double> aiScores)
    {
        return criteria.Select(c => new StoredCriterionScore
        {
            Code = c.Code,
            Label = c.Label,
            Weight = c.Weight,
            // Làm tròn từng tiêu chí để số hiển thị cộng lại khớp với điểm câu.
            Score = Math.Round(aiScores[c.Code], 0)
        }).ToList();
    }

    public static string Serialize(IReadOnlyList<StoredCriterionScore> scores)
        => JsonSerializer.Serialize(scores, JsonOptions);

    public static List<StoredCriterionScore>? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var list = JsonSerializer.Deserialize<List<StoredCriterionScore>>(json, JsonOptions);
            return list is { Count: > 0 } ? list : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
