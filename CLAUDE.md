# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
dotnet restore                          # Restore NuGet packages
dotnet build                            # Build all projects
dotnet run --project SaintHenriBasketball.API  # Run the API
```

**EF Core Migrations:**
```bash
dotnet ef migrations add MigrationName --project SaintHenriBasketball.Infrastructure --startup-project SaintHenriBasketball.API
dotnet ef database update --project SaintHenriBasketball.Infrastructure --startup-project SaintHenriBasketball.API
```

**Secrets (dev):**
```bash
cd SaintHenriBasketball.API
dotnet user-secrets set "Resend:ApiKey" "..."
dotnet user-secrets set "JwtSettings:Key" "..."
```

## Architecture (Clean Architecture)

Four layers with unidirectional dependencies: API → Application → Infrastructure → Domain.

- **Domain** — Zero external dependencies. Entities (`ApplicationUser`, `Session`, `SessionRegistration`, `SessionAttendance`, `Season`, `SeasonRegistration`, `SeasonSubscription`, `Payment`), enums, and repository interfaces (`Interfaces/Repositories/`). All entities inherit from `BaseEntity` (Guid Id, CreatedOn, ModifiedOn).
- **Application** — Service interfaces and implementations (no CQRS/MediatR). DTOs organized by feature. AutoMapper for entity-DTO mapping (`Mapping/MappingProfile.cs`). FluentValidation for input validation. Email sending via Resend with retry + Hangfire background fallback.
- **Infrastructure** — EF Core with SQL Server (`ApplicationDbContext`). Generic repository pattern plus specialized repositories. Hangfire jobs for recurring tasks (attendance reminders, payment reminders, capacity checks). Migrations auto-applied on startup.
- **API** — Controllers inherit `BaseApiController`. JWT Bearer auth. API versioning via URL segment (`api/v{version}/[controller]`), header (`X-Api-Version`), or query string. Swagger at `/swagger`. Hangfire dashboard at `/hangfire`. CORS allows all origins.

### Key Services

`IUserService`, `ISessionService`, `IRegistrationService`, `IEmailService`, `IAttendanceService`, `IPaymentService`, `ISeasonService`, `IEmailAutomationService`, `ICacheService` — each registered as scoped in `Extensions/DependencyInjection.cs` per layer.

## Deployment

Azure Web App via GitHub Actions (`master` branch → build → deploy). Secrets in Azure Key Vault.

## Domain Context

Basketball league management API for Saint-Henri (Montreal). Core workflows: user registration, session scheduling/registration, attendance tracking, payment processing (drop-in vs season plans), and email notifications (bilingual FR/EN).
