using System.Text;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// HrNote cho Studio generate plan — yêu cầu RAG trả outline/distribution đủ để FE hiển thị card chi tiết.
/// Marker STUDIO_UI_PLAN: RAG plan_generation_service nhận diện và bổ sung hướng dẫn UI.
/// </summary>
public static class StudioRagPlanHrNoteBuilder
{
    private const int MaxLength = 2000;

    public static string BuildInitial(
        Guid projectId,
        int selectedDocs,
        int totalQuestions,
        int interviewMinutes,
        string? language = null)
    {
        var lang = StudioOutputLanguage.Normalize(language);
        var isEn = lang == StudioOutputLanguage.English;
        var natural = isEn ? "English" : "Vietnamese (Tiếng Việt)";

        var sb = new StringBuilder();
        sb.AppendLine("STUDIO_UI_PLAN=1");
        sb.AppendLine("STRICT_CORPUS=1");
        sb.AppendLine(StudioOutputLanguage.RagInstruction(lang));
        sb.AppendLine($"Studio project {projectId}; selectedDocs={selectedDocs}; targetMinutes={interviewMinutes}.");
        sb.AppendLine("YÊU CẦU HIỂN THỊ STUDIO (BẮT BUỘC):");
        sb.AppendLine(
            $"- total_questions={totalQuestions}; difficulty_distribution (easy/medium/hard) cộng đúng {totalQuestions}, không dồn hết 1 mức.");
        sb.AppendLine(
            $"- question_type_distribution: đủ các loại trong settings (thường technical/system_design/problem_solving/behavioral) — mỗi loại count>0 nếu được chọn, reason bằng {natural}.");
        sb.AppendLine(
            $"- recommended_question_outline: đúng số câu; mỗi item có type/difficulty/skill/focus_area/goal rõ (goal bằng {natural}).");
        sb.AppendLine(
            $"- coverage: 1–6 skill CHỈ từ JD + Selected docs (không bịa; không ép ≥4 nếu corpus hẹp); mỗi skill có question_count; summary 2–3 câu bằng {natural}.");
        sb.AppendLine(
            "- Mỗi coverage PHẢI có source_files = tên file RAG thật từ retrieve (để UI hiện focus từ tài liệu nào).");
        sb.AppendLine(
            "- Section UI mong đợi map từ type: Technical Foundations, System Design, Problem Solving, Behavioral.");

        var text = sb.ToString().Trim();
        return text.Length <= MaxLength ? text : text[..MaxLength];
    }
}
