using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Database;

/// <summary>
/// SCRUM-163 AC-07: Khởi tạo tài khoản Admin mặc định.
/// Apply pending migrations và seed admin user.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            // Apply migration nếu có (Roles seed qua HasData)
            await context.Database.MigrateAsync();

            // Seed Admin mặc định nếu chưa có
            if (!await context.Users.AnyAsync(u => u.Email == "admin@iqgs.com"))
            {
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi seed database.");
        }
    }
}
