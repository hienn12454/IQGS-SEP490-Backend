using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Database;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            await context.Database.MigrateAsync();
            await SeedAdminAsync(context, logger);
            await SeedCompaniesAsync(context, logger);
            await SeedDefaultHRAsync(context, logger);
            await SeedDefaultCandidateAsync(context, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi seed database.");
        }
    }

    private static async Task SeedAdminAsync(AppDbContext context, ILogger logger)
    {
        if (await context.Users.AnyAsync(u => u.Email == "admin@iqgs.com"))
            return;

        context.Users.Add(new User
        {
            FullName = "System Administrator",
            Email = "admin@iqgs.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("adminiqgs", workFactor: 12),
            RoleId = UserRole.AdminId,
            Provider = AuthProvider.Local,
            IsEmailVerified = true,
            IsProfileComplete = true
        });

        await context.SaveChangesAsync();
        logger.LogInformation("Đã tạo tài khoản admin mặc định: admin@iqgs.com");
    }

    private static async Task SeedCompaniesAsync(AppDbContext context, ILogger logger)
    {
        if (await context.Companies.AnyAsync())
            return;

        var companies = new List<Company>
        {
            new() { Name = "FPT Software",              WebsiteUrl = "https://www.fpt-software.com",   Description = "Công ty phần mềm lớn nhất Việt Nam, thành viên tập đoàn FPT" },
            new() { Name = "Viettel Group",              WebsiteUrl = "https://viettel.com.vn",          Description = "Tập đoàn Công nghiệp – Viễn thông Quân đội Viettel" },
            new() { Name = "VNG Corporation",            WebsiteUrl = "https://www.vng.com.vn",          Description = "Công ty công nghệ hàng đầu Việt Nam, phát triển Zalo và ZaloPay" },
            new() { Name = "Tiki Corporation",           WebsiteUrl = "https://tiki.vn",                 Description = "Sàn thương mại điện tử hàng đầu Việt Nam" },
            new() { Name = "Shopee Vietnam",             WebsiteUrl = "https://shopee.vn",               Description = "Nền tảng thương mại điện tử hàng đầu Đông Nam Á" },
            new() { Name = "Grab Vietnam",               WebsiteUrl = "https://www.grab.com/vn",         Description = "Super app dịch vụ di chuyển, giao đồ ăn và thanh toán" },
            new() { Name = "MoMo",                       WebsiteUrl = "https://momo.vn",                 Description = "Ví điện tử số 1 Việt Nam với hơn 31 triệu người dùng" },
            new() { Name = "VNPT Technology",            WebsiteUrl = "https://vnpt-technology.vn",      Description = "Công ty công nghệ thuộc tập đoàn VNPT" },
            new() { Name = "CMC Technology & Solution",  WebsiteUrl = "https://cmcts.com.vn",            Description = "Công ty giải pháp công nghệ thông tin hàng đầu Việt Nam" },
            new() { Name = "NashTech Vietnam",           WebsiteUrl = "https://nashtechglobal.com",      Description = "Công ty công nghệ thuộc tập đoàn Nash Squared" },
            new() { Name = "KMS Technology",             WebsiteUrl = "https://kms-technology.com",      Description = "Công ty phần mềm và dịch vụ QA hàng đầu Việt Nam" },
            new() { Name = "TMA Solutions",              WebsiteUrl = "https://www.tmasolutions.com",    Description = "Công ty gia công phần mềm lớn nhất tại TP.HCM" },
            new() { Name = "Axon Active Vietnam",        WebsiteUrl = "https://axonactive.com",          Description = "Công ty phần mềm Thụy Sĩ với trung tâm R&D tại Việt Nam" },
            new() { Name = "Gameloft Vietnam",           WebsiteUrl = "https://www.gameloft.com",        Description = "Studio phát triển game mobile lớn tại Việt Nam" },
            new() { Name = "Sky Mavis",                  WebsiteUrl = "https://skymavis.com",            Description = "Công ty blockchain game phát triển Axie Infinity" },
            new() { Name = "Cốc Cốc",                   WebsiteUrl = "https://coccoc.com",              Description = "Trình duyệt và công cụ tìm kiếm phổ biến nhất Việt Nam" },
            new() { Name = "Base.vn",                    WebsiteUrl = "https://base.vn",                 Description = "Nền tảng quản trị doanh nghiệp toàn diện Made in Vietnam" },
            new() { Name = "Haravan",                    WebsiteUrl = "https://www.haravan.com",         Description = "Nền tảng thương mại điện tử và bán lẻ đa kênh" },
            new() { Name = "Logivan",                    WebsiteUrl = "https://logivan.com",             Description = "Startup logistics công nghệ kết nối xe tải và hàng hóa" },
            new() { Name = "Amanotes",                   WebsiteUrl = "https://amanotes.com",            Description = "Nhà xuất bản game âm nhạc mobile lớn nhất thế giới" },
        };

        context.Companies.AddRange(companies);
        await context.SaveChangesAsync();
        logger.LogInformation("Đã seed {Count} công ty mẫu.", companies.Count);
    }

    private static async Task SeedDefaultHRAsync(AppDbContext context, ILogger logger)
    {
        if (await context.Users.AnyAsync(u => u.Email == "hr@iqgs.com"))
            return;

        var company = await context.Companies.FirstOrDefaultAsync(c => c.Name == "FPT Software");
        if (company is null)
        {
            logger.LogWarning("Bỏ qua seed tài khoản HR mặc định vì chưa có công ty nào.");
            return;
        }

        var user = new User
        {
            FullName = "Default HR Manager",
            Email = "hr@iqgs.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("hriqgs", workFactor: 12),
            RoleId = UserRole.HRId,
            Provider = AuthProvider.Local,
            IsEmailVerified = true,
            IsProfileComplete = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.HRProfiles.Add(new HRProfile
        {
            UserId = user.Id,
            CompanyId = company.Id,
            JobTitle = "HR Manager",
            IsCompanyVerified = true
        });
        await context.SaveChangesAsync();

        logger.LogInformation("Đã tạo tài khoản HR mặc định: hr@iqgs.com");
    }

    private static async Task SeedDefaultCandidateAsync(AppDbContext context, ILogger logger)
    {
        if (await context.Users.AnyAsync(u => u.Email == "candidate@iqgs.com"))
            return;

        var user = new User
        {
            FullName = "Default Job Seeker",
            Email = "candidate@iqgs.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("candidateiqgs", workFactor: 12),
            RoleId = UserRole.CandidateId,
            Provider = AuthProvider.Local,
            IsEmailVerified = true,
            IsProfileComplete = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.CandidateProfiles.Add(new CandidateProfile
        {
            UserId = user.Id,
            TargetRole = "Backend Developer",
            SeniorityLevel = "Junior"
        });
        await context.SaveChangesAsync();

        logger.LogInformation("Đã tạo tài khoản Job Seeker mặc định: candidate@iqgs.com");
    }
}
