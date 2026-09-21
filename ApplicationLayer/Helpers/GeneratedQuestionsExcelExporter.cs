using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApplicationLayer.DTOs.QuestionGeneration;
using ClosedXML.Excel;
using DomainLayer.Entities;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Tạo file Excel chi tiết từ job / QuestionSet.
/// SCRUM-473: History export — đủ field HR-facing, lý do/rubric liệt kê đầy đủ, template kẻ ô + màu IQGS.
/// </summary>
public static partial class GeneratedQuestionsExcelExporter
{
    private const int ExcelCellMaxChars = 32_767;
    private const string TruncationNote = "\n(đã cắt do giới hạn Excel)";

    // Palette khớp portal #6c47ff
    private static readonly XLColor Brand = XLColor.FromHtml("#6C47FF");
    private static readonly XLColor BrandDark = XLColor.FromHtml("#4C2EC9");
    private static readonly XLColor LabelBg = XLColor.FromHtml("#EEF0FF");
    private static readonly XLColor ZebraBg = XLColor.FromHtml("#F7F6FF");
    private static readonly XLColor Grid = XLColor.FromHtml("#D0CCE8");
    private static readonly XLColor StatusPublishedBg = XLColor.FromHtml("#DCFCE7");
    private static readonly XLColor StatusPublishedFg = XLColor.FromHtml("#166534");
    private static readonly XLColor StatusDraftBg = XLColor.FromHtml("#F3F4F6");
    private static readonly XLColor StatusDraftFg = XLColor.FromHtml("#4B5563");
    private static readonly XLColor EasyBg = XLColor.FromHtml("#DCFCE7");
    private static readonly XLColor EasyFg = XLColor.FromHtml("#166534");
    private static readonly XLColor MediumBg = XLColor.FromHtml("#FEF9C3");
    private static readonly XLColor MediumFg = XLColor.FromHtml("#854D0E");
    private static readonly XLColor HardBg = XLColor.FromHtml("#FEE2E2");
    private static readonly XLColor HardFg = XLColor.FromHtml("#991B1B");

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

    /// <summary>SCRUM-391 / SCRUM-473: xuất Excel từ QuestionSet (History) — không phụ thuộc V1 job.</summary>
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
        sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = "Calibri";
        sheet.Style.Font.FontSize = 11;

        var plan = PlanJsonSummaryReader.ReadFromJson(questionSet.PlanJson);
        var title = string.IsNullOrWhiteSpace(questionSet.Title) ? "(Không có tiêu đề)" : questionSet.Title!;

        // Banner
        sheet.Range(1, 1, 1, 2).Merge();
        var banner = sheet.Cell(1, 1);
        banner.Value = "IQGS — Bộ câu hỏi phỏng vấn";
        banner.Style.Font.Bold = true;
        banner.Style.Font.FontSize = 16;
        banner.Style.Font.FontColor = XLColor.White;
        banner.Style.Fill.BackgroundColor = Brand;
        banner.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        banner.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        banner.Style.Alignment.Indent = 1;
        sheet.Row(1).Height = 28;

        sheet.Range(2, 1, 2, 2).Merge();
        var subtitle = sheet.Cell(2, 1);
        subtitle.Value = title;
        subtitle.Style.Font.FontSize = 12;
        subtitle.Style.Font.FontColor = XLColor.White;
        subtitle.Style.Fill.BackgroundColor = BrandDark;
        subtitle.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        subtitle.Style.Alignment.Indent = 1;
        sheet.Row(2).Height = 22;

        var row = 3;
        void Section(string name)
        {
            sheet.Range(row, 1, row, 2).Merge();
            var cell = sheet.Cell(row, 1);
            cell.Value = name;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = BrandDark;
            cell.Style.Fill.BackgroundColor = LabelBg;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.Indent = 1;
            ApplyThinBorder(sheet.Range(row, 1, row, 2));
            sheet.Row(row).Height = 20;
            row++;
        }

