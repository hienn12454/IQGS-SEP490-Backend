using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services;
using ApplicationLayer.Settings;
using Hangfire;
using Hangfire.PostgreSql;
using InfrastructureLayer.Database;
using InfrastructureLayer.External;
using InfrastructureLayer.Jobs;
using InfrastructureLayer.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using WebAPI.Middleware;

namespace WebAPI;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers()
            .AddJsonOptions(o =>
                o.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All));

        // Custom model-validation response (thay thế default 400 của ASP.NET)
        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = ctx =>
            {
                var errors = ctx.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .SelectMany(e => e.Value!.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return new JsonResult(new
                {
                    Code = 400,
                    Error = "Dữ liệu không hợp lệ.",
                    Errors = errors
                })
                { StatusCode = 400 };
            };
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "IQGS API",
                Version = "v1",
                Description = "AI-Powered Interview Question Generation System"
            });

            // Hỗ trợ JWT Bearer trong Swagger UI
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Nhập JWT token. Ví dụ: Bearer {token}",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection chưa được cấu hình.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var npgsqlDataSource = dataSourceBuilder.Build();
        builder.Services.AddSingleton(npgsqlDataSource);
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(npgsqlDataSource, o => o.UseVector()));

        builder.Services.Configure<InternalApiSettings>(
            builder.Configuration.GetSection(InternalApiSettings.SectionName));
        builder.Services.Configure<RagServiceSettings>(
            builder.Configuration.GetSection(RagServiceSettings.SectionName));
        builder.Services.Configure<BlobStorageSettings>(
            builder.Configuration.GetSection(BlobStorageSettings.SectionName));
        builder.Services.Configure<KnowledgeBaseSettings>(
            builder.Configuration.GetSection(KnowledgeBaseSettings.SectionName));
        builder.Services.Configure<WatchdogSettings>(
            builder.Configuration.GetSection(WatchdogSettings.SectionName));

        var kbSettings = builder.Configuration
            .GetSection(KnowledgeBaseSettings.SectionName)
            .Get<KnowledgeBaseSettings>() ?? new KnowledgeBaseSettings();

        builder.Services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

        builder.Services.AddHangfireServer(options =>
        {
            options.Queues = new[] { "knowledge-ingestion", "question-generation", "default" };
            options.WorkerCount = Math.Max(kbSettings.MaxConcurrentIngestJobs, 2);
        });

        var ragSettings = builder.Configuration
            .GetSection(RagServiceSettings.SectionName)
            .Get<RagServiceSettings>() ?? new RagServiceSettings();
        var internalApiKey = builder.Configuration[$"{InternalApiSettings.SectionName}:Key"] ?? string.Empty;

        builder.Services.AddHttpClient<IRagService, RagService>(client =>
        {
            client.BaseAddress = new Uri(ragSettings.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(ragSettings.TimeoutSeconds);
            if (!string.IsNullOrWhiteSpace(internalApiKey))
                client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
        });

        var jwtKey = builder.Configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey chưa được cấu hình.");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Giữ nguyên claim names gốc — không map "sub" → ClaimTypes.NameIdentifier
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["JwtSettings:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = "sub"
                };

                // SCRUM-159 AC-04: Thu hồi access token khi logout / tài khoản bị disable
                options.Events = new JwtBearerEvents
                {
                    // ── 401: Không có token hoặc token không hợp lệ ──────
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse(); // Tắt redirect mặc định của ASP.NET

                        var message = ctx.AuthenticateFailure?.Message
                            ?? "Bạn chưa đăng nhập. Vui lòng đăng nhập để tiếp tục.";

                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = "application/json; charset=utf-8";
                        await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                            new { code = 401, error = message },
                            new JsonSerializerOptions
                            {
                                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                            }));
                    },

                    // ── 403: Đã đăng nhập nhưng không đủ quyền ──────────
                    OnForbidden = async ctx =>
                    {
                        ctx.Response.StatusCode = 403;
                        ctx.Response.ContentType = "application/json; charset=utf-8";
                        await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                            new { code = 403, error = "Bạn không có quyền thực hiện thao tác này." },
                            new JsonSerializerOptions
                            {
                                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                            }));
                    },

                    // ── Kiểm tra user còn active sau khi token hợp lệ ───
                    OnTokenValidated = async ctx =>
                    {
                        // Fallback: JwtSecurityTokenHandler có thể map "sub" → NameIdentifier
                        var userIdStr = ctx.Principal?.FindFirst("sub")?.Value
                            ?? ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (!Guid.TryParse(userIdStr, out var userId))
                        {
                            ctx.Fail("Token không hợp lệ.");
                            return;
                        }

                        using var scope = ctx.HttpContext.RequestServices.CreateScope();
                        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                        var user = await userRepo.GetByIdAnyStatusAsync(userId);

                        if (user is null || !user.IsActive)
                        {
                            ctx.Fail("Tài khoản đã bị vô hiệu hóa.");
                            return;
                        }

                        // Nếu người dùng đã đăng xuất (refresh token = null) → từ chối access token
                        if (user.RefreshToken is null)
                        {
                            ctx.Fail("Phiên làm việc đã kết thúc. Vui lòng đăng nhập lại.");
                        }
                    }
                };
            });

        builder.Services.AddAuthorization();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Repositories
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IHRProfileRepository, HRProfileRepository>();
        builder.Services.AddScoped<ICandidateProfileRepository, CandidateProfileRepository>();
        builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
        builder.Services.AddScoped<IKnowledgeDocumentRepository, KnowledgeDocumentRepository>();
        builder.Services.AddScoped<IQuestionGenerationJobRepository, QuestionGenerationJobRepository>();
        builder.Services.AddScoped<IQuestionSetRepository, QuestionSetRepository>();

        // Services
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<IEmailService, EmailService>();
        builder.Services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<ICompanyService, CompanyService>();
        builder.Services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        builder.Services.AddScoped<IKnowledgeDocumentService, KnowledgeDocumentService>();
        builder.Services.AddScoped<IKnowledgeDocumentInternalService, KnowledgeDocumentInternalService>();
        builder.Services.AddScoped<IQuestionGenerationJobService, QuestionGenerationJobService>();
        builder.Services.AddScoped<IQuestionGenerationJobInternalService, QuestionGenerationJobInternalService>();
        builder.Services.AddScoped<IQuestionSetService, QuestionSetService>();
        builder.Services.AddScoped<QuestionAiContextBuilder>();
        builder.Services.AddScoped<IQuestionAiAssistService, QuestionAiAssistService>();

        // Hangfire jobs
        builder.Services.AddScoped<IKnowledgeIngestJob, KnowledgeIngestJob>();
        builder.Services.AddScoped<IGeneratePlanJob, GeneratePlanJob>();
        builder.Services.AddScoped<IGenerateQuestionsFromPlanJob, GenerateQuestionsFromPlanJob>();
        builder.Services.AddScoped<IStuckKnowledgeDocumentWatchdogJob, StuckKnowledgeDocumentWatchdogJob>();
        builder.Services.AddScoped<IStuckQuestionGenerationWatchdogJob, StuckQuestionGenerationWatchdogJob>();
        builder.Services.AddSingleton<IJobScheduler, JobScheduler>();

        var app = builder.Build();

        // SCRUM-163 AC-07: Seed database + admin account
        await DatabaseSeeder.SeedAsync(app.Services,
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder"));

        // Global exception handler — phải đứng đầu pipeline
        app.UseGlobalExceptionHandler();

        // Internal API key — chỉ route /internal/*, không dùng JWT
        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments("/internal"),
            branch => branch.UseMiddleware<InternalApiKeyMiddleware>());

        app.UseCors();

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "IQGS API v1");
            options.RoutePrefix = "swagger";
        });

        app.MapGet("/", () => Results.Redirect("/swagger"));

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHangfireDashboard("/hangfire");

        RecurringJob.AddOrUpdate<IStuckKnowledgeDocumentWatchdogJob>(
            "stuck-knowledge-documents",
            job => job.ExecuteAsync(),
            "*/15 * * * *");
        RecurringJob.AddOrUpdate<IStuckQuestionGenerationWatchdogJob>(
            "stuck-question-generation",
            job => job.ExecuteAsync(),
            "*/15 * * * *");

        app.MapControllers();
        app.Run();
    }
}
