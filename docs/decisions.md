# Decision Log

> Append an entry here for every non-obvious technical decision made during 
> design or implementation. If there was a real alternative, it belongs here.
> This file is read by reviewers to understand your thinking and tradeoffs.

## Format

### [ADR-NNN] Short title
**Decision:** What was chosen
**Alternatives considered:** What else was evaluated
**Rationale:** Why this choice was made
**Tradeoffs:** What you give up with this choice

---

## Decisions

### [ADR-001] Language and framework selection
**Decision:** C# with ASP.NET Core Web API on .NET 8
**Alternatives considered:** Python/FastAPI, TypeScript/Node
**Rationale:** Strongest proficiency — produces cleanest code under time pressure. .NET 8 is LTS and has excellent built-in DI, routing, and test tooling.
**Tradeoffs:** Heavier startup scaffolding than Python/Node, but tooling and type safety pay off in maintainability.

### [ADR-002] Controllers over Minimal API
**Decision:** Use MVC Controllers rather than Minimal API
**Alternatives considered:** Minimal API (.NET 6+)
**Rationale:** Controllers provide a familiar, structured pattern that keeps focus on solving the problem domain. Minimal API would shift attention toward demonstrating framework knowledge rather than solution quality.
**Tradeoffs:** Slightly more boilerplate than Minimal API, but clearer separation of concerns and more readable routing structure.

### [ADR-003] Resiliency via Microsoft.Extensions.Resilience
**Decision:** Use Microsoft.Extensions.Resilience for retry, circuit breaker, and timeout policies
**Alternatives considered:** Polly directly, manual retry logic
**Rationale:** Microsoft.Extensions.Resilience is the .NET 8 idiomatic wrapper over Polly — integrates cleanly with IHttpClientFactory and the DI pipeline without additional abstraction
**Tradeoffs:** Adds a dependency — only applied to external HTTP calls and database operations where transient failures are realistic

### [ADR-004] Stub repositories for incremental slice delivery
**Decision:** Use stub repository implementations behind feature flags to keep API slices independently mergeable before the DB layer exists
**Alternatives considered:** Feature flags alone (hiding endpoints), building DB layer first before any API work
**Rationale:** Coding to IRepository interfaces allows the API and service layers to be built, tested, and merged independently of the data layer. Stubs are swapped via DI — no stub logic leaks into business logic. This demonstrates interface-driven design and allows reviewers to see a working API at every point in the git history.
**Tradeoffs:** Requires disciplined cleanup — every stub must be replaced before 1.0.0. Adds a small amount of scaffolding upfront.

### [ADR-005] Test runner selection
**Decision:** xUnit
**Alternatives considered:** NUnit, MSTest
**Rationale:** xUnit is the de facto standard for modern .NET projects. First-class support in `dotnet test`, clean syntax, good parallelism defaults.
**Tradeoffs:** None significant at this scale.

### [ADR-006] JSON file store for persistence
**Decision:** Use a newline-delimited JSON file (`audit_store.json`) as the Audit Store, read/written via `System.Text.Json`.
**Alternatives considered:** SQLite via EF Core; keeping the in-memory stub as the final store.
**Rationale:** The spec explicitly permits a file-based store. `System.Text.Json` is already present in the ASP.NET Core host — no new packages, migrations, or server are required. SQLite would add EF Core, a schema migration, and a DbContext for no benefit justified by the requirements.
**Tradeoffs:** Reads load the entire file into memory and filter in-process. Acceptable at this scope; would not scale to high volumes without an indexed store.

### [ADR-007] Payload stored as a serialised JSON string
**Decision:** Serialise the audit entry `Payload` as a JSON string within each file record.
**Alternatives considered:** Strongly-typed payload classes per action type; a separate nested structure for diff fields.
**Rationale:** The two search filters (`actor_id`, `resource_type`) operate on other fields — payload contents are never queried. A serialised string keeps the file record flat and the store layer simple.
**Tradeoffs:** Payload is opaque to the store layer. Any future requirement to query by payload contents would require deserialising every record.

### [ADR-008] ResourceId added beyond the spec field list
**Decision:** Include `ResourceId` as an explicit field on `AuditEntry`, beyond the five fields listed in FR-2.
**Alternatives considered:** Omit it and store only what FR-2 mandates.
**Rationale:** An audit entry without a resource identifier is not actionable — knowing "a Patient was updated" without knowing *which* patient has no investigative value. This is a deliberate, minimal addition beyond the spec.
**Tradeoffs:** Adds scope beyond what is strictly required; noted explicitly so reviewers do not mistake it for a misread requirement.

