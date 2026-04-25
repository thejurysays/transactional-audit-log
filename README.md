# Practice Exercise 3: The "Transactional Audit Log"
## The Scenario:
Jane App handles sensitive medical and financial data. Every time a "Record" (like a Patient Note or an Invoice) is created, updated, or deleted, the system must generate an immutable audit log.

## The Task:
Build a backend service that intercepts "Events" and persists them into a searchable Audit Store.

## The Requirements:

### The Event Producer: 
Create a simple interface that simulates "Actions" being taken in a clinic (e.g., PatientUpdated, AppointmentCancelled).

### The Audit Service: 
Capture the Timestamp, ActorID, ActionType, and a Payload (the data that changed).

### The "Architect" Twist: Implement a "Diffing" logic. 
If a Patient's phone number changes from A to B, the audit log should store the specific change, not just the whole new object.

### Search & Retrieval:
Expose an API endpoint GET /audit?actor_id=123 or GET /audit?resource_type=Patient.
The results must be returned in reverse chronological order.

### Resiliency: 
Implement a basic "Retry" mechanism or a "Dead Letter" concept. If the Audit Store (e.g., a mock database or file) is "locked" or fails, how does your service handle the event so it isn't lost?
