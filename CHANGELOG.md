# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-05-09
### Added
- `GET /api/v1/audit` search endpoint — returns `AuditEntryResponse[]` ordered most-recent-first; accepts exactly one of `actor_id` or `resource_type` as a query parameter; returns 400 with `ProblemDetails` if neither or both are supplied
- `IAuditService.SearchAsync` — routes to the appropriate repository method based on the supplied filter; validates mutual exclusivity of `actor_id` / `resource_type`; uses `IsNullOrWhiteSpace` to treat blank query strings as absent
- `AuditController` — thin controller at `api/v1/audit`; rate-limited via `RateLimitPolicies.Fixed`; delegates all validation to `AuditService`
- Integration tests: `AuditController` — search by actor_id, search by resource_type, empty result set, neither param (400), both params (400), multiple entries ordered newest-first (6 cases)

## [0.2.0] - 2026-05-02
### Added
- `IngestEventRequest` model — `EventId` (optional, for idempotency), required `ActorId`, `ActionType`, `ResourceType`, `ResourceId`, and optional `Before`/`After` (`JsonObject?`) fields
- `AuditEntryResponse` model — all `AuditEntry` fields with `Payload` deserialized as `JsonElement` (structured JSON, not a raw string)
- `DeadLetterEntry` model — `FailedAt`, `Reason`, and original `IngestEventRequest` (used by Slice 5 dead-letter store)
- `Result<T>` — lightweight discriminated union (`IsSuccess` computed from `Error`); `ResultErrorType` enum (`Validation`, `Conflict`, `NotFound`); no library dependency
- `DiffEngine` — computes structured field-level diffs between `before`/`after` `JsonObject` payloads; uses two-pass iteration (no intermediate `HashSet` allocation); unchanged fields excluded; null↔value transitions handled
- `IAuditService` / `AuditService` — orchestrates ingestion: idempotency check → payload computation (diff for updates, full snapshot for create/delete) → persist; logs `Warning` on duplicate event rejection
- `AuditEventsController` — `POST /api/v1/audit/events`; returns 201 Created, 400 Bad Request, or 409 Conflict; rate-limited via `RateLimitPolicies.Fixed`
- `StubAuditRepository` backing store changed from `List<AuditEntry>` to `Dictionary<Guid, AuditEntry>` — O(1) ID lookup for `FindByIdAsync` and `TryAdd` idempotency check
- Unit tests: `DiffEngine` — changed field, unchanged excluded, null→value, value→null, empty diff, multiple changes, null-guard throws (8 cases)
- Integration tests: `AuditEventsController` — create/update/delete happy paths, missing required field, both-before-and-after-null, duplicate EventId 409, auto-assigned ID (7 cases)

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
