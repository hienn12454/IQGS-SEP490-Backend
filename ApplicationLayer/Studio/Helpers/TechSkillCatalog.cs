using ApplicationLayer.Helpers;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>Một dòng catalog trả về FE: tên enum, nhãn hiển thị, nhóm, alias để map JD.</summary>
public sealed record TechSkillCatalogItem(
    string Name,
    string Label,
    string Group,
    IReadOnlyList<string> Aliases);

/// <summary>
/// Nhãn hiển thị + map text JD/RAG về đúng một skill trong <see cref="TechSkill"/>.
/// Compact/token dùng <see cref="TechSkillMatcher"/> để "CSharp"/"csharp" → "C#", "React.js" → "React".
/// </summary>
public static class TechSkillCatalog
{
    public const string Languages = "Languages";
    public const string Backend = "Backend";
    public const string Frontend = "Frontend";
    public const string Mobile = "Mobile";
    public const string Database = "Database";
    public const string Messaging = "Messaging";
    public const string Cloud = "Cloud";
    public const string DevOps = "DevOps";
    public const string Testing = "Testing";
    public const string Architecture = "Architecture";
    public const string Api = "API";
    public const string DataAi = "Data / AI";
    public const string Security = "Security";
    public const string Tools = "Tools";

    private static readonly List<TechSkillCatalogItem> Items = new();
    private static readonly Dictionary<string, string> CompactToLabel = new(StringComparer.Ordinal);

