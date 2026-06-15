using InfrastructureLayer.Database;
using InfrastructureLayer.Repository;
using InfrastructureLayer.Repository.Interface;
using Microsoft.EntityFrameworkCore;

namespace WebAPI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // PostgreSQL — connection string lấy từ appsettings.json
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Đăng ký Repository theo pattern Scoped (mỗi HTTP request tạo một instance mới)
        builder.Services.AddScoped<IUserRepository, UserRepository>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }
}
