# Design

## System Architecture

### Components

| Component | Responsibility |
|---|---|
| **CorrelationIdMiddleware** | Assigns a unique request ID to every incoming request; enriches all log statements within that request |
| **AuditEventsController** | Thin controller for `POST /api/v1/audit/events`; validates input, delegates to `AuditService` |
| **AuditController** | Thin controller for `GET /api/v1/audit`; validates query params, delegates to `AuditService` |
| **AuditService** | Orchestrates event ingestion (validate → diff → persist) and retrieval; owns business rules |
| **DiffEngine** | Computes structured field-level diffs between before/after payloads; handles null/empty transitions |
| **IAuditRepository** | Repository interface; defines `SaveAsync` and search methods — no `Update` or `Delete` methods exist |
| **StubAuditRepository** | In-memory implementation of `IAuditRepository`; used during slices 2–3 before the real DB exists |
| **AuditRepository** | JSON file implementation of `IAuditRepository`; replaces stub in slice 4 |
| **ResiliencyPolicy** | Wraps `IAuditRepository.SaveAsync` with a retry pipeline via `Microsoft.Extensions.Resilience` |
| **DeadLetterStore** | Appends failed events to a local JSON file after retries are exhausted |
| **GlobalExceptionHandler** | Implements `IExceptionHandler`; catches unhandled exceptions and returns sanitised `ProblemDetails` (RFC 7807) |
| **HealthCheck** | Exposes `/health` via `Microsoft.Extensions.Diagnostics.HealthChecks` |

### Request Flow — Event Ingestion (POST)

```
Client
  → CorrelationIdMiddleware
  → AuditEventsController.IngestAsync
      → AuditService.IngestAsync
          → DiffEngine.Compute          (update events only)
          → ResiliencyPolicy.SaveAsync
              → IAuditRepository.SaveAsync
              [on retry exhaustion] → DeadLetterStore.AppendAsync
  ← 201 Created / 503 Service Unavailable
```

### Request Flow — Audit Retrieval (GET)

```
Client
  → CorrelationIdMiddleware
  → AuditController.SearchAsync
      → AuditService.SearchAsync
          → IAuditRepository.SearchByActorAsync  |  SearchByResourceTypeAsync
  ← 200 OK (results, reverse chronological)
```

### Event Producer

The `POST /api/v1/audit/events` endpoint serves as the event producer interface. Callers (or tests) submit events representing clinic actions directly. No separate background simulation component is introduced; the endpoint is the interface described in FR-1. See `decisions.md` for rationale.

---

## Data Models

### AuditEntry — domain model

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key; client-supplied for idempotency (auto-generated if omitted) |
| `Timestamp` | `DateTimeOffset` | Set by the service at ingestion time; not caller-supplied |
| `ActorId` | `string` | Who performed the action |
| `ActionType` | `string` | e.g. `PatientUpdated`, `AppointmentCancelled` |
| `ResourceType` | `string` | e.g. `Patient`, `Appointment` |
| `ResourceId` | `string` | Which specific resource was affected (see `decisions.md`) |
| `Payload` | `string` | JSON string; structured diff for updates, full object for create/delete |

Immutability is enforced at the repository interface level — no `Update` or `Delete` methods exist on `IAuditRepository`.

### IngestEventRequest — API input model

| Field | Type | Required | Notes |
|---|---|---|---|
| `EventId` | `Guid?` | No | If supplied, used as the entry `Id` for idempotency |
| `ActorId` | `string` | Yes | |
| `ActionType` | `string` | Yes | |
| `ResourceType` | `string` | Yes | |
| `ResourceId` | `string` | Yes | |
| `Before` | `JsonObject?` | Conditional | Required for update and delete events; null for create |
| `After` | `JsonObject?` | Conditional | Required for update and create events; null for delete |

### Payload shapes

**Update event — structured diff:**
```json
{
  "phone": { "old": "555-1234", "new": "555-5678" },
  "notes": { "old": "Allergic to penicillin", "new": null }
}
```

**Create event — full after object:**
```json
{ "name": "Jane Doe", "dob": "1980-03-15", "phone": null }
```

**Delete event — full before object:**
```json
{ "name": "Jane Doe", "dob": "1980-03-15", "phone": "555-1234" }
```

### AuditEntryResponse — API output model

All fields from `AuditEntry`, with `Payload` deserialized as `object` (not a raw string).

### DeadLetterEntry — written to `dead_letter_events.json`

| Field | Type |
|---|---|
| `FailedAt` | `DateTimeOffset` |
| `Reason` | `string` |
| `Event` | `IngestEventRequest` |

The file is append-only (newline-delimited JSON, one object per line) so concurrent writes do not corrupt earlier entries.

---

## API Contracts

### POST /api/v1/audit/events

Submits a new event for ingestion. Idempotent: submitting the same `EventId` twice returns the original entry without creating a duplicate.

**Request body:** `IngestEventRequest`

| Status | Condition |
|---|---|
| `201 Created` | Entry persisted; body is `AuditEntryResponse` |
| `400 Bad Request` | Validation failure; body is `ProblemDetails` |
| `409 Conflict` | `EventId` already exists; body is `ProblemDetails` |
| `503 Service Unavailable` | Store failed after retries; event routed to dead letter; body is `ProblemDetails` |

### GET /api/v1/audit

Exactly one of `actor_id` or `resource_type` must be supplied. Results are ordered most-recent-first.

| Query param | Example |
|---|---|
| `actor_id` | `GET /api/v1/audit?actor_id=user-123` |
| `resource_type` | `GET /api/v1/audit?resource_type=Patient` |

