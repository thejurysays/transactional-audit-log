# Specification

## Problem Statement

The company handles sensitive medical and financial data. Whenever a Record (such as a Patient Note or Invoice) is created, updated, or deleted, the system must generate an immutable audit log capturing who performed the action, when it occurred, what type of action it was, and exactly what changed. This project delivers a backend service that accepts those events, persists them to a durable Audit Store, records only the precise field-level changes for update events rather than the entire object, exposes the stored entries for search and retrieval, and ensures no event is silently lost if the Audit Store is temporarily unavailable.

## Functional Requirements

1. The system must provide an interface that simulates events representing actions taken on Records in a clinic (e.g., PatientUpdated, AppointmentCancelled).
2. Each event must be persisted to the Audit Store as an audit entry containing: Timestamp, ActorID, ActionType, ResourceType, and Payload.
3. For update events, the Payload must record only the fields that changed (a diff) — not the entire new object. The diff must be structured as `field: { old: value, new: value }` for each changed field, ensuring programmatic readability. For example, if a patient's phone number changes from A to B, the Payload stores only `{ "phone": { "old": "A", "new": "B" } }`. The diff must handle null/empty transitions gracefully — a field changing to or from null must be recorded as a change, since absent medical data is as significant as changed data.
4. Audit entries must be immutable: once written, an entry cannot be modified or deleted.
5. The API must expose `GET /audit?actor_id={id}` returning all audit entries for the specified actor.
6. The API must expose `GET /audit?resource_type={type}` returning all audit entries for the specified resource type (e.g., Patient).
7. Both search endpoints must return results in reverse chronological order (most recent first).
8. If the Audit Store fails or is unavailable when an event is written, the service must retry the write at least once before routing the event to a dead-letter store. Dead-letter events must be persisted to durable local storage so they can be recovered.

## Non-Functional Requirements

**Security**
- All input received at API boundaries must be validated and sanitized before processing, given the sensitive nature (medical and financial) of the data.

**Reliability**
- The retry or dead-letter mechanism must ensure no submitted event is silently discarded on a transient store failure.
- Audit entries must remain immutable after creation — no update or delete path may exist for stored entries.

## Out of Scope

- Authentication and authorization (not mentioned in the assignment).
- A production-grade database — a mock database or file-based store is explicitly acceptable.
- Combining multiple search filter parameters in a single query (e.g., filtering by both actor_id and resource_type simultaneously).
- Pagination or cursor-based navigation of search results.
- Any frontend, UI, or client application.
- Soft-delete or expiry of audit entries.

## Acceptance Criteria

1. An event submitted to the service appears in the Audit Store with Timestamp, ActorID, ActionType, and Payload populated correctly.
2. An update event where exactly one field changes produces an audit entry whose Payload contains only the before/after values for that field — not the full object.
3. `GET /audit?actor_id={id}` returns all and only entries for that actor, ordered most-recent-first.
4. `GET /audit?resource_type={type}` returns all and only entries for that resource type, ordered most-recent-first.
5. When the Audit Store is forced to fail during an event write, the event is not lost — it is either retried successfully or captured in a dead-letter store that can be inspected.
6. No existing audit entry can be modified or deleted via any code path.
7. Each acceptance criterion above is verified by at least one automated test covering both the happy path and relevant failure cases.
