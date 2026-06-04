using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces;

public interface ICompanyRepository : IBaseRepository<Company>
{
    Task<Company?> GetByNameAsync(string name);
    Task<List<Company>> SearchAsync(string? keyword, int limit = 50);
}
