using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApplicationLayer.DTOs.QuestionGeneration;
using ClosedXML.Excel;
using DomainLayer.Entities;

namespace ApplicationLayer.Helpers;

/// <summary>Tạo file Excel chi tiết từ job đã sinh câu hỏi.</summary>
public static partial class GeneratedQuestionsExcelExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static QuestionExportFileDto Build(QuestionGenerationJob job)
    {
        var plan = job.Plan!;
        var planSummary = PlanJsonSummaryReader.Read(job, plan);
        var questions = job.Questions.OrderBy(q => q.Order).ToList();
        var exportedAt = DateTime.UtcNow;

        using var workbook = new XLWorkbook();
        BuildInfoSheet(workbook, job, planSummary, questions.Count, exportedAt);
        BuildQuestionsSheet(workbook, questions);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new QuestionExportFileDto
        {
            Content = stream.ToArray(),
            FileName = BuildFileName(job.Id, planSummary.JobTitle, exportedAt)
        };
    }

    /// <summary>SCRUM-391: xuất Excel từ QuestionSet (History) — không phụ thuộc V1 job.</summary>
    public static QuestionExportFileDto BuildFromQuestionSet(
        QuestionSet questionSet,
        IReadOnlyList<QuestionSetQuestion> questions)
    {
        var exportedAt = DateTime.UtcNow;
        var title = questionSet.Title ?? string.Empty;

        using var workbook = new XLWorkbook();
        BuildQuestionSetInfoSheet(workbook, questionSet, questions.Count, exportedAt);
        BuildQuestionSetQuestionsSheet(workbook, questions);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new QuestionExportFileDto
        {
            Content = stream.ToArray(),
            FileName = BuildFileName(questionSet.Id, title, exportedAt)
        };
    }

    private static void BuildQuestionSetInfoSheet(
        XLWorkbook workbook,
        QuestionSet questionSet,
        int questionCount,
        DateTime exportedAt)
    {
        var sheet = workbook.Worksheets.Add("ThongTin");
        var rows = new (string Label, string? Value)[]
        {
            ("Question Set ID", questionSet.Id.ToString()),
            ("Studio Project ID", questionSet.SourceProjectId?.ToString()),
            ("Source Job ID", questionSet.SourceJobId?.ToString()),
            ("Tiêu đề", questionSet.Title),
            ("Trạng thái", questionSet.Status),
            ("Số câu hỏi", questionCount.ToString()),
            ("Ngày xuất", exportedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")),
            ("Mô tả JD", questionSet.JobDescription),
            ("Ghi chú HR", questionSet.HrNote)
        };

        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 1, 1).Value = rows[i].Label;
            sheet.Cell(i + 1, 2).Value = rows[i].Value ?? string.Empty;
            sheet.Cell(i + 1, 1).Style.Font.Bold = true;
        }

        sheet.Column(1).Width = 22;
        sheet.Column(2).Width = 80;
        sheet.Column(2).Style.Alignment.WrapText = true;
    }

    private static void BuildQuestionSetQuestionsSheet(
        XLWorkbook workbook,
        IReadOnlyList<QuestionSetQuestion> questions)
    {
        var sheet = workbook.Worksheets.Add("CauHoi");
        var headers = new[]
        {
            "STT",
            "Câu hỏi",
            "Loại câu",
            "Độ khó",
            "Kỹ năng",
            "Lĩnh vực",
            "Lý do chọn",
            "Câu trả lời mẫu",
            "Tiêu chí đánh giá",
            "Trích dẫn KB"
        };

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = sheet.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            var row = i + 2;
            sheet.Cell(row, 1).Value = q.Order;
            sheet.Cell(row, 2).Value = q.Question;
            sheet.Cell(row, 3).Value = q.QuestionType;
            sheet.Cell(row, 4).Value = q.Difficulty;
            sheet.Cell(row, 5).Value = q.Skill ?? string.Empty;
            sheet.Cell(row, 6).Value = q.FocusArea ?? string.Empty;
            sheet.Cell(row, 7).Value = q.Rationale ?? string.Empty;
            sheet.Cell(row, 8).Value = q.SampleAnswer ?? string.Empty;
            sheet.Cell(row, 9).Value = FormatEvaluationCriteria(q.EvaluationCriteriaJson);
            sheet.Cell(row, 10).Value = FormatCitations(q.CitationsJson);

            sheet.Range(row, 2, row, 10).Style.Alignment.WrapText = true;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(2, 10);
        sheet.Column(2).Width = Math.Min(sheet.Column(2).Width, 60);
        sheet.Column(8).Width = Math.Min(sheet.Column(8).Width, 60);
        sheet.Column(9).Width = Math.Min(sheet.Column(9).Width, 40);
        sheet.Column(10).Width = Math.Min(sheet.Column(10).Width, 50);
    }

    private static void BuildInfoSheet(
        XLWorkbook workbook,
        QuestionGenerationJob job,
        PlanJsonSummaryReader.PlanJsonSummary planSummary,
        int questionCount,
        DateTime exportedAt)
    {
        var sheet = workbook.Worksheets.Add("ThongTin");
        var rows = new (string Label, string? Value)[]
        {
            ("Job ID", job.Id.ToString()),
            ("Vị trí (roleTitle)", string.IsNullOrWhiteSpace(planSummary.JobTitle) ? null : planSummary.JobTitle),
            ("Trạng thái", job.Status),
            ("Số câu hỏi", questionCount.ToString()),
            ("Độ khó (plan)", planSummary.Level),
            ("Ngày xuất", exportedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")),
            ("Mô tả JD", job.JobDescription),
            ("Ghi chú HR", job.HrNote),
            ("Loại JD", job.JdInputType),
            ("Tên file JD", job.JdFileName)
        };

        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 1, 1).Value = rows[i].Label;
            sheet.Cell(i + 1, 2).Value = rows[i].Value ?? string.Empty;
            sheet.Cell(i + 1, 1).Style.Font.Bold = true;
        }

        sheet.Column(1).Width = 22;
        sheet.Column(2).Width = 80;
        sheet.Column(2).Style.Alignment.WrapText = true;
    }

    private static void BuildQuestionsSheet(XLWorkbook workbook, IReadOnlyList<GeneratedQuestion> questions)
    {
        var sheet = workbook.Worksheets.Add("CauHoi");
        var headers = new[]
        {
            "STT",
            "Câu hỏi",
            "Loại câu",
            "Độ khó",
            "Kỹ năng",
            "Lĩnh vực",
            "Lý do chọn",
            "Câu trả lời mẫu",
            "Tiêu chí đánh giá",
            "Trích dẫn KB"
        };

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = sheet.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            var row = i + 2;
            sheet.Cell(row, 1).Value = q.Order;
            sheet.Cell(row, 2).Value = q.Question;
            sheet.Cell(row, 3).Value = q.QuestionType;
            sheet.Cell(row, 4).Value = q.Difficulty;
            sheet.Cell(row, 5).Value = q.Skill ?? string.Empty;
            sheet.Cell(row, 6).Value = q.FocusArea ?? string.Empty;
            sheet.Cell(row, 7).Value = q.Rationale ?? string.Empty;
            sheet.Cell(row, 8).Value = q.SampleAnswer ?? string.Empty;
            sheet.Cell(row, 9).Value = FormatEvaluationCriteria(q.EvaluationCriteriaJson);
            sheet.Cell(row, 10).Value = FormatCitations(q.CitationsJson);

            sheet.Range(row, 2, row, 10).Style.Alignment.WrapText = true;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(2, 10);
        sheet.Column(2).Width = Math.Min(sheet.Column(2).Width, 60);
        sheet.Column(8).Width = Math.Min(sheet.Column(8).Width, 60);
        sheet.Column(9).Width = Math.Min(sheet.Column(9).Width, 40);
        sheet.Column(10).Width = Math.Min(sheet.Column(10).Width, 50);
    }

    private static string FormatEvaluationCriteria(string evaluationCriteriaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(evaluationCriteriaJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var parts = doc.RootElement.EnumerateArray()
                .Select(el => el.ValueKind == JsonValueKind.String
                    ? el.GetString()
                    : el.GetRawText())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();

            return string.Join("; ", parts);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string FormatCitations(string citationsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(citationsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var lines = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var knowledgeBase = GetJsonString(item, "knowledgeBase");
                var sourceFile = GetJsonString(item, "sourceFile");
                var chunkIndex = item.TryGetProperty("chunkIndex", out var chunkEl) && chunkEl.TryGetInt32(out var chunk)
                    ? chunk.ToString()
                    : string.Empty;
                var excerpt = GetJsonString(item, "excerpt");

                var line = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(knowledgeBase))
                    line.Append('[').Append(knowledgeBase).Append("] ");
                if (!string.IsNullOrWhiteSpace(sourceFile))
                    line.Append(sourceFile);
                if (!string.IsNullOrWhiteSpace(chunkIndex))
                    line.Append(" (chunk ").Append(chunkIndex).Append(')');
                if (!string.IsNullOrWhiteSpace(excerpt))
                    line.Append(": ").Append(excerpt);

                if (line.Length > 0)
                    lines.Add(line.ToString());
            }

            return string.Join(Environment.NewLine, lines);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
            return null;
        return prop.GetString();
    }

    private static string BuildFileName(Guid jobId, string roleTitle, DateTime exportedAt)
    {
        var slug = Slugify(roleTitle);
        if (string.IsNullOrWhiteSpace(slug))
            return $"plan-questions-{jobId}-{exportedAt:yyyyMMdd}.xlsx";

        return $"plan-questions-{slug}-{exportedAt:yyyyMMdd}.xlsx";
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant();
        normalized = InvalidFileNameCharsRegex().Replace(normalized, string.Empty);
        normalized = NonAlphanumericRegex().Replace(normalized, "-");
        normalized = MultiDashRegex().Replace(normalized, "-").Trim('-');

        if (normalized.Length > 50)
            normalized = normalized[..50].Trim('-');

        return normalized;
    }

    [GeneratedRegex(@"[<>:""/\\|?*]")]
    private static partial Regex InvalidFileNameCharsRegex();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultiDashRegex();
}
