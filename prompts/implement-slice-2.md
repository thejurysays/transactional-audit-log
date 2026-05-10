# TODO
Change the Claude.md file to: IMPLEMENTATION — follow all rules below.
- change to /plan mode, make sure effort is high

# Claude Code Prompt
Switch to /plan mode
```
Implement Slice 2 — Event Ingestion from docs/design.md.

  Scope is exactly what's listed under "Slice 2" in the Implementation Slices section.

  Prior state:
  - IAuditRepository, StubAuditRepository, and AuditEntry are already scaffolded (see the existing files)
  - StubAuditRepository is registered as Singleton — keep it that way or cross-request integration tests will fail
  - UseStubRepository feature flag is wired in Program.cs

  Stop when the deliverable is met: POST endpoint accepts and stores events; diff is correct; stub swappable via config
   flag.
  Write tests alongside implementation, not after.
  Do not update CHANGELOG.md, bump the version, or display git commands — I'll ask for those separately when ready.
  ```


# Human Actions
- switch to plan mode for each implementation phase
- switch back to edit mode
- "review my current changes and check for .NET best practices"
- "review my current changes for Linq query optimizations. Recommend alternative if not efficient."
- When you're ready: run /simplify, /security-review
- is there any thing from slice 1 that needs to be updated in the readme.md 
- is there any thing from slice 1 that should have a decision.md entry?
- then let me know and I'll update the changelog, bump the version, and display the git commands.

# Updates
- Add big O notation to the claude.md file

# Test Data
http://localhost:5268/swagger/index.html
Here's some test JSON you can use:

Minimal (required fields only):


{
  "actorId": "user-123",
  "actionType": "UPDATE",
  "resourceType": "Order",
  "resourceId": "order-456"
}
With a before/after diff (e.g. updating an order's status):


{
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "actorId": "user-123",
  "actionType": "UPDATE",
  "resourceType": "Order",
  "resourceId": "order-456",
  "before": {
    "status": "pending",
    "total": 99.99
  },
  "after": {
    "status": "shipped",
    "total": 99.99
  }
}
Create event (no before):


{
  "actorId": "admin-001",
  "actionType": "CREATE",
  "resourceType": "User",
  "resourceId": "user-789",
  "after": {
    "email": "alice@example.com",
    "role": "viewer"
  }
}
Delete event (no after):


{
  "actorId": "admin-001",
  "actionType": "DELETE",
  "resourceType": "User",
  "resourceId": "user-789",
  "before": {
    "email": "alice@example.com",
    "role": "viewer"
  }
}