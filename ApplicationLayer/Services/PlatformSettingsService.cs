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
        return Map(settings);
    }

    public async Task<PlatformSettingsDto> UpdateAsync(UpdatePlatformSettingsDto dto)
    {
        var settings = await _repository.GetAsync();
        settings.MinQuestionsToPublish = dto.MinQuestionsToPublish;
        settings.MaxPinnedSets = dto.MaxPinnedSets;
        settings.MinAttemptsForTrending = dto.MinAttemptsForTrending;
        await _repository.UpdateAsync(settings);

        return Map(settings);
    }

    private static PlatformSettingsDto Map(DomainLayer.Entities.PlatformSettings settings) => new()
    {
        MinQuestionsToPublish = settings.MinQuestionsToPublish,
        MaxPinnedSets = settings.MaxPinnedSets,
        MinAttemptsForTrending = settings.MinAttemptsForTrending
    };
}
