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

        // â”€â”€ Controllers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        builder.Services.AddControllers()
            .AddJsonOptions(o =>
                o.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All));

        // Custom model-validation response (thay tháº¿ default 400 cá»§a ASP.NET)
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
                    Error = "Dá»¯ liá»‡u khÃ´ng há»£p lá»‡.",
                    Errors = errors
                })
                { StatusCode = 400 };
            };
        });

        // â”€â”€ Swagger / OpenAPI â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "IQGS API",
                Version = "v1",
                Description = "AI-Powered Interview Question Generation System"
            });

            // Há»— trá»£ JWT Bearer trong Swagger UI
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Nháº­p JWT token. VÃ­ dá»¥: Bearer {token}",
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

        // â”€â”€ Database â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection chÆ°a Ä‘Æ°á»£c cáº¥u hÃ¬nh.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var npgsqlDataSource = dataSourceBuilder.Build();
        builder.Services.AddSingleton(npgsqlDataSource);
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(npgsqlDataSource, o => o.UseVector()));

        // â”€â”€ App settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        builder.Services.Configure<InternalApiSettings>(
            builder.Configuration.GetSection(InternalApiSettings.SectionName));
        builder.Services.Configure<RagServiceSettings>(
            builder.Configuration.GetSection(RagServiceSettings.SectionName));
        builder.Services.Configure<BlobStorageSettings>(
            builder.Configuration.GetSection(BlobStorageSettings.SectionName));
        builder.Services.Configure<KnowledgeBaseSettings>(
            builder.Configuration.GetSection(KnowledgeBaseSettings.SectionName));

        var kbSettings = builder.Configuration
            .GetSection(KnowledgeBaseSettings.SectionName)
            .Get<KnowledgeBaseSettings>() ?? new KnowledgeBaseSettings();

        // â”€â”€ Hangfire â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // â”€â”€ RAG HttpClient â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // â”€â”€ JWT Authentication â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var jwtKey = builder.Configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey chÆ°a Ä‘Æ°á»£c cáº¥u hÃ¬nh.");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Giá»¯ nguyÃªn claim names gá»‘c â€” khÃ´ng map "sub" â†’ ClaimTypes.NameIdentifier
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

                // SCRUM-159 AC-04: Thu há»“i access token khi logout / tÃ i khoáº£n bá»‹ disable
                options.Events = new JwtBearerEvents
                {
                    // â”€â”€ 401: KhÃ´ng cÃ³ token hoáº·c token khÃ´ng há»£p lá»‡ â”€â”€â”€â”€â”€â”€
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse(); // Táº¯t redirect máº·c Ä‘á»‹nh cá»§a ASP.NET

                        var message = ctx.AuthenticateFailure?.Message
                            ?? "Báº¡n chÆ°a Ä‘Äƒng nháº­p. Vui lÃ²ng Ä‘Äƒng nháº­p Ä‘á»ƒ tiáº¿p tá»¥c.";

                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = "application/json; charset=utf-8";
                        await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                            new { code = 401, error = message },
                            new JsonSerializerOptions
                            {
                                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                            }));
                    },

                    // â”€â”€ 403: ÄÃ£ Ä‘Äƒng nháº­p nhÆ°ng khÃ´ng Ä‘á»§ quyá»n â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                    OnForbidden = async ctx =>
                    {
                        ctx.Response.StatusCode = 403;
                        ctx.Response.ContentType = "application/json; charset=utf-8";
                        await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                            new { code = 403, error = "Báº¡n khÃ´ng cÃ³ quyá»n thá»±c hiá»‡n thao tÃ¡c nÃ y." },
                            new JsonSerializerOptions
                            {
                                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                            }));
                    },

                    // â”€â”€ Kiá»ƒm tra user cÃ²n active sau khi token há»£p lá»‡ â”€â”€â”€
                    OnTokenValidated = async ctx =>
                    {
                        // Fallback: JwtSecurityTokenHandler cÃ³ thá»ƒ map "sub" â†’ NameIdentifier
                        var userIdStr = ctx.Principal?.FindFirst("sub")?.Value
                            ?? ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (!Guid.TryParse(userIdStr, out var userId))
                        {
                            ctx.Fail("Token khÃ´ng há»£p lá»‡.");
                            return;
                        }

                        using var scope = ctx.HttpContext.RequestServices.CreateScope();
                        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                        var user = await userRepo.GetByIdAnyStatusAsync(userId);

                        if (user is null || !user.IsActive)
                        {
                            ctx.Fail("TÃ i khoáº£n Ä‘Ã£ bá»‹ vÃ´ hiá»‡u hÃ³a.");
                            return;
                        }

                        // Náº¿u ngÆ°á»i dÃ¹ng Ä‘Ã£ Ä‘Äƒng xuáº¥t (refresh token = null) â†’ tá»« chá»‘i access token
                        if (user.RefreshToken is null)
                        {
                            ctx.Fail("PhiÃªn lÃ m viá»‡c Ä‘Ã£ káº¿t thÃºc. Vui lÃ²ng Ä‘Äƒng nháº­p láº¡i.");
                        }
                    }
                };
            });

        builder.Services.AddAuthorization();

        // â”€â”€ CORS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // â”€â”€ Dependency Injection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
        builder.Services.AddScoped<IQuestionSetService, QuestionSetService>();

        // Hangfire jobs
        builder.Services.AddScoped<IKnowledgeIngestJob, KnowledgeIngestJob>();
        builder.Services.AddScoped<IGeneratePlanJob, GeneratePlanJob>();
        builder.Services.AddScoped<IGenerateQuestionsFromPlanJob, GenerateQuestionsFromPlanJob>();
        builder.Services.AddSingleton<IJobScheduler, JobScheduler>();

        // â”€â”€ App pipeline â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var app = builder.Build();

        // SCRUM-163 AC-07: Seed database + admin account
        await DatabaseSeeder.SeedAsync(app.Services,
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder"));

        // Global exception handler â€” pháº£i Ä‘á»©ng Ä‘áº§u pipeline
        app.UseGlobalExceptionHandler();

        // Internal API key â€” chá»‰ route /internal/*, khÃ´ng dÃ¹ng JWT
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
        app.MapControllers();
        app.Run();
    }
}
