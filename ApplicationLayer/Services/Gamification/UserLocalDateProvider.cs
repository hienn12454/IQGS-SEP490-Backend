using ApplicationLayer.Interfaces.Services;

namespace ApplicationLayer.Services.Gamification;

/// <summary>Fallback UTC — xem doc-comment IUserLocalDateProvider.</summary>
public class UserLocalDateProvider : IUserLocalDateProvider
{
    public Task<DateOnly> GetLocalDateAsync(Guid userId, DateTime utcInstant)
    {
        var utc = utcInstant.Kind == DateTimeKind.Utc
            ? utcInstant
            : DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);

        return Task.FromResult(DateOnly.FromDateTime(utc));
    }
}
