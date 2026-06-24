using DomainLayer.Constants;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Validate input tạo/sửa job — khớp rule RAG trước khi enqueue Hangfire.
/// </summary>
public static class QuestionGenerationInputValidator
{
    public const int MinQuestions = 1;
    public const int MaxQuestions = 50;

    private static readonly HashSet<string> AllowedDifficulties = new(StringComparer.OrdinalIgnoreCase)
    {
        "easy", "medium", "hard"
    };

    public static IReadOnlyList<string> ValidateAndNormalizeQuestionTypes(IEnumerable<string> questionTypes)
    {
        var list = questionTypes?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? [];
        if (list.Count == 0)
        {
            throw StructuredHttpException.FromBe(
                "Dữ liệu không hợp lệ",
                ErrorStage.Validation,
                ["questionTypes không được rỗng."]);
        }

        var normalized = QuestionTypeNormalizer.Normalize(list);
        var invalid = normalized.Where(t => !QuestionTypeNormalizer.IsAllowed(t)).ToList();
        if (invalid.Count > 0)
        {
            var allowed = string.Join(", ", QuestionTypeNormalizer.AllowedTypes.OrderBy(x => x));
            throw StructuredHttpException.FromBe(
                "Dữ liệu không hợp lệ",
                ErrorStage.Validation,
                invalid.Select(t =>
                    $"questionTypes không hợp lệ: '{t}'. Cho phép: {allowed}").ToList());
        }

        return normalized;
    }

    public static string ValidateAndNormalizeDifficulty(string difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            throw StructuredHttpException.FromBe(
                "Dữ liệu không hợp lệ",
                ErrorStage.Validation,
                ["difficulty không được rỗng."]);
        }

        var normalized = difficulty.Trim().ToLowerInvariant();
        if (!AllowedDifficulties.Contains(normalized))
        {
            throw StructuredHttpException.FromBe(
                "Dữ liệu không hợp lệ",
                ErrorStage.Validation,
                [$"difficulty không hợp lệ: '{difficulty}'. Cho phép: easy, medium, hard."]);
        }

        return normalized;
    }

    public static void ValidateNumberOfQuestions(int numberOfQuestions)
    {
        if (numberOfQuestions < MinQuestions || numberOfQuestions > MaxQuestions)
        {
            throw StructuredHttpException.FromBe(
                "Dữ liệu không hợp lệ",
                ErrorStage.Validation,
                [$"numberOfQuestions phải từ {MinQuestions} đến {MaxQuestions}."]);
        }
    }
}