### [ADR-009] Client-supplied EventId for idempotency
**Decision:** Accept an optional `EventId` (Guid) on `POST /api/v1/audit/events`; use it as the entry `Id` if supplied, auto-generate otherwise.
**Alternatives considered:** Auto-generate all IDs server-side; exact-match deduplication on all fields.
**Rationale:** `POST` endpoints must be idempotent per project conventions. A caller-supplied UUID is the standard pattern — if the same `EventId` is submitted twice the server returns the existing entry unchanged, making retries safe.
**Tradeoffs:** Callers must generate and track UUIDs to get idempotency guarantees. Without `EventId`, duplicate posts create duplicate entries.

### [ADR-010] DiffEngine as a standalone service
**Decision:** Implement diffing logic in a dedicated `DiffEngine` class injected into `AuditService`.
**Alternatives considered:** Inline diffing inside `AuditService`.
**Rationale:** Isolating the diff logic — including null/empty transition handling — allows it to be unit-tested independently without standing up the full service graph.
**Tradeoffs:** One additional class; negligible overhead at this scale.

### [ADR-011] Dead letter store as a local append-only JSON file
**Decision:** Persist dead-letter events to a local append-only newline-delimited JSON file (`dead_letter_events.json`).
**Alternatives considered:** In-memory queue; external message queue (e.g. RabbitMQ, Azure Service Bus).
**Rationale:** Simplest durable local storage with no additional packages or infrastructure. Append-only writes avoid read-modify-write races under concurrent access. Matches the spec's requirement for "durable local storage".
**Tradeoffs:** No built-in replay mechanism — recovery requires manual inspection and resubmission. An external queue would provide replay and visibility out of the box but adds infrastructure dependency not warranted by the spec.

### [ADR-013] StubAuditRepository registered as Singleton
**Decision:** Register `StubAuditRepository` with `AddSingleton` rather than `AddScoped`.
**Alternatives considered:** `AddScoped` (the typical lifetime for repository registrations).
**Rationale:** `AddScoped` creates a new instance per HTTP request. In integration tests that share a `WebApplicationFactory`, each request would receive a fresh empty list — a POST followed by a GET would always return an empty result, making cross-request tests impossible to write correctly. `AddSingleton` preserves in-memory state across the lifetime of the test host, which is exactly what a stub repository needs to do.
**Tradeoffs:** `AddScoped` is the correct lifetime for real repositories (unit-of-work semantics). The singleton registration is intentional and specific to the stub — it must be revisited when the stub is replaced in Slice 4.

### [ADR-014] AuditEntryResponse.Payload typed as JsonElement
**Decision:** Type the `Payload` field on `AuditEntryResponse` as `System.Text.Json.JsonElement` rather than `object` or `string`.
**Alternatives considered:** `object` (as described in `design.md`); keeping it as `string` (matching the domain model).
**Rationale:** `object` serializes inconsistently under System.Text.Json — a deserialized JSON object round-trips as `JsonElement` anyway, but typed as `object` it can silently serialize as `{}` depending on the serializer configuration. `JsonElement` is the correct concrete type for a pre-parsed JSON value that must serialize faithfully as structured JSON (not a quoted string) in the response body.
**Tradeoffs:** `JsonElement` is tied to System.Text.Json; switching serializers would require changing the response model. Acceptable given the project already commits to System.Text.Json throughout.

### [ADR-015] 409 Conflict on duplicate EventId rather than returning the original entry
**Decision:** Return `409 Conflict` when a `POST /api/v1/audit/events` request supplies an `EventId` that already exists.
**Alternatives considered:** Return `200 OK` or `201 Created` with the original entry (true idempotency — the spec description says "returns the original entry without creating a duplicate").
**Rationale:** The spec description and the status table in `design.md` are inconsistent. Returning the original entry silently would hide the fact that the caller submitted a duplicate — the caller would see 201 and believe the event was newly ingested. 409 makes the duplicate explicit and actionable, while still preventing double-writes. The "idempotent" guarantee (no duplicate entry created) is preserved regardless of which status is returned.
**Tradeoffs:** Strict idempotency (same input → same output including status) would return 201 on retries. 409 requires callers to distinguish "first submission" from "retry", which is reasonable for an audit system where distinguishing new events from duplicates has investigative value.

