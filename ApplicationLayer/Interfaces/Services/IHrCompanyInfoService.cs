namespace ApplicationLayer.Interfaces.Services;

/// <summary>Tra cứu tên + logo công ty của 1 HR (qua HRProfile.CompanyId) — dùng chung cho mọi response HR-facing cần hiển thị công ty.</summary>
public interface IHrCompanyInfoService
{
    /// <summary>Trả về tên công ty (rỗng nếu HR chưa có HRProfile) và logo (luôn có giá trị nhờ fallback).</summary>
    Task<(string Name, string Logo)> GetByHrUserIdAsync(Guid hrUserId);
}
