namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// Danh mục tech skill chuẩn cho focus area Studio.
/// Giá trị lưu DB là nhãn hiển thị (TechSkillCatalog.Label), không phải tên enum.
/// </summary>
public enum TechSkill
{
    // ===== Languages =====
    CSharp, Java, Kotlin, Python, JavaScript, TypeScript, Go, Rust, PHP, Ruby,
    Swift, Dart, Cpp, C, Scala, SQL, Bash, PowerShell, R, Lua,

    // ===== Backend =====
    DotNet, AspNetCore, AspNetMvc, WebApi, SignalR, Blazor, Grpc, MinimalApi,
    SpringBoot, Hibernate, NodeJs, ExpressJs, NestJs, Django, FastApi, Flask,
    Laravel, RubyOnRails, Wpf, WinForms,

    // ===== Frontend =====
    Html, Css, Sass, TailwindCss, Bootstrap, MaterialUI, AntDesign, ShadcnUI,
    React, NextJs, Vue, Nuxt, Angular, Svelte, jQuery,
    Redux, Zustand, ReactQuery, ReactHookForm, Vite, Webpack, Storybook,

    // ===== Mobile / Desktop / Game / Embedded =====
    ReactNative, Flutter, AndroidNative, IosNative, Xamarin, Maui, Ionic,
    Electron, Unity, UnrealEngine, Godot, Arduino, RaspberryPi, Rtos,

    // ===== Database & ORM =====
    SqlServer, PostgreSql, MySql, Oracle, Sqlite, MariaDb, MongoDb, Cassandra,
    DynamoDb, CosmosDb, Neo4j, ClickHouse,
    EntityFrameworkCore, Dapper, AdoNet, Prisma, TypeOrm, Sequelize, Flyway, Liquibase,

    // ===== Cache / Search / Messaging =====
    Redis, Memcached, Elasticsearch, OpenSearch, Solr,
    RabbitMq, Kafka, AzureServiceBus, AwsSqs, MassTransit, NServiceBus,
    Hangfire, Quartz, MediatR,

    // ===== Cloud =====
    Aws, Azure, Gcp, AwsLambda, AzureFunctions, S3, AzureBlobStorage,
    Cloudflare, Vercel, Heroku, Firebase, Supabase,

    // ===== DevOps / Infra / Monitoring =====
    Docker, Kubernetes, Terraform, Ansible, Nginx, Linux, Helm,
    GitHubActions, GitLabCi, Jenkins, AzureDevOps, ArgoCd,
    Prometheus, Grafana, Elk, Serilog, OpenTelemetry, Sentry, Datadog,
    Networking, LoadBalancing,

    // ===== Testing & QA =====
    UnitTesting, IntegrationTesting, TestAutomation, TestDrivenDevelopment,
    XUnit, NUnit, MsTest, Moq, FluentAssertions, Testcontainers,
    Selenium, Playwright, Cypress, Appium, Jest, Vitest,
    Postman, JMeter, K6, SonarQube, Jira,

    // ===== Architecture & Practices =====
    CleanArchitecture, Ddd, Cqrs, EventSourcing, Microservices, Monolith,
    EventDrivenArchitecture, RepositoryPattern, UnitOfWork, DependencyInjection,
    DesignPatterns, Solid, CiCd, Agile, Scrum, Kanban,

    // ===== API & Integration =====
    RestApi, GraphQL, OpenApiSwagger, WebSocket, Webhooks, Oauth2, OpenIdConnect,
    Stripe, VnPay, Momo, Zalo, SendGrid,

    // ===== Data / AI / ML =====
    Pandas, NumPy, Spark, Airflow, Dbt, PowerBi, Tableau, Excel,
    MachineLearning, DeepLearning, TensorFlow, PyTorch, ScikitLearn, Opencv,
    Nlp, Llm, PromptEngineering, Rag, LangChain, VectorDatabase, PgVector,
    Pinecone, OpenAiApi, HuggingFace,

    // ===== Security =====
    Jwt, Owasp, Encryption, Keycloak, IdentityServer, PenetrationTesting,
    Siem, Iam,

    // ===== Tools =====
    Git, GitHub, GitLab, Bitbucket, Confluence, Figma, Notion, VisualStudio, VsCode
}