### [ADR-016] 200 OK with empty array when search returns no results
**Decision:** Return `200 OK` with an empty JSON array `[]` when a valid search query matches no audit entries.
**Alternatives considered:** `404 Not Found` when no entries match the filter.
**Rationale:** The endpoint operates on a collection, not a single resource. A 404 would imply the collection itself does not exist, which is misleading — the collection exists and is valid, it simply contains no matching entries. `200 []` is the standard REST semantics for an empty collection result and is consistent with how clients consuming paginated or filtered APIs expect to detect "no data" without treating it as an error.
**Tradeoffs:** Callers cannot distinguish "the actor_id is valid but has no events yet" from "the actor_id has never been used" — both return 200 []. For an audit log this distinction is not operationally meaningful.

### [ADR-017] IsNullOrWhiteSpace used to determine query parameter presence
**Decision:** Use `string.IsNullOrWhiteSpace` rather than `!= null` to determine whether `actor_id` or `resource_type` was meaningfully supplied.
**Alternatives considered:** Null-check only (`!= null`), which would allow whitespace-only strings through as valid filter values.
**Rationale:** A query string like `?actor_id=` or `?actor_id=   ` produces a non-null but semantically empty string after binding. Allowing whitespace as a valid filter value would silently return no results rather than returning a 400, making the error invisible to the caller. `IsNullOrWhiteSpace` treats blank values as absent, consistent with the intent of "exactly one parameter must be supplied."
**Tradeoffs:** A filter value that is genuinely a whitespace string (unlikely for actor IDs or resource types) would be rejected. Acceptable given the domain.

### [ADR-018] WebApplicationFactory uses ConfigureServices to swap IAuditRepository, not ConfigureAppConfiguration
**Decision:** In `FileBackedWebApplicationFactory`, replace the `IAuditRepository` DI registration directly via `ConfigureServices` rather than overriding `Features:UseStubRepository` via `ConfigureAppConfiguration`.
**Alternatives considered:** `builder.ConfigureAppConfiguration` to add an in-memory config source that sets `UseStubRepository=false` and `Storage:AuditFilePath`.
**Rationale:** `WebApplicationFactory.ConfigureWebHost` calls `ConfigureAppConfiguration` callbacks *before* the app's default configuration sources (appsettings.json) are added by `WebApplication.CreateBuilder`. This means the in-memory override is added at a lower priority position and appsettings.json wins — so `UseStubRepository` stays `true` and the stub is registered. `ConfigureServices` runs *after* `Program.cs` has called `builder.Build()`, so it can reliably find and replace the already-registered `IAuditRepository` binding regardless of config source ordering.
**Tradeoffs:** The factory bypasses the feature-flag path entirely and directly wires `AuditRepository` with a dedicated `IConfiguration` scoped to the temp file. This is slightly more coupling to the implementation type, but it is test-only code and the direct wiring is more explicit about what is being tested.

### [ADR-019] HMAC-SHA256 pseudonymization for PII fields in operational logs
**Decision:** Log `ActorId` and `ResourceId` as HMAC-SHA256 pseudonyms rather than raw values. The HMAC key is operator-supplied via `Logging:PseudonymKey` (environment variable `Logging__PseudonymKey` in production). The first 8 bytes (16 hex chars) of the digest are logged.
**Alternatives considered:** (1) Split by log level — PII fields at Debug, non-PII at Information; (2) plain SHA-256 with no key; (3) omit PII fields entirely and rely solely on EventId for cross-referencing.
**Rationale:** `ActorId` and `ResourceId` can contain email addresses, usernames, and patient identifiers — PII in a medical context. A keyed HMAC produces a stable pseudonym: the same input always maps to the same output, so log entries for the same actor can be correlated across requests and restarts without exposing the raw value. A secret key defeats rainbow table attacks against the known value space; plain SHA-256 would not. Splitting by log level (option 1) would make PII completely invisible in production, losing all actor-level observability. Omitting the fields (option 3) requires an audit store lookup for every investigation.
**Tradeoffs:** Key rotation breaks cross-session log correlation — old and new entries for the same actor produce different pseudonyms. The key must therefore be treated as long-lived rather than rotated frequently. If the key is absent at startup the service throws `InvalidOperationException` rather than silently logging raw PII or falling back to unkeyed hashing.

