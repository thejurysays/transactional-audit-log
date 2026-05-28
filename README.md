# Transactional Audit Log

A .NET 8 ASP.NET Core Web API that captures immutable audit events for clinic actions, computes structured field-level diffs for updates, and exposes a searchable audit trail.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Setup

1. Copy the example configuration:
   ```bash
   cp src/TransactionalAuditLog/appsettings.example.json src/TransactionalAuditLog/appsettings.json
   ```

2. Review `appsettings.json` — the defaults work out of the box for local development with the in-memory stub repository.

## Running the API

```bash
dotnet run --project src/TransactionalAuditLog
```

- API base URL: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`
- Health check: `http://localhost:5000/health`

## Running Tests

```bash
dotnet test src/TransactionalAuditLog.Tests
```

## Configuration

### Feature Flags

| Flag | Default | Description |
|---|---|---|
| `Features:UseStubRepository` | `true` | `true` uses the in-memory stub repository; `false` requires the file-backed repository (available from v0.4.0) |

Override via environment variable (useful in containers):
```bash
Features__UseStubRepository=true
```

### Storage

| Key | Default | Description |
|---|---|---|
| `Storage:AuditFilePath` | `audit_store.json` | Path to the newline-delimited JSON audit store (used when `UseStubRepository=false`) |
| `Storage:DeadLetterFilePath` | `dead_letter_events.json` | Path to the newline-delimited JSON dead-letter file. Events that still fail after the retry pipeline is exhausted are appended here as `{ failedAt, reason, event }` so they can be inspected and replayed. |

### Logging

| Key | Default | Description |
|---|---|---|
| `Logging:PseudonymKey` | *(dev key in appsettings.json)* | HMAC-SHA256 key used to pseudonymize PII fields (`ActorId`, `ResourceId`) in operational logs. **Must be overridden in production.** Same key produces the same pseudonym, enabling log correlation without exposing raw values. |

Override via environment variable in production:
```bash
Logging__PseudonymKey=<strong-random-secret>
```

> **Note:** Rotating this key breaks cross-session log correlation — treat it as long-lived. If the key is absent at startup the service will not start.

### CORS

Allowed origins are configured under `Cors:AllowedOrigins`. The default allows `http://localhost:3000`.

## API Reference

### POST /api/v1/audit/events

Ingest a new audit event. Supply an optional `EventId` (UUID) for idempotent retries — submitting the same `EventId` twice returns 409 rather than creating a duplicate.

**Request body:**
```json
{
  "eventId": "optional-uuid",
  "actorId": "user-123",
  "actionType": "PatientUpdated",
  "resourceType": "Patient",
  "resourceId": "patient-456",
  "before": { "phone": "555-1234" },
  "after":  { "phone": "555-5678" }
}
```

- Omit `before` for create events (payload will be the full `after` object).
- Omit `after` for delete events (payload will be the full `before` object).
- Supply both for update events (payload will be a structured field-level diff).

If the audit store fails, the write is retried once (200ms delay). If it still fails, the event is captured in the dead-letter file (`Storage:DeadLetterFilePath`) and the caller receives `503 Service Unavailable` — no submitted event is silently dropped.

**Responses:** `201 Created` · `400 Bad Request` · `409 Conflict` · `503 Service Unavailable`

### GET /api/v1/audit

Search audit entries. Exactly one query parameter must be supplied. Results are ordered most-recent-first.

```
GET /api/v1/audit?actor_id=user-123
GET /api/v1/audit?resource_type=Patient
```

**Responses:** `200 OK` · `400 Bad Request`

### GET /health

Returns `200 Healthy` when the service is running.