    static TechSkillCatalog()
    {
        // Languages
        Add(TechSkill.CSharp, "C#", Languages, "csharp", "CSharp");
        Add(TechSkill.Java, "Java", Languages);
        Add(TechSkill.Kotlin, "Kotlin", Languages);
        Add(TechSkill.Python, "Python", Languages);
        Add(TechSkill.JavaScript, "JavaScript", Languages, "js", "Javascript");
        Add(TechSkill.TypeScript, "TypeScript", Languages, "ts", "Typescript");
        Add(TechSkill.Go, "Go", Languages, "golang", "Golang");
        Add(TechSkill.Rust, "Rust", Languages);
        Add(TechSkill.PHP, "PHP", Languages);
        Add(TechSkill.Ruby, "Ruby", Languages);
        Add(TechSkill.Swift, "Swift", Languages);
        Add(TechSkill.Dart, "Dart", Languages);
        Add(TechSkill.Cpp, "C++", Languages, "cpp", "cplusplus");
        Add(TechSkill.C, "C", Languages);
        Add(TechSkill.Scala, "Scala", Languages);
        Add(TechSkill.SQL, "SQL", Languages);
        Add(TechSkill.Bash, "Bash", Languages);
        Add(TechSkill.PowerShell, "PowerShell", Languages);
        Add(TechSkill.R, "R", Languages);
        Add(TechSkill.Lua, "Lua", Languages);

        // Backend
        Add(TechSkill.DotNet, ".NET", Backend, "dotnet", "DotNet");
        Add(TechSkill.AspNetCore, "ASP.NET Core", Backend, "aspnetcore", "AspNetCore");
        Add(TechSkill.AspNetMvc, "ASP.NET MVC", Backend, "aspnetmvc", "AspNetMvc");
        Add(TechSkill.WebApi, "Web API", Backend, "webapi");
        Add(TechSkill.SignalR, "SignalR", Backend);
        Add(TechSkill.Blazor, "Blazor", Backend);
        Add(TechSkill.Grpc, "gRPC", Backend, "grpc");
        Add(TechSkill.MinimalApi, "Minimal API", Backend, "minimalapi");
        Add(TechSkill.SpringBoot, "Spring Boot", Backend, "springboot", "SpringBoot");
        Add(TechSkill.Hibernate, "Hibernate", Backend);
        Add(TechSkill.NodeJs, "Node.js", Backend, "nodejs", "node", "NodeJs");
        Add(TechSkill.ExpressJs, "Express.js", Backend, "express", "expressjs");
        Add(TechSkill.NestJs, "NestJS", Backend, "nestjs", "NestJs");
        Add(TechSkill.Django, "Django", Backend);
        Add(TechSkill.FastApi, "FastAPI", Backend, "fastapi");
        Add(TechSkill.Flask, "Flask", Backend);
        Add(TechSkill.Laravel, "Laravel", Backend);
        Add(TechSkill.RubyOnRails, "Ruby on Rails", Backend, "rails", "rubyonrails");
        Add(TechSkill.Wpf, "WPF", Backend);
        Add(TechSkill.WinForms, "WinForms", Backend, "winforms");

        // Frontend
        Add(TechSkill.Html, "HTML", Frontend);
        Add(TechSkill.Css, "CSS", Frontend);
        Add(TechSkill.Sass, "Sass", Frontend, "scss");
        Add(TechSkill.TailwindCss, "Tailwind CSS", Frontend, "tailwind", "tailwindcss");
        Add(TechSkill.Bootstrap, "Bootstrap", Frontend);
        Add(TechSkill.MaterialUI, "Material UI", Frontend, "mui", "materialui");
        Add(TechSkill.AntDesign, "Ant Design", Frontend, "antd", "antdesign");
        Add(TechSkill.ShadcnUI, "shadcn/ui", Frontend, "shadcn", "shadcnui");
        Add(TechSkill.React, "React", Frontend, "reactjs", "react.js");
        Add(TechSkill.NextJs, "Next.js", Frontend, "nextjs", "next.js");
        Add(TechSkill.Vue, "Vue", Frontend, "vuejs", "vue.js");
        Add(TechSkill.Nuxt, "Nuxt", Frontend, "nuxtjs");
        Add(TechSkill.Angular, "Angular", Frontend);
        Add(TechSkill.Svelte, "Svelte", Frontend);
        Add(TechSkill.jQuery, "jQuery", Frontend, "jquery");
        Add(TechSkill.Redux, "Redux", Frontend);
        Add(TechSkill.Zustand, "Zustand", Frontend);
        Add(TechSkill.ReactQuery, "React Query", Frontend, "reactquery", "tanstack query");
        Add(TechSkill.ReactHookForm, "React Hook Form", Frontend, "reacthookform");
        Add(TechSkill.Vite, "Vite", Frontend);
        Add(TechSkill.Webpack, "Webpack", Frontend);
        Add(TechSkill.Storybook, "Storybook", Frontend);

        // Mobile / desktop / game
        Add(TechSkill.ReactNative, "React Native", Mobile, "reactnative", "react-native");
        Add(TechSkill.Flutter, "Flutter", Mobile);
        Add(TechSkill.AndroidNative, "Android", Mobile, "android");
        Add(TechSkill.IosNative, "iOS", Mobile, "ios");
        Add(TechSkill.Xamarin, "Xamarin", Mobile);
        Add(TechSkill.Maui, ".NET MAUI", Mobile, "maui");
        Add(TechSkill.Ionic, "Ionic", Mobile);
        Add(TechSkill.Electron, "Electron", Mobile);
        Add(TechSkill.Unity, "Unity", Mobile);
        Add(TechSkill.UnrealEngine, "Unreal Engine", Mobile, "unreal", "unrealengine");
        Add(TechSkill.Godot, "Godot", Mobile);
        Add(TechSkill.Arduino, "Arduino", Mobile);
        Add(TechSkill.RaspberryPi, "Raspberry Pi", Mobile, "raspberrypi");
        Add(TechSkill.Rtos, "RTOS", Mobile);

        // Database
        Add(TechSkill.SqlServer, "SQL Server", Database, "sqlserver", "mssql", "SqlServer");
        Add(TechSkill.PostgreSql, "PostgreSQL", Database, "postgres", "psql", "PostgreSql");
        Add(TechSkill.MySql, "MySQL", Database, "mysql");
        Add(TechSkill.Oracle, "Oracle", Database);
        Add(TechSkill.Sqlite, "SQLite", Database, "sqlite");
        Add(TechSkill.MariaDb, "MariaDB", Database, "mariadb");
        Add(TechSkill.MongoDb, "MongoDB", Database, "mongodb");
        Add(TechSkill.Cassandra, "Cassandra", Database);
        Add(TechSkill.DynamoDb, "DynamoDB", Database, "dynamodb");
        Add(TechSkill.CosmosDb, "Cosmos DB", Database, "cosmosdb");
        Add(TechSkill.Neo4j, "Neo4j", Database);
        Add(TechSkill.ClickHouse, "ClickHouse", Database, "clickhouse");
        Add(TechSkill.EntityFrameworkCore, "EF Core", Database, "efcore", "entityframework", "entity framework", "EntityFrameworkCore");
        Add(TechSkill.Dapper, "Dapper", Database);
        Add(TechSkill.AdoNet, "ADO.NET", Database, "adonet");
        Add(TechSkill.Prisma, "Prisma", Database);
        Add(TechSkill.TypeOrm, "TypeORM", Database, "typeorm");
        Add(TechSkill.Sequelize, "Sequelize", Database);
        Add(TechSkill.Flyway, "Flyway", Database);
        Add(TechSkill.Liquibase, "Liquibase", Database);

        // Cache / messaging
        Add(TechSkill.Redis, "Redis", Messaging);
        Add(TechSkill.Memcached, "Memcached", Messaging);
        Add(TechSkill.Elasticsearch, "Elasticsearch", Messaging, "elastic");
        Add(TechSkill.OpenSearch, "OpenSearch", Messaging, "opensearch");
        Add(TechSkill.Solr, "Solr", Messaging);
        Add(TechSkill.RabbitMq, "RabbitMQ", Messaging, "rabbitmq");
        Add(TechSkill.Kafka, "Kafka", Messaging);
        Add(TechSkill.AzureServiceBus, "Azure Service Bus", Messaging, "azureservicebus", "service bus");
        Add(TechSkill.AwsSqs, "AWS SQS", Messaging, "sqs", "awssqs");
        Add(TechSkill.MassTransit, "MassTransit", Messaging, "masstransit");
        Add(TechSkill.NServiceBus, "NServiceBus", Messaging, "nservicebus");
        Add(TechSkill.Hangfire, "Hangfire", Messaging);
        Add(TechSkill.Quartz, "Quartz", Messaging);
        Add(TechSkill.MediatR, "MediatR", Messaging, "mediatr");

        // Cloud
        Add(TechSkill.Aws, "AWS", Cloud);
        Add(TechSkill.Azure, "Azure", Cloud);
        Add(TechSkill.Gcp, "GCP", Cloud, "google cloud");
        Add(TechSkill.AwsLambda, "AWS Lambda", Cloud, "awslambda");
        Add(TechSkill.AzureFunctions, "Azure Functions", Cloud, "azurefunctions");
        Add(TechSkill.S3, "Amazon S3", Cloud, "s3");
        Add(TechSkill.AzureBlobStorage, "Azure Blob Storage", Cloud, "azureblobstorage", "blob storage");
        Add(TechSkill.Cloudflare, "Cloudflare", Cloud);
        Add(TechSkill.Vercel, "Vercel", Cloud);
        Add(TechSkill.Heroku, "Heroku", Cloud);
        Add(TechSkill.Firebase, "Firebase", Cloud);
        Add(TechSkill.Supabase, "Supabase", Cloud);

        // DevOps
        Add(TechSkill.Docker, "Docker", DevOps);
        Add(TechSkill.Kubernetes, "Kubernetes", DevOps, "k8s");
        Add(TechSkill.Terraform, "Terraform", DevOps);
        Add(TechSkill.Ansible, "Ansible", DevOps);
        Add(TechSkill.Nginx, "Nginx", DevOps);
        Add(TechSkill.Linux, "Linux", DevOps);
        Add(TechSkill.Helm, "Helm", DevOps);
        Add(TechSkill.GitHubActions, "GitHub Actions", DevOps, "githubactions");
        Add(TechSkill.GitLabCi, "GitLab CI", DevOps, "gitlabci");
        Add(TechSkill.Jenkins, "Jenkins", DevOps);
        Add(TechSkill.AzureDevOps, "Azure DevOps", DevOps, "azuredevops");
        Add(TechSkill.ArgoCd, "Argo CD", DevOps, "argocd");
        Add(TechSkill.Prometheus, "Prometheus", DevOps);
        Add(TechSkill.Grafana, "Grafana", DevOps);
        Add(TechSkill.Elk, "ELK", DevOps);
        Add(TechSkill.Serilog, "Serilog", DevOps);
        Add(TechSkill.OpenTelemetry, "OpenTelemetry", DevOps, "opentelemetry");
        Add(TechSkill.Sentry, "Sentry", DevOps);
        Add(TechSkill.Datadog, "Datadog", DevOps);
        Add(TechSkill.Networking, "Networking", DevOps);
        Add(TechSkill.LoadBalancing, "Load Balancing", DevOps, "loadbalancing");

        // Testing
        Add(TechSkill.UnitTesting, "Unit Testing", Testing, "unittesting", "unit test");
        Add(TechSkill.IntegrationTesting, "Integration Testing", Testing, "integrationtesting");
        Add(TechSkill.TestAutomation, "Test Automation", Testing, "testautomation");
        Add(TechSkill.TestDrivenDevelopment, "TDD", Testing, "tdd");
        Add(TechSkill.XUnit, "xUnit", Testing, "xunit");
        Add(TechSkill.NUnit, "NUnit", Testing, "nunit");
        Add(TechSkill.MsTest, "MSTest", Testing, "mstest");
        Add(TechSkill.Moq, "Moq", Testing);
        Add(TechSkill.FluentAssertions, "FluentAssertions", Testing, "fluentassertions");
        Add(TechSkill.Testcontainers, "Testcontainers", Testing);
        Add(TechSkill.Selenium, "Selenium", Testing);
        Add(TechSkill.Playwright, "Playwright", Testing);
        Add(TechSkill.Cypress, "Cypress", Testing);
        Add(TechSkill.Appium, "Appium", Testing);
        Add(TechSkill.Jest, "Jest", Testing);
        Add(TechSkill.Vitest, "Vitest", Testing);
        Add(TechSkill.Postman, "Postman", Testing);
        Add(TechSkill.JMeter, "JMeter", Testing, "jmeter");
        Add(TechSkill.K6, "k6", Testing);
        Add(TechSkill.SonarQube, "SonarQube", Testing, "sonarqube");
        Add(TechSkill.Jira, "Jira", Testing);

        // Architecture
        Add(TechSkill.CleanArchitecture, "Clean Architecture", Architecture, "cleanarchitecture");
        Add(TechSkill.Ddd, "DDD", Architecture);
        Add(TechSkill.Cqrs, "CQRS", Architecture);
        Add(TechSkill.EventSourcing, "Event Sourcing", Architecture, "eventsourcing");
        Add(TechSkill.Microservices, "Microservices", Architecture);
        Add(TechSkill.Monolith, "Monolith", Architecture);
        Add(TechSkill.EventDrivenArchitecture, "Event-Driven Architecture", Architecture, "eventdriven");
        Add(TechSkill.RepositoryPattern, "Repository Pattern", Architecture, "repositorypattern");
        Add(TechSkill.UnitOfWork, "Unit of Work", Architecture, "unitofwork");
        Add(TechSkill.DependencyInjection, "Dependency Injection", Architecture, "dependencyinjection");
        Add(TechSkill.DesignPatterns, "Design Patterns", Architecture, "designpatterns");
        Add(TechSkill.Solid, "SOLID", Architecture);
        Add(TechSkill.CiCd, "CI/CD", Architecture, "cicd");
        Add(TechSkill.Agile, "Agile", Architecture);
        Add(TechSkill.Scrum, "Scrum", Architecture);
        Add(TechSkill.Kanban, "Kanban", Architecture);

        // API
        Add(TechSkill.RestApi, "REST API", Api, "rest", "restful", "restapi");
        Add(TechSkill.GraphQL, "GraphQL", Api, "graphql");
        Add(TechSkill.OpenApiSwagger, "OpenAPI / Swagger", Api, "swagger", "openapi");
        Add(TechSkill.WebSocket, "WebSocket", Api, "websocket");
        Add(TechSkill.Webhooks, "Webhooks", Api);
        Add(TechSkill.Oauth2, "OAuth 2.0", Api, "oauth", "oauth2");
        Add(TechSkill.OpenIdConnect, "OpenID Connect", Api, "oidc", "openidconnect");
        Add(TechSkill.Stripe, "Stripe", Api);
        Add(TechSkill.VnPay, "VNPay", Api, "vnpay");
        Add(TechSkill.Momo, "MoMo", Api);
        Add(TechSkill.Zalo, "Zalo", Api);
        Add(TechSkill.SendGrid, "SendGrid", Api, "sendgrid");

        // Data / AI
        Add(TechSkill.Pandas, "Pandas", DataAi);
        Add(TechSkill.NumPy, "NumPy", DataAi, "numpy");
        Add(TechSkill.Spark, "Spark", DataAi);
        Add(TechSkill.Airflow, "Airflow", DataAi);
        Add(TechSkill.Dbt, "dbt", DataAi);
        Add(TechSkill.PowerBi, "Power BI", DataAi, "powerbi");
        Add(TechSkill.Tableau, "Tableau", DataAi);
        Add(TechSkill.Excel, "Excel", DataAi);
        Add(TechSkill.MachineLearning, "Machine Learning", DataAi, "machinelearning");
        Add(TechSkill.DeepLearning, "Deep Learning", DataAi, "deeplearning");
        Add(TechSkill.TensorFlow, "TensorFlow", DataAi, "tensorflow");
        Add(TechSkill.PyTorch, "PyTorch", DataAi, "pytorch");
        Add(TechSkill.ScikitLearn, "scikit-learn", DataAi, "sklearn", "scikitlearn");
        Add(TechSkill.Opencv, "OpenCV", DataAi, "opencv");
        Add(TechSkill.Nlp, "NLP", DataAi);
        Add(TechSkill.Llm, "LLM", DataAi);
        Add(TechSkill.PromptEngineering, "Prompt Engineering", DataAi, "promptengineering");
        Add(TechSkill.Rag, "RAG", DataAi);
        Add(TechSkill.LangChain, "LangChain", DataAi, "langchain");
        Add(TechSkill.VectorDatabase, "Vector Database", DataAi, "vectordatabase");
        Add(TechSkill.PgVector, "pgvector", DataAi, "pgvector");
        Add(TechSkill.Pinecone, "Pinecone", DataAi);
        Add(TechSkill.OpenAiApi, "OpenAI API", DataAi, "openai", "openaiapi");
        Add(TechSkill.HuggingFace, "Hugging Face", DataAi, "huggingface");

        // Security
        Add(TechSkill.Jwt, "JWT", Security);
        Add(TechSkill.Owasp, "OWASP", Security);
        Add(TechSkill.Encryption, "Encryption", Security);
        Add(TechSkill.Keycloak, "Keycloak", Security);
        Add(TechSkill.IdentityServer, "IdentityServer", Security, "identityserver");
        Add(TechSkill.PenetrationTesting, "Penetration Testing", Security, "pentest", "penetrationtesting");
        Add(TechSkill.Siem, "SIEM", Security);
        Add(TechSkill.Iam, "IAM", Security);

        // Tools
        Add(TechSkill.Git, "Git", Tools);
        Add(TechSkill.GitHub, "GitHub", Tools, "github");
        Add(TechSkill.GitLab, "GitLab", Tools, "gitlab");
        Add(TechSkill.Bitbucket, "Bitbucket", Tools);
        Add(TechSkill.Confluence, "Confluence", Tools);
        Add(TechSkill.Figma, "Figma", Tools);
        Add(TechSkill.Notion, "Notion", Tools);
        Add(TechSkill.VisualStudio, "Visual Studio", Tools, "visualstudio");
        Add(TechSkill.VsCode, "VS Code", Tools, "vscode");
        Labels = Items.Select(i => i.Label).ToList();
    }