        void Field(string label, string? value, bool tall = false, bool statusStyle = false)
        {
            var labelCell = sheet.Cell(row, 1);
            var valueCell = sheet.Cell(row, 2);
            labelCell.Value = label;
            labelCell.Style.Font.Bold = true;
            labelCell.Style.Fill.BackgroundColor = LabelBg;
            labelCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            labelCell.Style.Alignment.WrapText = true;

            var text = TruncateForExcel(value ?? string.Empty);
            valueCell.Value = text;
            valueCell.Style.Alignment.WrapText = true;
            valueCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

            if (statusStyle)
                ApplyStatusStyle(valueCell, text);

            ApplyThinBorder(sheet.Range(row, 1, row, 2));
            if (tall)
                sheet.Row(row).Height = Math.Min(120, Math.Max(36, EstimateRowHeight(text, 90)));
            else
                sheet.Row(row).Height = Math.Max(18, EstimateRowHeight(text, 90));
            row++;
        }

        Section("Định danh");
        Field("Question Set ID", questionSet.Id.ToString());
        Field("Loại bộ (Kind)", questionSet.Kind);
        Field("Trạng thái", questionSet.Status, statusStyle: true);
        Field("Tiêu đề", questionSet.Title);
        Field("Số câu hỏi", questionCount.ToString());

        Section("Nguồn");
        Field("Studio Project ID", questionSet.SourceProjectId?.ToString());
        Field("Source Plan ID", questionSet.SourcePlanId?.ToString());
        Field("Source Run ID", questionSet.SourceRunId?.ToString());
        Field("Source Job ID (legacy)", questionSet.SourceJobId?.ToString());

        Section("Job Description");
        Field("Nguồn JD", questionSet.JdSourceType);
        Field("Tên file JD", questionSet.JdOriginalFileName);
        Field("Mô tả JD (nội bộ)", questionSet.JobDescription, tall: true);
        Field("JD công khai (candidate)", questionSet.PublicJobDescription, tall: true);

        Section("Tin tuyển");
        Field("Địa điểm", questionSet.JobLocation);
        Field("Hình thức làm việc", questionSet.WorkplaceType);
        Field("Lương tối thiểu (VND)", questionSet.SalaryMin?.ToString("N0"));
        Field("Lương tối đa (VND)", questionSet.SalaryMax?.ToString("N0"));
        Field("Lương thỏa thuận", BoolVi(questionSet.SalaryNegotiable));
        Field("Chuyên môn", questionSet.JobExpertise);
        Field("Lĩnh vực / Domain", questionSet.JobDomain);

        Section("Cấu hình practice");
        Field("Giới hạn thời gian (phút)", questionSet.TimeLimitMinutes?.ToString() ?? "Không giới hạn");
        Field("Tự động recommendation", BoolVi(questionSet.AutoRecommendEnabled));
        Field("Ngưỡng điểm recommendation", questionSet.RecommendationMinScore.ToString("0.##"));
        Field("Bộ tuyển dụng (Hiring)", BoolVi(questionSet.IsHiringAssessment));
        Field("Anti-cheat (HR)", BoolVi(questionSet.HrAntiCheatEnabled));

        Section("Interview Plan");
        Field("Role / Vị trí", string.IsNullOrWhiteSpace(plan.Role) ? plan.JobTitle : plan.Role);
        Field("Level", plan.Level);
        Field("Kinh nghiệm", plan.ExperienceLevel);
        Field("Số câu (plan)", plan.Question > 0 ? plan.Question.ToString() : null);
        Field("Skills (plan)", plan.Skills.Count > 0 ? string.Join(", ", plan.Skills) : null);

        Section("Ghi chú & thời gian");
        Field("Ghi chú HR", questionSet.HrNote, tall: true);
        Field("Generated At (UTC)", FormatUtc(questionSet.GeneratedAt));
        Field("Created At (UTC)", FormatUtc(questionSet.CreatedAt));
        Field("Updated At (UTC)", FormatUtc(questionSet.UpdatedAt));
        Field("Published At (UTC)", FormatUtc(questionSet.PublishedAt));
        Field("Ngày xuất (UTC)", FormatUtc(exportedAt));

