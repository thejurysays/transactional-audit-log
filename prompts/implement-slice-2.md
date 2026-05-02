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
- When you're ready: run /simplify, /security-review, then let me know and I'll update the changelog, bump the version, and display the git commands.

# Updates
- Add big O notation to the claude.md file
- code should use commenting to explain areas of the code.