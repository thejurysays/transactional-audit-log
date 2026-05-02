## TODO
Change Claude.md to: DESIGN —

# Claude Code Prompt
- Read CLAUDE.md and docs/spec.md. Produce a design document in docs/design.md. Include: [system architecture, data
   models, API contracts, key technical decisions, implementation slices with independently-mergeable strategies]. I
  will review and edit this before you write any code.
  - **TODO** should ask CC to ask me questions about the work
  - Spend 20-30 minutes on this

# Human Questions
- which decision are you referring to? "the endpoint is the interface described in FR-1. See decisions.md for
  rationale."
- double check the "Delete event — full before object:" I thought we were not allowed to delete audits?
- this confuses me: "SQLite via EF Core for persistence..." why add SQLite and EF when we are using a file based
  store?

# AI Review Output (use Claude Code again)
- review the @docs/design.md make sure we are meeting the @docs/spec.md requirements. We don't want to miss
  anything and we don't want to add anything not required.

# Further Instructions
- update @docs/decisions.md with the decisions made during the @docs/design.md creation.
- I created a design.md and a decisions.md file, create me a conventional commit message for this work

## Current Phase
Change the Claude.md file to: IMPLEMENTATION — follow all rules below.

## Updates
- need to check that Claude.md file doesn't instruct to design doc for unit test happy path, we want full testing done on the implementation.