# Interview Plan Studio Backend

## Mục tiêu
- Bổ sung module `Studio` cho luồng tạo interview plan theo revision.
- Dùng lại Auth/JWT, `AppDbContext`, middleware, và pattern service hiện có.

## Thành phần chính
- Domain: `DomainLayer/Studio` (entities, enums, `StudioBusinessException`).
- Persistence: DbSet + cấu hình EF tại `InfrastructureLayer/Configurations/Studio`.
- Application/API: contracts + interfaces + controllers Studio (`api/studio/*`).
- Streaming: SSE endpoint `POST /api/studio/projects/{projectId}/chat/messages`.
- Logging/Validation: Serilog + FluentValidation.

## Migration
- Migration đã tạo: `AddInterviewPlanStudioCoreFlow`.

## Chạy local
1. `docker compose up -d postgres`
2. `dotnet ef database update --project InfrastructureLayer --startup-project WebAPI`
3. `dotnet run --project WebAPI`

## Test
- Unit: `dotnet test ApplicationLayer.Tests/ApplicationLayer.Tests.csproj`
- Integration: `dotnet test WebAPI.IntegrationTests/WebAPI.IntegrationTests.csproj`
