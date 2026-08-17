# Class / package diagrams

Editable [diagrams.net](https://app.diagrams.net) (`.drawio`) sources, redrawn from the
actual code as of **2026-08-17** (open with the draw.io desktop app, app.diagrams.net,
or the *Draw.io Integration* VS Code extension).

| File | Covers |
|---|---|
| `authentication-class-diagram.drawio` | `AuthController` → `IAuthService`/`AuthService` → `IUserRepository`/`UserRepository` → `User`, plus the Auth DTOs and the other services/repositories `AuthService` depends on. |
| `hr-company-management-class-diagram.drawio` | `CompanyController` → `ICompanyService`/`CompanyService` → `ICompanyRepository`/`CompanyRepository` & `IHRProfileRepository`/`HRProfileRepository` → `Company`/`HRProfile`, plus the Company DTOs. |
| `backend-package-diagram.drawio` | Solution-level architecture: the 4 projects (`WebAPI`, `ApplicationLayer`, `InfrastructureLayer`, `DomainLayer`), their real top-level folders, and the `ProjectReference` dependencies between them. |

## Notes on this redraw

The first two diagrams replace an earlier version whose classes/methods no longer matched
the code (e.g. the old `CompanyController` had `GetMyCompany`/`VerifyCompany`; the current
one has `Search`/`GetById`/`Create`/`CreateBulk`/`Update`/`Delete`). Content was re-derived
directly from `WebAPI/Controllers`, `ApplicationLayer/Interfaces`, `ApplicationLayer/Services`,
`InfrastructureLayer/Repository` and `DomainLayer/Entities`.

The package diagram replaces the old single-project MVC-style folder layout with the
solution's real Clean-Architecture shape (4 separate `.csproj`s), derived from each
project's `<ProjectReference>` entries.

Visual conventions (kept consistent across all three):

- **Blue** = controller, **purple** = interface, **green** = service impl.,
  **orange** = repository impl. / infrastructure, **gray** = domain entity,
  **yellow** = DTO.
- Dashed open arrow = dependency/uses; dashed hollow-triangle = realizes
  (implements an interface); solid hollow-triangle = generalizes (extends);
  solid open arrow = association.
- Very large member/DTO lists were grouped into labelled dashed containers
  instead of drawing one arrow per class, to keep the diagrams readable.
