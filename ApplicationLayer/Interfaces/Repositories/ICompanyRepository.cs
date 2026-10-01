using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface ICompanyRepository : IBaseRepository<Company>
{
    Task<Company?> GetByNameAsync(string name);
    Task<List<Company>> SearchAsync(string? keyword, int limit = 50);

    /// <summary>SCRUM-480: tìm kiếm + phân trang (Admin). Trả items trang hiện tại và totalCount.</summary>
    Task<(List<Company> Items, int TotalCount)> SearchPagedAsync(string? keyword, int page, int pageSize);

    /// <summary>Thêm nhiều công ty trong 1 lần lưu (bulk create) — all-or-nothing.</summary>
    Task AddRangeAsync(IReadOnlyList<Company> companies);
}
