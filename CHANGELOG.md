# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.5.0] - 2026-05-27
### Added
- `IDeadLetterStore` / `DeadLetterStore` — append-only NDJSON store for events that fail to persist; one line per `DeadLetterEntry`; `SemaphoreSlim(1,1)` serialises concurrent appends; file path configured via `Storage:DeadLetterFilePath`
- Named resilience pipeline `audit-save` (`Microsoft.Extensions.Resilience` 8.10.0) wrapping `IAuditRepository.SaveAsync` — `MaxRetryAttempts = 1`, 200ms constant delay (two total attempts, satisfying FR-8 "retry at least once")
- `ResiliencePipelines.AuditSave` constant (mirrors `RateLimitPolicies`) — no magic strings
- Unit tests: `AuditServiceResiliencyTests` — retry succeeds on second attempt (no dead-letter), retry exhaustion routes to dead-letter and returns `ServiceUnavailable` (2 cases)
- Integration test: `AuditResiliencyIntegrationTests` — `FailingStoreWebApplicationFactory` swaps `IAuditRepository` for an always-throwing fake and points `DeadLetterStore` at a temp file; verifies `POST` returns 503 and the dead-letter file contains the event

### Changed
- `AuditService` — `SaveAsync` is now executed inside the named resilience pipeline; on retry exhaustion the request is captured in the dead-letter store and a `ServiceUnavailable` result is returned. `OperationCanceledException` is re-thrown rather than dead-lettered so caller cancellation is not treated as a store failure; the dead-letter write uses `CancellationToken.None` so the durability backstop completes even if the caller's token cancels mid-flight. `DeadLetterEntry.Reason` records `"{ExceptionType}: {Message}"` for triage
- `ResultErrorType` — adds `ServiceUnavailable`
- `AuditEventsController` — maps `ResultErrorType.ServiceUnavailable` → `503 Service Unavailable` with sanitised `ProblemDetails`; the underlying exception message is not exposed to the caller
- `Program.cs` — registers `IDeadLetterStore` as a singleton and adds the `audit-save` resilience pipeline
- `docs/decisions.md` — ADR-020 (retry orchestration in `AuditService`, no separate `ResiliencyPolicy` class), ADR-021 (`MaxRetryAttempts = 1` — minimal "retry at least once"; dead-letter is the durability backstop), ADR-022 (cancellation semantics: re-throw `OperationCanceledException`; dead-letter write uses `CancellationToken.None`)
- `README.md` — documents the retry / dead-letter behaviour and the new 503 response

## [0.4.0] - 2026-05-16
### Added
- `AuditRepository` — file-backed implementation of `IAuditRepository`; appends one NDJSON line per entry to the path configured at `Storage:AuditFilePath`; reads load and filter the full file in memory; `SemaphoreSlim(1,1)` serialises all concurrent reads and writes including the file-existence check
- `LogPseudonymizer` — HMAC-SHA256 service that maps PII fields (`ActorId`, `ResourceId`) to a stable 16-character hex pseudonym for operational logs; same input always produces the same pseudonym enabling log correlation; requires `Logging:PseudonymKey` to be set at startup (throws `InvalidOperationException` if absent)
- Integration tests: `AuditRepositoryIntegrationTests` — repeats all Slice 2–3 scenarios against the real file-backed store via `FileBackedWebApplicationFactory`; temp file is created and deleted per test class; covers create, update, duplicate 409, physical file persistence, search by actor, search by resource type, empty results, reverse-chronological order, and 400 validation cases (10 cases)
- `appsettings.example.json` — documents the required `Logging:PseudonymKey` field

### Changed
- `AuditService` — `ActorId` and `ResourceId` are now pseudonymized in all log statements via `LogPseudonymizer`; raw PII no longer appears in operational logs
- `Program.cs` — registers `AuditRepository` as the live singleton when `Features:UseStubRepository` is `false`; registers `LogPseudonymizer` as a singleton
- `docs/decisions.md` — ADR-018 (WebApplicationFactory `ConfigureServices` vs `ConfigureAppConfiguration`), ADR-019 (HMAC pseudonymization for log PII)
- `README.md` — documents `Logging:PseudonymKey` configuration, production override, and key-rotation behaviour

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
