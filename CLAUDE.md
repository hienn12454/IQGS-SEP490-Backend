# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

IQGS – AI-Powered Interview Question Generation System using RAG and LLM. A dual-sided platform: HR generates AI-powered interview question sets from a job description, publishes them to a marketplace, and candidates practice against them with AI-scored feedback.

## Commands

```bash
# Build
dotnet build IQGS-BE.sln

# Run the API (Swagger opens automatically at /swagger)
cd WebAPI && dotnet run

# EF Core migrations — dotnet-ef must be installed once: dotnet tool install --global dotnet-ef
# Must be run from InfrastructureLayer (WebAPI is not the "startup project" for EF purposes,
# but AppDbContextFactory.cs reads connection string from WebAPI/appsettings.json at design time)
cd InfrastructureLayer
dotnet ef migrations add <Name>
dotnet ef database update
```

There is no test project in the solution (`IQGS-BE.sln` only contains WebAPI, ApplicationLayer, InfrastructureLayer, DomainLayer) — there is no `dotnet test` command to run. When asked to verify behavior, validate via `dotnet build`, and if needed check EF query translation with `.ToQueryString()` in a scratch console app referencing the layers, rather than assuming a test suite exists.

## Architecture

Clean Architecture, 4 projects, strict one-way dependency:

```
DomainLayer          — Entities, Constants, Exceptions. No project references at all.
       ^
ApplicationLayer      — DTOs, Interfaces/{Repositories,Services}, Services (business logic), Helpers, Settings.
       ^                References DomainLayer only. This is where business rules live.
InfrastructureLayer   — Repository implementations, AppDbContext (EF Core), External/ (RAG HTTP client),
       ^                Jobs/ (Hangfire), Migrations/. References Domain + Application.
WebAPI               — Controllers, Program.cs (composition root / DI registration), Middleware,
                        Extensions, ResponseCode, appsettings.json. References Application + Infrastructure.
```

Controllers are grouped by actor under `WebAPI/Controllers/{Admin,Hr,Candidate,Internal}/`. A feature almost always touches all 4 layers: Entity (Domain) → DTO + Interface + Service (Application) → Repository impl (Infrastructure) → Controller (WebAPI) → registered in `Program.cs`.

### Repository pattern

`IBaseRepository<T>` (Domain entity constrained) gives `GetByIdAsync/GetAllAsync/AddAsync/UpdateAsync/ExistsAsync` plus `DeleteAsync`, which is a **soft delete** (`IsActive = false`), implemented once in `InfrastructureLayer/Repository/BaseRepository.cs`. Never hard-delete rows; every entity extends `BaseEntity` (`Id`, `CreatedAt`, `UpdatedAt`, `IsActive`).

### Errors — all messages are in Vietnamese

Custom exceptions in `DomainLayer/Exceptions/` (`BadRequestException`, `NotFoundException`, `ConflictException`, `ForbiddenException`, `UnauthorizedException`, `StructuredHttpException`) all extend `BaseHttpException` and carry an HTTP status. `WebAPI/Middleware/ExceptionHandlingMiddleware` (registered first in the pipeline) catches them and writes `{ code, error }`; anything unhandled becomes a generic 500 with a Vietnamese fallback message, never leaking internals outside Development. **Every user-facing exception message and every `[Required]/[MaxLength]/[Range]/...ErrorMessage` on a DTO must be in Vietnamese** — this is an established, strictly-followed convention across the whole codebase, not optional.

Controllers return `SuccessResp.Ok/Created/Accepted/NoContent(...)` (`ApplicationLayer/ResponseCode/`), which wraps payloads as `{ data, code, message }`.

### Auth

