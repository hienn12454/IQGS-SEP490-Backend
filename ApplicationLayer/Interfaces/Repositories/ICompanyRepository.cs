using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface ICompanyRepository : IBaseRepository<Company>
{
    Task<Company?> GetByNameAsync(string name);
    Task<List<Company>> SearchAsync(string? keyword, int limit = 50);

    /// <summary>Thêm nhiều công ty trong 1 lần lưu (bulk create) — all-or-nothing.</summary>
    Task AddRangeAsync(IReadOnlyList<Company> companies);
}
