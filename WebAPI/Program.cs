using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services;
using ApplicationLayer.Services.Gamification;
using ApplicationLayer.Services.Gamification.AchievementRules;
using ApplicationLayer.Studio.Interfaces;
using ApplicationLayer.Studio.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
using ApplicationLayer.Settings;
using Hangfire;
using Hangfire.PostgreSql;
using InfrastructureLayer.Database;
using InfrastructureLayer.External;
using InfrastructureLayer.Jobs;
using InfrastructureLayer.Repository;
using InfrastructureLayer.Services.Gamification;
using InfrastructureLayer.Services.Studio;
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
using System.Text.Json.Serialization;
using System.Text.Unicode;
using WebAPI.Hubs;
using WebAPI.Middleware;
using WebAPI.Realtime;
using Serilog;

namespace WebAPI;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseSerilog((ctx, cfg) =>
            cfg.ReadFrom.Configuration(ctx.Configuration)
               .WriteTo.Console()
               .WriteTo.File("logs/webapi-.log", rollingInterval: RollingInterval.Day));

        builder.Services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddFluentValidationClientsideAdapters();
        builder.Services.AddValidatorsFromAssemblyContaining<CreateStudioProjectRequestValidator>();

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

            // Đọc XML doc comment (/// <summary>) từ WebAPI + ApplicationLayer để hiển thị mô tả API/DTO trên Swagger UI
            foreach (var xmlFile in new[] { "WebAPI.xml", "ApplicationLayer.xml" })
            {
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                    // false: không hiện XML summary cạnh tên controller trên Swagger UI
                    options.IncludeXmlComments(xmlPath, includeControllerXmlComments: false);
            }
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
        builder.Services.Configure<SePaySettings>(
            builder.Configuration.GetSection(SePaySettings.SectionName));
        builder.Services.Configure<KnowledgeBaseSettings>(
            builder.Configuration.GetSection(KnowledgeBaseSettings.SectionName));
        builder.Services.Configure<CvSettings>(
            builder.Configuration.GetSection(CvSettings.SectionName));
        builder.Services.Configure<WatchdogSettings>(
            builder.Configuration.GetSection(WatchdogSettings.SectionName));
        builder.Services.Configure<GamificationOptions>(
            builder.Configuration.GetSection(GamificationOptions.SectionName));

        var kbSettings = builder.Configuration
            .GetSection(KnowledgeBaseSettings.SectionName)
            .Get<KnowledgeBaseSettings>() ?? new KnowledgeBaseSettings();
        var sePaySettings = builder.Configuration
            .GetSection(SePaySettings.SectionName)
            .Get<SePaySettings>() ?? new SePaySettings();
        ValidateSePaySettings(sePaySettings);

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
                    },

                    // SCRUM-387: SignalR browser gửi JWT qua query ?access_token=...
                    OnMessageReceived = ctx =>
                    {
                        var accessToken = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken)
                            && path.StartsWithSegments("/hubs"))
                        {
                            ctx.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization();

        // SCRUM-387: realtime payment push (hub + notifier)
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<ISubscriptionPaymentRealtimeNotifier, SignalRSubscriptionPaymentNotifier>();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                // SignalR negotiate + WebSocket: không dùng AllowAnyOrigin khi cần cookies.
                // FE dùng JWT access_token (query) — không cookie → Keep AllowAnyOrigin.
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Repositories
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IHRProfileRepository, HRProfileRepository>();
        builder.Services.AddScoped<ICandidateProfileRepository, CandidateProfileRepository>();
        builder.Services.AddScoped<IQuestionSetBookmarkRepository, QuestionSetBookmarkRepository>();
        builder.Services.AddScoped<IPracticeSessionRepository, PracticeSessionRepository>();
        builder.Services.AddScoped<ICandidateAnswerRepository, CandidateAnswerRepository>();
        builder.Services.AddScoped<IAiFeedbackRepository, AiFeedbackRepository>();
        builder.Services.AddScoped<ICandidateMarketplaceRepository, CandidateMarketplaceRepository>();
        builder.Services.AddScoped<ICandidatePersonalSetJobRepository, CandidatePersonalSetJobRepository>();
        builder.Services.AddScoped<ICandidateSkillPlanRepository, CandidateSkillPlanRepository>();
        builder.Services.AddScoped<IAdminMarketplaceRepository, AdminMarketplaceRepository>();
        builder.Services.AddScoped<ICandidateRecommendationRepository, CandidateRecommendationRepository>();
        builder.Services.AddScoped<ICandidateInvitationRepository, CandidateInvitationRepository>();
        builder.Services.AddScoped<ICandidateOfferRepository, CandidateOfferRepository>();
        builder.Services.AddScoped<IPlatformSettingsRepository, PlatformSettingsRepository>();
        builder.Services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
        builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        builder.Services.AddScoped<IUsageCounterRepository, UsageCounterRepository>();
        builder.Services.AddScoped<ISubscriptionTransactionRepository, SubscriptionTransactionRepository>();
        builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
        builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
        builder.Services.AddScoped<IKnowledgeDocumentRepository, KnowledgeDocumentRepository>();
        builder.Services.AddScoped<IQuestionGenerationJobRepository, QuestionGenerationJobRepository>();
        builder.Services.AddScoped<IQuestionSetRepository, QuestionSetRepository>();
        builder.Services.AddScoped<IHrDashboardStudioStatsRepository, HrDashboardStudioStatsRepository>();
        builder.Services.AddScoped<IHrQuestionSetBookmarkRepository, HrQuestionSetBookmarkRepository>();
        builder.Services.AddScoped<IQuestionSetFeedbackRepository, QuestionSetFeedbackRepository>();
        builder.Services.AddScoped<IQuestionSetJdFitReviewRepository, QuestionSetJdFitReviewRepository>();

        // Services
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<IEmailService, EmailService>();
        builder.Services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<ICompanyService, CompanyService>();
        builder.Services.AddScoped<IFeedbackService, FeedbackService>();
        builder.Services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        // SCRUM-385: HttpClient cho SePay User API v2 (timeout ngắn, rate-limit thân thiện)
        builder.Services.AddHttpClient<ISePayGateway, SePayGateway>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(12);
        });
        builder.Services.AddScoped<IKnowledgeDocumentService, KnowledgeDocumentService>();
        builder.Services.AddScoped<ICandidateCvService, CandidateCvService>();
        builder.Services.AddScoped<ICandidateQuestionSetService, CandidateQuestionSetService>();
        builder.Services.AddScoped<ICandidatePersonalSetService, CandidatePersonalSetService>();
        builder.Services.AddScoped<ICandidateSkillPlanService, CandidateSkillPlanService>();
        builder.Services.AddScoped<ICandidateBookmarkService, CandidateBookmarkService>();
        builder.Services.AddScoped<ICandidatePracticeSessionService, CandidatePracticeSessionService>();
        builder.Services.AddScoped<IQuestionSetFeedbackService, QuestionSetFeedbackService>();

        // ── Gamification ─────────────────────────────────────────────
        builder.Services.AddScoped<ILevelCalculator, LevelCalculator>();
        builder.Services.AddScoped<IXpRewardPolicy, XpRewardPolicy>();
        builder.Services.AddScoped<IStreakCalculator, StreakCalculator>();
        builder.Services.AddScoped<IUserLocalDateProvider, UserLocalDateProvider>();
        builder.Services.AddScoped<IAchievementRule, FirstStepAchievementRule>();
        builder.Services.AddScoped<IAchievementRule, OnFireAchievementRule>();
        builder.Services.AddScoped<IAchievementRule, ExcellentAnswerAchievementRule>();
        builder.Services.AddScoped<IAchievementRule, DedicatedAchievementRule>();
        builder.Services.AddScoped<IAchievementRule, TechnicalMindAchievementRule>();
        builder.Services.AddScoped<IAchievementRule, SystemThinkerAchievementRule>();
        builder.Services.AddScoped<IAchievementRule, ConsistencyAchievementRule>();
        builder.Services.AddScoped<IAchievementRule, InterviewVeteranAchievementRule>();
        builder.Services.AddScoped<IGamificationService, GamificationService>();
        builder.Services.AddScoped<ICandidatePrivacySettingsService, CandidatePrivacySettingsService>();
        builder.Services.AddScoped<IRecommendationService, RecommendationService>();
        builder.Services.AddScoped<IHrCandidateOverviewService, HrCandidateOverviewService>();
        builder.Services.AddScoped<ICandidateInvitationService, CandidateInvitationService>();
        builder.Services.AddScoped<ICandidateOfferService, CandidateOfferService>();
        builder.Services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();
        builder.Services.AddScoped<IUsageMeteringService, UsageMeteringService>();
        builder.Services.AddScoped<ISubscriptionGateService, SubscriptionGateService>();
        builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
        builder.Services.AddScoped<IAdminSubscriptionPlanService, AdminSubscriptionPlanService>();
        builder.Services.AddScoped<IAdminMarketplaceService, AdminMarketplaceService>();
        builder.Services.AddScoped<IKnowledgeDocumentInternalService, KnowledgeDocumentInternalService>();
        builder.Services.AddScoped<IQuestionGenerationJobService, QuestionGenerationJobService>();
        builder.Services.AddScoped<IQuestionGenerationJobInternalService, QuestionGenerationJobInternalService>();
        builder.Services.AddScoped<IQuestionSetService, QuestionSetService>();
        builder.Services.AddScoped<IQuestionSetJdFitService, QuestionSetJdFitService>();
        builder.Services.AddScoped<IHrCompanyInfoService, HrCompanyInfoService>();
        builder.Services.AddScoped<IHrDashboardService, HrDashboardService>();
        builder.Services.AddScoped<IHrBookmarkService, HrBookmarkService>();
        builder.Services.AddScoped<QuestionAiContextBuilder>();
        builder.Services.AddScoped<IQuestionAiAssistService, QuestionAiAssistService>();
        builder.Services.AddScoped<IInterviewProjectService, InterviewProjectService>();
        builder.Services.AddScoped<IJobDescriptionService, JobDescriptionService>();
        builder.Services.AddScoped<IJobDescriptionAnalyzer, MockJobDescriptionAnalyzer>();
        builder.Services.AddScoped<IStudioJobDescriptionUploadService, StudioJobDescriptionUploadService>();
        builder.Services.AddScoped<IStudioKnowledgeDocumentService, StudioKnowledgeRagService>();
        builder.Services.AddScoped<IInterviewPlanService, InterviewPlanService>();
        builder.Services.AddScoped<IQuestionGenerationService, QuestionGenerationService>();
        builder.Services.AddScoped<IAiChatService, AiChatService>();
        builder.Services.AddScoped<IStudioSettingsService, StudioSettingsService>();
        builder.Services.AddScoped<IStudioShareService, StudioShareService>();
        builder.Services.AddSingleton<IStudioMockAiService, StudioMockAiService>();
        builder.Services.AddScoped<IDocumentTextExtractor, PdfTextExtractor>();
        builder.Services.AddScoped<IDocumentTextExtractor, DocxTextExtractor>();
        builder.Services.AddScoped<IDocumentTextExtractor, TxtTextExtractor>();
        builder.Services.AddScoped<IDocumentTextExtractorFactory, DocumentTextExtractorFactory>();

        // Hangfire jobs
        builder.Services.AddScoped<IKnowledgeIngestJob, KnowledgeIngestJob>();
        builder.Services.AddScoped<IGeneratePlanJob, GeneratePlanJob>();
        builder.Services.AddScoped<IGenerateQuestionsFromPlanJob, GenerateQuestionsFromPlanJob>();
        builder.Services.AddScoped<IGenerateCandidatePersonalSetJob, GenerateCandidatePersonalSetJob>();
        builder.Services.AddScoped<IStuckKnowledgeDocumentWatchdogJob, StuckKnowledgeDocumentWatchdogJob>();
        builder.Services.AddScoped<IStuckQuestionGenerationWatchdogJob, StuckQuestionGenerationWatchdogJob>();
        builder.Services.AddScoped<IExpiredPracticeSessionWatchdogJob, ExpiredPracticeSessionWatchdogJob>();
        builder.Services.AddScoped<IExpirePendingUpgradeOrdersJob, ExpirePendingUpgradeOrdersJob>();
        builder.Services.AddSingleton<IJobScheduler, JobScheduler>();

        var app = builder.Build();

        // SCRUM-163 AC-07: Seed database + admin account
        await DatabaseSeeder.SeedAsync(app.Services,
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder"));

        // Global exception handler — phải đứng đầu pipeline
        app.UseGlobalExceptionHandler();

        // Fallback cho các request không khớp route nào (VD: :guid constraint không hợp lệ)
        // — ASP.NET trả 404 rỗng trước khi vào tới controller/exception middleware, nên cần map thủ công.
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var ctx = statusCodeContext.HttpContext;
            var message = ctx.Response.StatusCode switch
            {
                404 => "Không tìm thấy tài nguyên yêu cầu.",
                405 => "Phương thức không được hỗ trợ.",
                _ => "Đã xảy ra lỗi khi xử lý yêu cầu."
            };

            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                new { code = ctx.Response.StatusCode, error = message },
                new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) }));
        });

        // Internal API key — chỉ route /internal/*, không dùng JWT
        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments("/internal"),
            branch => branch.UseMiddleware<InternalApiKeyMiddleware>());

        app.UseCors();
        app.UseSerilogRequestLogging();

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
        RecurringJob.AddOrUpdate<IExpiredPracticeSessionWatchdogJob>(
            "expired-practice-sessions",
            job => job.ExecuteAsync(),
            "* * * * *");
        // TTL 10p đơn SePay upgrade — dọn Pending quá ExpiresAt
        RecurringJob.AddOrUpdate<IExpirePendingUpgradeOrdersJob>(
            "expire-pending-upgrade-orders",
            job => job.ExecuteAsync(),
            "*/5 * * * *");

        app.MapControllers();
        // SCRUM-387: FE kết nối Hub này để nhận PaymentPaid sau webhook SePay
        app.MapHub<SubscriptionPaymentHub>(SubscriptionPaymentHub.HubPath);
        app.Run();
    }

    private static void ValidateSePaySettings(SePaySettings settings)
    {
        if (!settings.Enabled) return;

        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
            throw new InvalidOperationException("SePay:BaseUrl chưa được cấu hình.");
        if (string.IsNullOrWhiteSpace(settings.BankAccountName))
            throw new InvalidOperationException("SePay:BankAccountName chưa được cấu hình.");
        if (string.IsNullOrWhiteSpace(settings.BankAccountNumber))
            throw new InvalidOperationException("SePay:BankAccountNumber chưa được cấu hình.");
        if (settings.StrictSignatureValidation && string.IsNullOrWhiteSpace(settings.WebhookSecret))
            throw new InvalidOperationException("SePay:WebhookSecret chưa được cấu hình khi StrictSignatureValidation=true.");
    }
}
