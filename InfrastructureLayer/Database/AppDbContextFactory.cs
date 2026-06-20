using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace InfrastructureLayer.Database;

// Factory này dùng cho EF Core design-time (dotnet ef migrations add...)
// Cần thiết vì InfrastructureLayer không phải startup project — EF cần cách tìm connection string
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Trỏ lên thư mục WebAPI để đọc appsettings.json
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "WebAPI");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(configPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection chưa được cấu hình.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(dataSource, npgsql => npgsql.UseVector());

        return new AppDbContext(optionsBuilder.Options);
    }
}
