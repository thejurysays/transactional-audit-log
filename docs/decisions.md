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

### [ADR-012] POST endpoint as the event producer interface
**Decision:** Treat `POST /api/v1/audit/events` as the event producer interface required by FR-1. No separate background simulation component is introduced.
**Alternatives considered:** A background `IHostedService` that autonomously generates clinic events; a dedicated `/simulate` endpoint.
**Rationale:** A separate simulation component would duplicate what the POST endpoint already provides and add scope without satisfying an additional requirement. The spec's "interface that simulates Actions" is the HTTP endpoint itself — callers and tests drive it directly.
**Tradeoffs:** The system does not self-demonstrate without an external caller or test. A background simulator would make the behaviour more visible during a live demo.