### [ADR-020] Retry orchestration lives in AuditService, no separate ResiliencyPolicy class
**Decision:** Wire the named resilience pipeline (`audit-save`) directly into `AuditService.IngestAsync` rather than introducing the `ResiliencyPolicy` component listed in the original design table.
**Alternatives considered:** A dedicated `ResiliencyPolicy` class implementing `IAuditRepository` (decorator) or a separate orchestrator that owns the pipeline and the dead-letter store.
**Rationale:** The pipeline *is* the policy — `Microsoft.Extensions.Resilience` already provides the retry strategy, telemetry, and cancellation handling. A wrapper class would only forward calls and add an indirection layer with no behavioural value. Keeping the pipeline + dead-letter orchestration inside `AuditService` puts the failure-handling logic next to the success-path log statement, which is where a reader expects to see it. The dead-letter store remains a separate, single-purpose component behind its own interface.
**Tradeoffs:** `AuditService` now has two infrastructure dependencies (the repository and the dead-letter store) plus a pipeline. If the retry contract grows (different policies per failure type, circuit breaking, fallback values) the indirection may become worth it; revisit at that point.

### [ADR-021] MaxRetryAttempts = 1, dead-letter is the durability backstop
**Decision:** Configure the `audit-save` pipeline with `MaxRetryAttempts = 1` and a 200ms constant delay. Any further failure routes the event to the dead-letter store.
**Alternatives considered:** Higher retry counts (3–5) with exponential backoff; no retry at all (dead-letter on first failure).
**Rationale:** FR-8 requires "retry at least once before routing to a dead-letter store". One retry (two total attempts) is the minimum that satisfies the requirement and recovers from genuinely transient blips (file-lock contention, brief I/O stalls). Aggressive retrying against a local file store would mostly amplify a persistent failure rather than fix it — the dead-letter file is the durable backstop, not the retry policy. A short constant delay (200ms) avoids burning the request thread; exponential backoff is unnecessary at one retry.
**Tradeoffs:** Truly transient failures longer than ~200ms still end up in dead-letter. Acceptable: the spec's reliability bar is "no event silently lost", which dead-letter satisfies. Operators can tune the policy in `Program.cs` without touching domain code.

### [ADR-022] Cancellation semantics: re-throw OperationCanceledException, dead-letter uses CancellationToken.None
**Decision:** In `AuditService.IngestAsync`, `OperationCanceledException` propagating out of the resilience pipeline is re-thrown to the caller rather than treated as a store failure. The dead-letter `AppendAsync` call passes `CancellationToken.None` instead of the inbound request token.
**Alternatives considered:** (1) Catch all exceptions including cancellation and write to dead-letter; (2) propagate the inbound token to the dead-letter write to honour caller deadlines uniformly.
**Rationale:** A cancelled request is not a store failure — the resilience pipeline already observes the token and stops retrying when cancellation is signalled, so the `OperationCanceledException` it surfaces is meaningful, not noise. Persisting these to the dead-letter file would pollute it with events the caller never confirmed wanting to submit, and would mask genuine store outages during operator triage. For the dead-letter write itself, the durability contract is "no submitted event is silently lost" — if the caller's connection drops or their token cancels in the narrow window between the store failure and the dead-letter capture, we must still land the event on disk. `CancellationToken.None` guarantees the capture completes.
**Tradeoffs:** The dead-letter write cannot be cancelled, so it can in principle block the request thread for the duration of a file append. Acceptable given the append is a single small write under a `SemaphoreSlim` already in use; the alternative (silently losing a captured-but-not-written event under cancellation) is worse.

### [ADR-012] POST endpoint as the event producer interface
**Decision:** Treat `POST /api/v1/audit/events` as the event producer interface required by FR-1. No separate background simulation component is introduced.
**Alternatives considered:** A background `IHostedService` that autonomously generates clinic events; a dedicated `/simulate` endpoint.
**Rationale:** A separate simulation component would duplicate what the POST endpoint already provides and add scope without satisfying an additional requirement. The spec's "interface that simulates Actions" is the HTTP endpoint itself — callers and tests drive it directly.
**Tradeoffs:** The system does not self-demonstrate without an external caller or test. A background simulator would make the behaviour more visible during a live demo.
