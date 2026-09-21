using System.Text.Json;

namespace ApplicationLayer.DTOs.Rag;

public class RagIngestRequest
{
    public Guid DocumentId { get; set; }
    public string BlobReadUrl { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? SourceTitle { get; set; }
    /// <summary>SCRUM-442: loại tài liệu (Policy/…) — ghi vào chunk metadata.</summary>
    public string? Section { get; set; }
    public string? SourceUrl { get; set; }
    public int? Year { get; set; }
}

public class RagIngestResult
{
    public Guid DocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ChunkCount { get; set; }
    public string? ErrorMessage { get; set; }
}

public class RagAsyncAcceptedResult
{
    public bool Accepted { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid? JobId { get; set; }
    public string Phase { get; set; } = string.Empty;
}

public class RagDeleteResult
{
    public Guid DocumentId { get; set; }
    public int DeletedCount { get; set; }
}

public class ValidateJdRequest
{
    public string JobDescription { get; set; } = string.Empty;
    public string? FileName { get; set; }
}

public class ValidateJdResult
{
    public bool Success { get; set; }
    public string? JobDescription { get; set; }
    public string? FileName { get; set; }
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object>? Stats { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? ExceptionType { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ParseJdResult
{
    public bool Success { get; set; }
    public string? JobDescription { get; set; }
    public string? FileName { get; set; }
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object>? Stats { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? ExceptionType { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>SCRUM-416: LLM analyze JD — null nếu RAG không chắc (không default Software Engineer).</summary>
public class AnalyzeJdRequest
{
    public string JobDescription { get; set; } = string.Empty;
    public string? FileName { get; set; }
}

public class AnalyzeJdResult
{
    public bool Success { get; set; }
    public string? Position { get; set; }
    public string? DetectedRole { get; set; }
    public string? DetectedSeniority { get; set; }
    public string? DetectedLanguage { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<string> Responsibilities { get; set; } = new();
    public string? Summary { get; set; }
    public string? JobTitle { get; set; }
    public string? ExperienceLevel { get; set; }
    /// <summary>SCRUM-432: job_description | resume | article | documentation | other</summary>
    public string? DocumentType { get; set; }
    /// <summary>SCRUM-432: true khi công việc chính thuộc IT/phần mềm.</summary>
    public bool? IsItRole { get; set; }
    /// <summary>SCRUM-432: lý do reject classify (tiếng Việt).</summary>
    public string? RejectReason { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? ExceptionType { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class RagJobProfileInput
{
    public string? JobTitle { get; set; }
    public string? ExperienceLevel { get; set; }
    public string? DetectedRole { get; set; }
    public string? DetectedLanguage { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<string> Responsibilities { get; set; } = new();
    public string? Summary { get; set; }
}

public class RecommendInterviewConfigurationRequest
{
    public Guid OwnerId { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public RagJobProfileInput JobProfile { get; set; } = new();
    public List<Guid> DocumentIds { get; set; } = new();
    public int? NumberOfQuestions { get; set; }
}

public class RecommendInterviewConfigurationResult
{
    public bool Success { get; set; }
    public JsonElement? RecommendedConfiguration { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? ExceptionType { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ParseCvResult
{
    public bool Success { get; set; }
    public List<string> Skills { get; set; } = new();
    public string? Summary { get; set; }
    public string? FileName { get; set; }

    // Thông tin cá nhân trích xuất từ CV — dùng để tự đồng bộ vào profile (feature "CV auto-apply profile").
    // Optional/nullable: phụ thuộc RAG service có hỗ trợ trích xuất hay chưa. Nếu RAG chưa trả các field này,
    // JSON deserialize để null — code gọi (CandidateCvService) coi null là "CV không có thông tin này", giữ nguyên
    // giá trị cũ trên profile, không lỗi, không mất dữ liệu.
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? GithubUrl { get; set; }
    public string? LinkedInUrl { get; set; }

    /// <summary>SCRUM-447: role gợi ý từ CV — không phải level.</summary>
    public string? SuggestedRole { get; set; }
    public double? YearsOfExperienceHint { get; set; }

    /// <summary>SCRUM-466: resume | other — classify trong parse-cv.</summary>
    public string? DocumentType { get; set; }
    /// <summary>SCRUM-466: true khi hồ sơ thuộc IT/phần mềm.</summary>
    public bool? IsItRole { get; set; }
    /// <summary>SCRUM-466: lý do reject classify.</summary>
    public string? RejectReason { get; set; }

    public List<string> Warnings { get; set; } = new();
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? ExceptionType { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class RagQuestionDistributionItemDto
{
    public string Category { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public int QuestionCount { get; set; }
}

public class RagFocusAreaItemDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public int OrderIndex { get; set; }
    public string? Description { get; set; }
    public string? SourceReason { get; set; }
}

public class GeneratePlanRequest
{
    public Guid OwnerId { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public int NumberOfQuestions { get; set; }
    public string Difficulty { get; set; } = "medium";
    public List<string> QuestionTypes { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    public string? HrNote { get; set; }
    /// <summary>SCRUM-417: intern|junior|mid|senior|lead — HR đã confirm; RAG ưu tiên giá trị này.</summary>
    public string? ExperienceLevel { get; set; }
    /// <summary>Vietnamese | English — ngôn ngữ plan/câu hỏi.</summary>
    public string? Language { get; set; }
    /// <summary>SCRUM-388: KnowledgeDocumentIds Selected → filter HR retrieve.</summary>
    public List<Guid>? DocumentIds { get; set; }
    /// <summary>coach | jd_practice — chỉ dùng endpoint Candidate RAG.</summary>
    public string? Audience { get; set; }
    public string? CvContext { get; set; }
    public string? CandidateNote { get; set; }
    public List<RagQuestionDistributionItemDto>? QuestionDistribution { get; set; }
    public List<RagFocusAreaItemDto>? FocusAreas { get; set; }
    public List<string>? QuestionStyles { get; set; }
    public List<string>? CodingTaskTypes { get; set; }
}

public class GeneratePlanResult
{
    public bool Success { get; set; }
    public object? Plan { get; set; }
    public double? ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? ExceptionType { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>SCRUM-420: Refine plan — baseline + instruction → PlanPatch delta.</summary>
public class RefinePlanRequest
{
    public Guid OwnerId { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public object BaselinePlan { get; set; } = new();
    public int NumberOfQuestions { get; set; }
    public string Difficulty { get; set; } = "medium";
    public List<string> QuestionTypes { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    public string? HrNote { get; set; }
    public string? ExperienceLevel { get; set; }
    public string? Language { get; set; }
    public List<Guid>? DocumentIds { get; set; }
    public List<RagQuestionDistributionItemDto>? QuestionDistribution { get; set; }
    public List<RagFocusAreaItemDto>? FocusAreas { get; set; }
    public List<string>? QuestionStyles { get; set; }
    public List<string>? CodingTaskTypes { get; set; }
}

public class RefinePlanResult
{
    public bool Success { get; set; }
    public object? Patch { get; set; }
    public double? ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
    public string? Stage { get; set; }
    public string? ExceptionType { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>SCRUM-426: khóa/rebind citations trên outline slots.</summary>
public class BindOutlineSourcesRequest
{
    public Guid OwnerId { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public List<object> Outline { get; set; } = new();
    public List<string>? DocumentIds { get; set; }
    public bool ForceRebind { get; set; }
}

public class BindOutlineSourcesResult
{
    public bool Success { get; set; }
    public List<object>? Outline { get; set; }
    public double? ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
}

public class GenerateQuestionsFromPlanRequest
{
    public Guid OwnerId { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public object ApprovedPlan { get; set; } = new();
    public string? HrNote { get; set; }
    /// <summary>Vietnamese | English — ngôn ngữ câu hỏi sinh ra.</summary>
    public string? Language { get; set; }
    /// <summary>coach | jd_practice — endpoint Candidate RAG.</summary>
    public string? Audience { get; set; }
    public string? CvContext { get; set; }
    public string? CandidateNote { get; set; }
    /// <summary>SCRUM-447: filter SYSTEM retrieve theo documentIds (Tech/Roadmap curated).</summary>
    public List<Guid>? DocumentIds { get; set; }
}

public class RagGeneratedQuestionDto
{
    public string Question { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Rationale { get; set; }
    public string? SampleAnswer { get; set; }
    public List<object>? Citations { get; set; }
    public int? Order { get; set; }
    public string? Skill { get; set; }
    public string? FocusArea { get; set; }
    public List<object>? EvaluationCriteria { get; set; }
    public string? CodeTemplateType { get; set; }
    public string? CodeSnippet { get; set; }
    /// <summary>SCRUM-396: gợi ý text hình ảnh/diagram cho HR (không AI gen ảnh).</summary>
    public string? ImageHint { get; set; }
    /// <summary>SCRUM-400: Text | Code — phương thức trả lời Candidate.</summary>
    public string? AnswerMethod { get; set; }
    /// <summary>SCRUM-421: Provenance waterfall JD → Admin → LLM.</summary>
    public object? SourceProvenance { get; set; }
    /// <summary>SCRUM-421: Cảnh báo thiếu tài liệu Admin (soft_llm).</summary>
    public bool MissingAdminWarning { get; set; }
}

public class GenerateQuestionsFromPlanResult
{
    public bool Success { get; set; }
    public List<RagGeneratedQuestionDto> Questions { get; set; } = new();
    public double? ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
    /// <summary>Coach: system | inferred.</summary>
    public string? KbSource { get; set; }
}

/// <summary>Request gọi RAG evaluate-answer (SCRUM-281/282).</summary>
public class EvaluateAnswerRequest
{
    public string Question { get; set; } = string.Empty;
    public List<string> EvaluationCriteria { get; set; } = new();
    public string CandidateAnswer { get; set; } = string.Empty;
    public string? SampleAnswer { get; set; }
    public string? JdContext { get; set; }
    public string? Skill { get; set; }
    public string? QuestionType { get; set; }
    /// <summary>SCRUM-447: marketplace (default) | coach — coach bắt buộc correctness/relevance/clarity.</summary>
    public string? ScoringMode { get; set; }
}

public class EvaluateAnswerResult
{
    public bool Success { get; set; }
    public double? Score { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public string? Suggestion { get; set; }
    public Dictionary<string, double>? DimensionScores { get; set; }
    public double? ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
}

/// <summary>Request gọi RAG practice-session-insight (SCRUM-305).</summary>
public class PracticeSessionInsightRequest
{
    public double? OverallScore { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredCount { get; set; }
    public string? SetTitle { get; set; }
    public List<string> SetSkills { get; set; } = new();
    public List<QuestionInsightSummaryDto> QuestionSummaries { get; set; } = new();
}

public class QuestionInsightSummaryDto
{
    public string? QuestionType { get; set; }
    public string? Skill { get; set; }
    public double? Score { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public Dictionary<string, double>? DimensionScores { get; set; }
}

public class PracticeSessionInsightResult
{
    public bool Success { get; set; }
    public string? InsightVi { get; set; }
    public string? InsightEn { get; set; }
    public List<string> SkillsToImproveVi { get; set; } = new();
    public List<string> SkillsToImproveEn { get; set; } = new();
    public double? ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
}

/// <summary>Request RAG đánh giá bộ câu hỏi so với JD — không gửi sampleAnswer.</summary>
public class EvaluateQuestionSetItemDto
{
    public string? QuestionId { get; set; }
    public int? Order { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? QuestionType { get; set; }
    public string? Difficulty { get; set; }
    public string? Skill { get; set; }
    public string? FocusArea { get; set; }
    public string? Rationale { get; set; }
}

public class EvaluateQuestionSetRequest
{
    public Guid? OwnerId { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public string? HrNote { get; set; }
    public string? SetTitle { get; set; }
    public object? Plan { get; set; }
    public List<EvaluateQuestionSetItemDto> Questions { get; set; } = new();
}

public class EvaluateQuestionSetFlagDto
{
    public string? QuestionId { get; set; }
    public int? Order { get; set; }
    public string Flag { get; set; } = string.Empty;
    public string? NoteVi { get; set; }
    public string? NoteEn { get; set; }
    public List<JdFitSourceDto> Sources { get; set; } = new();
}

public class JdFitSourceDto
{
    public int ChunkIndex { get; set; }
    public string Excerpt { get; set; } = string.Empty;
}

public class EvaluateQuestionSetActionDto
{
    public string Type { get; set; } = string.Empty;
    public string? QuestionId { get; set; }
    public string? ReasonVi { get; set; }
    public string? ReasonEn { get; set; }
}

public class EvaluateQuestionSetResult
{
    public bool Success { get; set; }
    public string? Verdict { get; set; }
    public string? SummaryVi { get; set; }
    public string? SummaryEn { get; set; }
    public List<EvaluateQuestionSetFlagDto> QuestionFlags { get; set; } = new();
    public List<string> MissingTopics { get; set; } = new();
    public List<EvaluateQuestionSetActionDto> SuggestedActions { get; set; } = new();
    public List<JdFitSourceDto> JdSources { get; set; } = new();
    public double? ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
    public string? Detail { get; set; }
}

/// <summary>GET cache / POST sau khi upsert — không gọi RAG khi GET.</summary>
public class JdFitReviewResponse
{
    public EvaluateQuestionSetResult? Review { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ContentHash { get; set; }
    public bool IsStale { get; set; }
    public bool HasJobDescription { get; set; }
}

/// <summary>SCRUM-443/444: gọi RAG /internal/rag/retrieve.</summary>
public class RagRetrieveRequest
{
    public Guid OwnerId { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public List<Guid> DocumentIds { get; set; } = new();
    public string? QueryExtra { get; set; }
    public int? TopKSystem { get; set; }
    public int? TopKHr { get; set; }
}

public class RagRetrievedChunkDto
{
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public double Score { get; set; }
    public string? FileName { get; set; }
    public string? Section { get; set; }
}

public class RagRetrieveResult
{
    public bool Success { get; set; }
    public List<RagRetrievedChunkDto> SystemChunks { get; set; } = new();
    public List<RagRetrievedChunkDto> HrChunks { get; set; } = new();
    public double? ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
}

/// <summary>SCRUM-455: gửi node curated + gap để LLM reorder/giải thích. Score/target do Backend giữ.</summary>
public class RagRoadmapRecommendRequest
{
    public string TargetRole { get; set; } = string.Empty;
    public string TargetLevel { get; set; } = string.Empty;
    public string? RoleKey { get; set; }
    /// <summary>DocumentIds SYSTEM trong folder coach-roadmap — RAG chỉ retrieve các id này.</summary>
    public List<Guid>? DocumentIds { get; set; }
    public List<RagRoadmapWeakSkillDto> WeakSkills { get; set; } = new();
    public List<RagRoadmapNodeDto> CandidateNodes { get; set; } = new();
}

public class RagRoadmapWeakSkillDto
{
    public string Skill { get; set; } = string.Empty;
    public double CurrentScore { get; set; }
    public double TargetScore { get; set; }
    public double Gap { get; set; }
}

public class RagRoadmapNodeDto
{
    public string Topic { get; set; } = string.Empty;
    public string? Subtopic { get; set; }
    public string Skill { get; set; } = string.Empty;
    public double Importance { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public List<string> NextTopics { get; set; } = new();
    public string? SourceTitle { get; set; }
    public string? SourceUrl { get; set; }
}

public class RagRoadmapRecommendResult
{
    public bool Success { get; set; }
    public List<RagRoadmapTopicPickDto> Topics { get; set; } = new();
    public string? Explanation { get; set; }
    public string? Error { get; set; }
}

public class RagRoadmapTopicPickDto
{
    public string Topic { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

/// <summary>SCRUM-457: retrieve Tech KB cho Adaptive competency blueprint.</summary>
public class RagCompetencyContextRequest
{
    public string TargetRole { get; set; } = string.Empty;
    public string TargetLevel { get; set; } = string.Empty;
    public string? RoleFamilyKey { get; set; }
    public List<string> Skills { get; set; } = new();
}

public class RagCompetencyChunkDto
{
    public string? SourceTitle { get; set; }
    public string? SourceUrl { get; set; }
    public Guid? DocumentId { get; set; }
    public string? Section { get; set; }
    public string? DocumentType { get; set; }
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class RagCompetencyContextResult
{
    public bool Success { get; set; }
    public List<RagCompetencyChunkDto> Chunks { get; set; } = new();
    public string? Error { get; set; }
}

public class RagAdaptiveBlueprintRequest
{
    public string TargetRole { get; set; } = string.Empty;
    public string TargetLevel { get; set; } = string.Empty;
    public string? RoleFamilyKey { get; set; }
    public List<string> CvSkills { get; set; } = new();
    public List<RagCompetencyChunkDto> Chunks { get; set; } = new();
}

public class RagAdaptiveCompetencyDto
{
    public string SkillKey { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public string Category { get; set; } = "ROLE_CORE";
    public double Weight { get; set; }
    public List<string> Topics { get; set; } = new();
    public List<RagAdaptiveCitationDto> Citations { get; set; } = new();
}

public class RagAdaptiveCitationDto
{
    public string? SourceTitle { get; set; }
    public string? SourceUrl { get; set; }
    public string? Section { get; set; }
    public Guid? DocumentId { get; set; }
    public string? Excerpt { get; set; }
}

public class RagAdaptiveBlueprintResult
{
    public bool Success { get; set; }
    public List<RagAdaptiveCompetencyDto> Competencies { get; set; } = new();
    public string? Error { get; set; }
}

public class RagAdaptiveRoadmapRequest
{
    public string TargetRole { get; set; } = string.Empty;
    public string TargetLevel { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public double CurrentScore { get; set; }
    public double TargetScore { get; set; }
    public double Gap { get; set; }
    public List<string> BlueprintTopics { get; set; } = new();
    public List<RagCompetencyChunkDto> Chunks { get; set; } = new();
}

public class RagAdaptiveRoadmapTopicDto
{
    public string Topic { get; set; } = string.Empty;
    public string? Subtopic { get; set; }
    public string? SourceTitle { get; set; }
    public string? SourceUrl { get; set; }
    public string? Section { get; set; }
    public Guid? DocumentId { get; set; }
}

public class RagAdaptiveRoadmapResult
{
    public bool Success { get; set; }
    public List<RagAdaptiveRoadmapTopicDto> Topics { get; set; } = new();
    public string? Explanation { get; set; }
    public string? Error { get; set; }
}
