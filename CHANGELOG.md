# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-04-26
### Added
- Solution file and dual-project layout (`TransactionalAuditLog` API + `TransactionalAuditLog.Tests`)
- `CorrelationIdMiddleware` — assigns or forwards `X-Correlation-ID` header; enriches every request's log scope with correlation ID, machine name, environment, and app version
- `GlobalExceptionHandler` (`IExceptionHandler`) — centralised unhandled exception handler returning sanitised `ProblemDetails` (RFC 7807); no internal state leaks
- `IAuditRepository` interface with `FindByIdAsync`, `SaveAsync`, `SearchByActorAsync`, and `SearchByResourceTypeAsync`
- `StubAuditRepository` — thread-safe singleton in-memory implementation; duplicate-ID writes are idempotent no-ops
- `FeatureFlags` typed options bound from `Features` config section; `UseStubRepository` flag controls repository registration via DI
- `/health` endpoint via `Microsoft.Extensions.Diagnostics.HealthChecks`
- OpenAPI/Swagger with XML doc comment support (`GenerateDocumentationFile` enabled)
- Explicit CORS policy; origins read from `Cors:AllowedOrigins` in configuration
- Fixed-window rate limiter (100 req / 10 s) via `AddRateLimiter`; policy name held in `RateLimitPolicies.Fixed` constant
- `appsettings.json` with `Features`, `Cors`, and `Storage` sections
- `appsettings.example.json` as a committed, secret-free configuration reference
- `.gitignore` additions: Rider (`.idea/`), runtime data files (`audit_store.json`, `dead_letter_events.json`)
- Integration test: `GetHealth_WhenAppIsRunning_ReturnsHealthy` via `WebApplicationFactory<Program>`
- Project documentation: `docs/spec.md`, `docs/design.md`, `docs/decisions.md` (ADR-001 – ADR-012)
