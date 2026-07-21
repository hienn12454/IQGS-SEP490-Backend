using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;

namespace ApplicationLayer.Services;

public class PlatformSettingsService : IPlatformSettingsService
{
    private readonly IPlatformSettingsRepository _repository;

    public PlatformSettingsService(IPlatformSettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<PlatformSettingsDto> GetAsync()
    {
        var settings = await _repository.GetAsync();
        return new PlatformSettingsDto { MinQuestionsToPublish = settings.MinQuestionsToPublish };
    }

    public async Task<PlatformSettingsDto> UpdateAsync(UpdatePlatformSettingsDto dto)
    {
        var settings = await _repository.GetAsync();
        settings.MinQuestionsToPublish = dto.MinQuestionsToPublish;
        await _repository.UpdateAsync(settings);

        return new PlatformSettingsDto { MinQuestionsToPublish = settings.MinQuestionsToPublish };
    }
}
