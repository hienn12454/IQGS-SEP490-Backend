using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.DTOs.QuestionGeneration;

namespace ApplicationLayer.Interfaces.Services;

public interface IQuestionSetService
{
    Task<SaveDraftResponseDto> SaveDraftFromJobAsync(Guid jobId, Guid ownerId);
    /// <summary>SCRUM-397: tạo DRAFT rỗng từ Question Builder (0 câu hỏi).</summary>
    Task<SaveDraftResponseDto> CreateManualDraftAsync(Guid ownerId, CreateManualDraftQuestionSetRequestDto dto);
    Task<QuestionSetDetailResponseDto> GetQuestionSetAsync(Guid questionSetId, Guid ownerId);
    Task<IReadOnlyList<QuestionSetListItemDto>> ListQuestionSetsAsync(Guid ownerId, QuestionSetListQueryDto query);
    Task<QuestionSetQuestionResponseDto> UpdateQuestionAsync(
        Guid questionSetId, Guid questionId, Guid ownerId, UpdateQuestionRequestDto dto);
    Task<QuestionSetQuestionResponseDto> AddQuestionAsync(
        Guid questionSetId, Guid ownerId, CreateQuestionRequestDto dto);
    Task DeleteQuestionAsync(Guid questionSetId, Guid questionId, Guid ownerId);
    Task<IReadOnlyList<QuestionSetQuestionResponseDto>> ReorderQuestionsAsync(
        Guid questionSetId, Guid ownerId, ReorderQuestionsRequestDto dto);
    Task<QuestionSetActionResponseDto> PublishAsync(Guid questionSetId, Guid ownerId);
    Task<QuestionSetActionResponseDto> UnpublishAsync(Guid questionSetId, Guid ownerId);
    Task<SetTimeLimitResponseDto> SetTimeLimitAsync(Guid questionSetId, Guid ownerId, SetTimeLimitRequestDto dto);
    Task<RenameQuestionSetTitleResponseDto> RenameTitleAsync(
        Guid questionSetId, Guid ownerId, RenameQuestionSetTitleRequestDto dto);
    Task<UpdateQuestionSetJobDescriptionResponseDto> SetJobDescriptionFromTextAsync(
        Guid questionSetId, Guid ownerId, string jobDescription);
    Task<UpdateQuestionSetJobDescriptionResponseDto> SetJobDescriptionFromFileAsync(
        Guid questionSetId, Guid ownerId, Stream file, string fileName, CancellationToken ct = default);
    Task<IReadOnlyList<QuestionSetPractitionerDto>> GetPractitionersAsync(Guid questionSetId, Guid ownerId);
    /// <summary>SCRUM-391: xuất Excel bộ câu hỏi (gate export).</summary>
    Task<QuestionExportFileDto> ExportExcelAsync(Guid questionSetId, Guid ownerId);
    /// <summary>SCRUM-391: soft-delete bộ câu hỏi (unpublish nếu đang PUBLISHED).</summary>
    Task SoftDeleteAsync(Guid questionSetId, Guid ownerId);
    /// <summary>SCRUM-396: upload ảnh đính kèm câu hỏi trong Question Set (History).</summary>
    Task<QuestionSetQuestionResponseDto> UploadQuestionImageAsync(
        Guid questionSetId, Guid questionId, Guid ownerId,
        Stream fileStream, string fileName, string contentType, long fileLength);
    /// <summary>SCRUM-396: xóa ảnh đính kèm câu hỏi trong Question Set.</summary>
    Task<QuestionSetQuestionResponseDto> DeleteQuestionImageAsync(
        Guid questionSetId, Guid questionId, Guid ownerId);
}