        var lastDataRow = row - 1;
        sheet.Column(1).Width = 28;
        sheet.Column(2).Width = 90;
        sheet.PageSetup.PrintAreas.Add($"A1:B{lastDataRow}");
        sheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);
    }

    private static void BuildQuestionSetQuestionsSheet(
        XLWorkbook workbook,
        IReadOnlyList<QuestionSetQuestion> questions)
    {
        var sheet = workbook.Worksheets.Add("CauHoi");
        sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = "Calibri";
        sheet.Style.Font.FontSize = 11;

        var headers = new[]
        {
            "STT",
            "Question Id",
            "Câu hỏi",
            "Loại câu",
            "Độ khó",
            "Kỹ năng",
            "Lĩnh vực",
            "Cách trả lời",
            "Lý do chọn",
            "Template code",
            "Snippet",
            "Image hint",
            "Câu trả lời mẫu",
            "Tiêu chí đánh giá",
            "Trích dẫn KB",
            "Có ảnh đính kèm"
        };

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = sheet.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = Brand;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
        }

        sheet.Row(1).Height = 32;
        sheet.SheetView.FreezeRows(1);

        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            var row = i + 2;
            var meta = QuestionRationaleMetaParser.Parse(q.Rationale);
            var zebra = i % 2 == 1;

            var values = new object?[]
            {
                q.Order,
                q.Id.ToString(),
                TruncateForExcel(q.Question),
                q.QuestionType,
                q.Difficulty,
                q.Skill ?? string.Empty,
                q.FocusArea ?? string.Empty,
                string.IsNullOrWhiteSpace(q.AnswerMethod) ? "Text" : q.AnswerMethod,
                TruncateForExcel(FormatListedRationale(meta.CleanRationale)),
                meta.CodeTemplateType ?? string.Empty,
                TruncateForExcel(meta.CodeSnippet ?? string.Empty),
                TruncateForExcel(meta.ImageHint ?? string.Empty),
                TruncateForExcel(q.SampleAnswer ?? string.Empty),
                TruncateForExcel(FormatRubricListed(q.EvaluationCriteriaJson)),
                TruncateForExcel(FormatCitations(q.CitationsJson)),
                string.IsNullOrWhiteSpace(q.AttachedImageBlobPath) ? "Không" : "Có"
            };

            for (var col = 0; col < values.Length; col++)
            {
                var cell = sheet.Cell(row, col + 1);
                var val = values[col];
                if (val is int n)
                    cell.Value = n;
                else
                    cell.Value = val?.ToString() ?? string.Empty;

                cell.Style.Alignment.WrapText = true;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                if (zebra)
                    cell.Style.Fill.BackgroundColor = ZebraBg;
                ApplyThinBorder(cell);
            }

            sheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(row, 16).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ApplyDifficultyStyle(sheet.Cell(row, 5), q.Difficulty);

            var longText = string.Join(
                "\n",
                values[2]?.ToString(),
                values[8]?.ToString(),
                values[12]?.ToString(),
                values[13]?.ToString());
            sheet.Row(row).Height = Math.Min(160, Math.Max(22, EstimateRowHeight(longText, 50)));
        }

        var lastCol = headers.Length;
        var lastRow = Math.Max(1, questions.Count + 1);

        // Widths cố định — không AdjustToContents để tránh cắt nhìn
        sheet.Column(1).Width = 6;
        sheet.Column(2).Width = 36;
        sheet.Column(3).Width = 48;
        sheet.Column(4).Width = 14;
        sheet.Column(5).Width = 12;
        sheet.Column(6).Width = 16;
        sheet.Column(7).Width = 16;
        sheet.Column(8).Width = 12;
        sheet.Column(9).Width = 42;
        sheet.Column(10).Width = 16;
        sheet.Column(11).Width = 36;
        sheet.Column(12).Width = 24;
        sheet.Column(13).Width = 42;
        sheet.Column(14).Width = 42;
        sheet.Column(15).Width = 40;
        sheet.Column(16).Width = 14;

        if (questions.Count > 0)
        {
            sheet.Range(1, 1, lastRow, lastCol).SetAutoFilter();
            ApplyThinBorder(sheet.Range(1, 1, 1, lastCol));
        }

        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(1, 1);
        if (questions.Count > 0)
            sheet.PageSetup.PrintAreas.Add($"A1:{GetExcelColumnName(lastCol)}{lastRow}");
    }

    // ── V1 job export (giữ tương thích) ──────────────────────────────────────

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

    // ── Format helpers ───────────────────────────────────────────────────────

    /// <summary>Liệt kê rationale sạch thành 1. 2. 3. — không lẫn template/snippet.</summary>
    private static string FormatListedRationale(string? cleanRationale)
    {
        if (string.IsNullOrWhiteSpace(cleanRationale))
            return string.Empty;

        var parts = cleanRationale
            .Split(new[] { ';', '\n' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (parts.Count == 0)
            return string.Empty;
        if (parts.Count == 1)
            return parts[0];

        var sb = new StringBuilder();
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            sb.Append(i + 1).Append(". ").Append(parts[i]);
        }

        return sb.ToString();
    }

    /// <summary>RubricV1 / legacy — mỗi criterion một dòng kèm weight + anchors.</summary>
    private static string FormatRubricListed(string evaluationCriteriaJson)
    {
        if (string.IsNullOrWhiteSpace(evaluationCriteriaJson))
            return string.Empty;

        try
        {
            var doc = RubricNormalizer.NormalizeFromJson(evaluationCriteriaJson);
            if (doc.Criteria.Count == 0)
                return FormatEvaluationCriteria(evaluationCriteriaJson);

            var sb = new StringBuilder();
            for (var i = 0; i < doc.Criteria.Count; i++)
            {
                var c = doc.Criteria[i];
                if (i > 0) sb.AppendLine();
                sb.Append(i + 1).Append(". ").Append(c.Label);
                if (c.Weight > 0)
                    sb.Append(" (").Append(c.Weight).Append("%)");

                if (c.Anchors is { Count: > 0 })
                {
                    foreach (var kv in c.Anchors.OrderBy(a => a.Key))
                    {
                        if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                        sb.AppendLine();
                        sb.Append("   · ").Append(kv.Key).Append(": ").Append(kv.Value.Trim());
                    }
                }
            }

            return sb.ToString();
        }
        catch
        {
            return FormatEvaluationCriteria(evaluationCriteriaJson);
        }
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

    private static string TruncateForExcel(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Length <= ExcelCellMaxChars)
            return value;

        var keep = ExcelCellMaxChars - TruncationNote.Length;
        if (keep < 1) keep = ExcelCellMaxChars;
        return value[..keep] + TruncationNote;
    }

    private static string BoolVi(bool value) => value ? "Có" : "Không";

    private static string? FormatUtc(DateTime? dt)
        => dt.HasValue ? dt.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'") : null;

    private static string FormatUtc(DateTime dt)
        => dt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

    private static void ApplyThinBorder(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = Grid;
        range.Style.Border.InsideBorderColor = Grid;
    }

    private static void ApplyThinBorder(IXLCell cell)
    {
        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        cell.Style.Border.OutsideBorderColor = Grid;
    }

    private static void ApplyStatusStyle(IXLCell cell, string status)
    {
        var s = status.Trim().ToUpperInvariant();
        if (s is "PUBLISHED")
        {
            cell.Style.Fill.BackgroundColor = StatusPublishedBg;
            cell.Style.Font.FontColor = StatusPublishedFg;
            cell.Style.Font.Bold = true;
        }
        else if (s is "DRAFT")
        {
            cell.Style.Fill.BackgroundColor = StatusDraftBg;
            cell.Style.Font.FontColor = StatusDraftFg;
            cell.Style.Font.Bold = true;
        }
    }

    private static void ApplyDifficultyStyle(IXLCell cell, string? difficulty)
    {
        var d = (difficulty ?? string.Empty).Trim().ToLowerInvariant();
        if (d is "easy" or "dễ" or "de")
        {
            cell.Style.Fill.BackgroundColor = EasyBg;
            cell.Style.Font.FontColor = EasyFg;
            cell.Style.Font.Bold = true;
        }
        else if (d is "medium" or "trung bình" or "trung binh" or "mid")
        {
            cell.Style.Fill.BackgroundColor = MediumBg;
            cell.Style.Font.FontColor = MediumFg;
            cell.Style.Font.Bold = true;
        }
        else if (d is "hard" or "khó" or "kho")
        {
            cell.Style.Fill.BackgroundColor = HardBg;
            cell.Style.Font.FontColor = HardFg;
            cell.Style.Font.Bold = true;
        }
    }

    private static double EstimateRowHeight(string? text, double charsPerLine)
    {
        if (string.IsNullOrEmpty(text)) return 18;
        var lines = text.Split('\n').Sum(line =>
            Math.Max(1, (int)Math.Ceiling(Math.Max(1, line.Length) / Math.Max(1, charsPerLine))));
        return Math.Min(160, 14 + lines * 14);
    }

    private static string GetExcelColumnName(int columnNumber)
    {
        var dividend = columnNumber;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar(65 + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
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