| Status | Condition |
|---|---|
| `200 OK` | Body is `AuditEntryResponse[]` (empty array if no matches) |
| `400 Bad Request` | Neither param supplied, or both supplied; body is `ProblemDetails` |

### GET /health

| Status | Condition |
|---|---|
| `200 Healthy` | All registered health checks pass |
| `503 Unhealthy` | One or more checks fail |

---

## Key Technical Decisions

Full rationale for each decision is recorded in `decisions.md`.

1. **JSON file store for persistence** — the spec explicitly permits a file-based store; a single append-style JSON file requires no new packages, no migrations, and no server. `System.Text.Json` is already present in the ASP.NET Core host.

2. **Payload stored as a JSON string** — the two search filters (actor, resource type) filter on other fields only; payload contents are never queried. Storing it as a serialised string keeps the file store simple with no query benefit lost.

3. **ResourceId as an explicit stored field** — not listed in the spec's field enumeration (FR-2); this is a deliberate addition beyond the spec. An audit entry is not actionable without knowing *which* patient was updated, not just that some patient was.

4. **Client-supplied EventId for idempotency** — `POST` endpoints must be idempotent per project conventions. An optional caller-supplied `EventId` (UUID) lets event producers retry safely without creating duplicate entries.

5. **DiffEngine as a standalone service** — isolated from `AuditService` so that diffing logic (including null/empty transitions) can be unit-tested independently without standing up the full service graph.

6. **Dead letter as newline-delimited JSON file** — simplest durable local storage with no additional infrastructure dependency; append-only writes are safe under concurrent access.

7. **POST endpoint as the event producer interface** — a separate in-process simulation component would duplicate what the POST endpoint already provides and add scope without meeting an additional requirement.

---

## Implementation Slices

### Slice 1 — Project Scaffold
**Strategy: Neither** (self-contained; no business logic, no incomplete endpoints)

- Create solution, Web API project, and xUnit test project
- Configure `appsettings.json` with `Features` section skeleton and `UseStubRepository` flag
- Register `CorrelationIdMiddleware`
- Register `GlobalExceptionHandler` (`IExceptionHandler` → sanitised `ProblemDetails`)
- Add `/health` endpoint via `Microsoft.Extensions.Diagnostics.HealthChecks`
- Enable OpenAPI/Swagger with XML doc comment support
- Configure explicit CORS and `AddRateLimiter`
- Enrich logs globally with machine name, environment, and app version

Deliverable: the host starts, `/health` returns 200, Swagger UI is reachable.

---

### Slice 2 — Event Ingestion
**Strategy: Stub repository** — `POST /api/v1/audit/events` is fully exposed and functional; persistence goes to an in-memory stub so the API layer and diffing logic are independently testable before the real DB exists.

- Define `AuditEntry`, `IngestEventRequest`, `AuditEntryResponse`, and `DeadLetterEntry` models
- Implement `DiffEngine` with null/empty transition handling
- Implement `AuditService.IngestAsync` (validate → diff → save)
- Define `IAuditRepository` with `SaveAsync` only (no update/delete)
- Implement `StubAuditRepository` (thread-safe in-memory list)
- Implement `AuditEventsController` (`POST /api/v1/audit/events`)
- Wire stub via `UseStubRepository` flag in `Program.cs`
- Unit tests: `DiffEngine` (happy path, null→value, value→null, no-change field excluded)
- Integration tests: `POST` happy path, validation errors, 409 on duplicate `EventId`

Deliverable: `POST` endpoint accepts and stores events; diff is correct; stub swappable via config flag.

---

### Slice 3 — Audit Retrieval
**Strategy: Stub repository** — both `GET` endpoints are fully exposed; the existing stub is extended to support search, keeping this slice independently mergeable before the real DB.

- Extend `IAuditRepository` with `SearchByActorAsync` and `SearchByResourceTypeAsync`
- Extend `StubAuditRepository` with in-memory LINQ implementations (reverse chronological)
- Implement `AuditService.SearchAsync`
- Implement `AuditController` (`GET /api/v1/audit`)
- Integration tests: search by actor, search by resource type, empty results, 400 on missing/double params

Deliverable: both search endpoints return results in reverse chronological order against the stub.

---

### Slice 4 — Persistence
**Strategy: Neither** — this slice replaces the stub with a real file-backed repository; the endpoints and service layer are unchanged. Toggled by setting `UseStubRepository: false`.

- Implement `AuditRepository` using `System.Text.Json` to read/write a newline-delimited JSON file (`audit_store.json`)
- File path configurable via `appsettings.json` (`Storage:AuditFilePath`)
- Writes are append-only; reads load and deserialize the full file then filter in memory
- Register `AuditRepository` in `Program.cs` behind the `UseStubRepository` flag
- Integration tests: repeat slice 2–3 scenarios against `WebApplicationFactory` with a temp file

Deliverable: with `UseStubRepository: false`, all existing tests pass against a real file-backed store.

---

### Slice 5 — Resiliency
**Strategy: Neither** — self-contained addition to the write path; no new endpoints, no stubs required.

- Add `Microsoft.Extensions.Resilience` retry pipeline wrapping `IAuditRepository.SaveAsync`
- Implement `DeadLetterStore` with append-only newline-delimited JSON writes
- Wire retry → dead letter in `AuditService.IngestAsync`
- Return `503 Service Unavailable` to the caller when the event is routed to dead letter
- Unit tests: retry succeeds on second attempt; dead letter file is written after retries exhausted
- Integration tests: simulate store failure; verify event appears in dead letter file

Deliverable: a forced store failure results in retries followed by a recoverable dead-letter entry; no event is silently lost.