JWT bearer. The user id lives in the `"sub"` claim (`MapInboundClaims = false`, so it's *not* remapped to `ClaimTypes.NameIdentifier`). Use the `User.GetUserId()` extension (`WebAPI/Extensions/`) to read it — it throws `UnauthorizedException` (Vietnamese) if missing/malformed. Roles are `Admin | HR | Candidate` (`DomainLayer/Constants/UserRole.cs`, fixed RoleIds 1/2/3, seeded via `HasData`). Gate endpoints with `[Authorize(Roles = "...")]`.

### Database

PostgreSQL via Npgsql + the `pgvector` extension (for `KnowledgeChunk` embeddings used by RAG). `NpgsqlDataSourceBuilder.UseVector()` must be called both in `WebAPI/Program.cs` and in `InfrastructureLayer/Database/AppDbContextFactory.cs` (the EF Core *design-time* factory — needed because `InfrastructureLayer` isn't the startup project; it locates the connection string by reading `../WebAPI/appsettings.json`).

Table naming is inconsistent by history: newer feature tables use explicit `snake_case` via `.ToTable(...)` (`question_sets`, `practice_sessions`, `candidate_recommendations`, `candidate_invitations`, `platform_settings`, `ai_feedbacks`, `knowledge_documents`, `knowledge_chunks`), while older core tables use EF Core's default PascalCase (`Users`, `Companies`, `HRProfiles`, `CandidateProfiles`). When adding a new table, match the snake_case convention (that's where all recent work has been).

**Singleton-row settings table**: `PlatformSettings` (fixed `SingletonId` GUID, seeded via `HasData`) holds runtime-tunable config (e.g. `MinQuestionsToPublish`) that Admin can change via API instead of editing `appsettings.json` + redeploying. Follow this pattern for other admin-tunable knobs.

**Never edit an already-generated migration that may have been applied** — always add a new one. Pure data fixes (no schema change) are valid migrations too: use `migrationBuilder.Sql("...")` with a raw `UPDATE`, no `AlterColumn`/`CreateTable` needed.

### Background jobs

Hangfire with PostgreSQL storage, queues `knowledge-ingestion`, `question-generation`, `default`. Recurring jobs (watchdogs for stuck generation jobs, expired practice sessions, etc.) are registered with `RecurringJob.AddOrUpdate<TJob>(...)` in `Program.cs`.

### External RAG service

A separate microservice (**not in this repo**), called through `IRagService` / `InfrastructureLayer/External/RagService.cs` (a typed `HttpClient`), endpoints like `/internal/rag/parse-jd`, `/internal/rag/parse-cv`, `/internal/rag/evaluate-answer`, `/internal/rag/generate-plan`, authenticated with an `X-Internal-Api-Key` header. Connection/timeout failures are wrapped into descriptive Vietnamese exceptions (`CreateRagUnavailableException` / `BuildRagExceptionAsync`) — reuse that pattern for any new RAG endpoint. Because it's an external contract, extend result DTOs (`ApplicationLayer/DTOs/Rag/RagDtos.cs`) with new **nullable** fields when the RAG side gains capabilities, so older RAG responses still deserialize safely (all-null) instead of breaking.

### Company logo

`CompanyLogoResolver` (`ApplicationLayer/Services/Mapping/`) guarantees every response is never missing a logo: real uploaded logo → favicon-by-website-domain → auto-generated SVG initials, deterministic color per company name. **Any response DTO that includes a company name must include a company logo resolved through this helper** — this rule has been applied retroactively across marketplace, HR's own dashboards, invitations, and the raw Company CRUD endpoints; keep it true for new ones too. `IHrCompanyInfoService` centralizes "look up an HR user's own company name + logo via `HRProfile.CompanyId`" — reuse it rather than re-deriving that join.

### Scoring conventions — two different scales, don't confuse them

- `PracticeSession.OverallScore` (0–100 scale): `PracticeOverallScoreCalculator.Compute` = sum of per-answer AI scores (only ones with `Succeeded` evaluation) ÷ total questions in the set, rounded to **2 decimals**.
- Per-answer AI score (`AiFeedback.Score`, 0–100 scale): rounded to a **whole number**.
- Marketplace star `Rating` (0–5 scale, shown to candidates): derived, `PublishedQuestionSetMapper.RoundRating` = `OverallScore average ÷ 20`, clamped to `[0,5]`, rounded to **1 decimal**. Never treat this the same as `OverallScore`.

### Recommendation feature (rule-based, not AI matching)

`RecommendationService.GenerateForCompletedSessionAsync` creates a `CandidateRecommendation` only when a practice session completes **and**: `OverallScore >= 70` (`MinScoreForRecommendation`) **and** `CandidateProfile.AllowRecruiterRecommendation == true` (defaults to `true`) **and** the question set is `PUBLISHED`. HR can shortlist/dismiss/invite from `GET /api/hr/recommendations`; inviting creates a `CandidateInvitation` (1:1 with the recommendation, enforced both by status check and by checking the invitation table directly to avoid a unique-index race). Toggling consent off does **not** retroactively hide already-created recommendations — it only gates whether future sessions generate new ones.

### Swagger / XML docs

`GenerateDocumentationFile` + `IncludeXmlComments` in `Program.cs` pulls in `WebAPI.xml` and `ApplicationLayer.xml`, so `/// <summary>` comments on controllers, actions, and DTOs are live API documentation shown in Swagger UI — write them in Vietnamese, matching the existing style, not as throwaway code comments.

## Keeping this file current

When you add a new architectural pattern, a new cross-cutting convention (not just a one-off endpoint), or change one of the conventions above, update the relevant section here in the same commit/session. Don't log every feature added — only what a future session would otherwise have to rediscover by reading multiple files.