    public static IReadOnlyList<TechSkillCatalogItem> All => Items;

    /// <summary>Gán cuối static ctor — field initializer chạy trước khi Add() nên không dùng được.</summary>
    public static IReadOnlyList<string> Labels { get; private set; } = [];

    /// <summary>
    /// Map một tên tự do (JD, RAG, enum) về nhãn canonical. Không khớp enum → null.
    /// Cắt phần sau dấu gạch (vd "C# – OOP") trước khi so.
    /// </summary>
    public static string? TryMap(string? raw)
    {
        var head = Head(raw);
        if (head.Length == 0) return null;

        if (CompactToLabel.TryGetValue(TechSkillMatcher.Compact(head), out var byCompact))
            return byCompact;

        // Fallback token: "React.js" → React, "k8s" → Kubernetes. Ưu tiên khớp dài/chính xác hơn.
        return TechSkillMatcher.MapToAllowed(head, Labels);
    }

    private static void Add(TechSkill skill, string label, string group, params string[] aliases)
    {
        var forms = new List<string> { label, skill.ToString() };
        forms.AddRange(aliases);
        var distinct = forms
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Items.Add(new TechSkillCatalogItem(skill.ToString(), label, group, distinct));

        // Compact của nhãn đăng ký trước — alias không được ghi đè nhãn đã có.
        RegisterCompact(label, label);
        foreach (var form in distinct)
            RegisterCompact(form, label);
    }

    private static void RegisterCompact(string form, string label)
    {
        var key = TechSkillMatcher.Compact(form);
        if (key.Length == 0) return;
        CompactToLabel.TryAdd(key, label);
    }

    private static string Head(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0) return string.Empty;

        // Chỉ cắt mô tả sau gạch dài / " - ", không cắt hyphen trong "Event-Driven" hay "ASP.NET".
        var cut = text.IndexOfAny(['—', '–']);
        if (cut > 0) return text[..cut].Trim();

        var spaced = text.IndexOf(" - ", StringComparison.Ordinal);
        if (spaced > 0) return text[..spaced].Trim();
        return text;
    }
}
