## TODO
Change Claude.md to: SPEC — extract requirements into docs/spec.md only. Do not suggest implementation details, architecture, or code.

# Claude Code Prompt
- Read CLAUDE.md and the assignment README.md. Extract the requirements and rewrite them into docs/spec.md using
  this structure: [Problem statement, Functional requirements, Non-functional requirements, Out of scope, Acceptance
  criteria]. Be precise. Do not invent requirements that are not stated. I will review the spec before we proceed.
  - **TODO** should ask CC to ask me questions about the work
  - Spend 15–20 minutes on this

 # AI Review Output (Using different model)
- should you add ResourceType to the list of fields to be captured?
- is this a good enhancement? for update events, the Payload must record only the fields that changed. The diff must
   be structured (e.g., field: { old: value, new: value }) rather than a flat string, ensuring programmatic
  readability.
- Another improvement might be to say where to write the dead letter event. Implement a basic retry mechanism (e.g.,
   3 attempts). If the Audit Store remains unavailable, the event must be persisted to a local dead_letter_events.json
  file to prevent data loss.
- suggest adding a tiny detail to the Security section: Ensure that the "Diffing" logic specifically handles
  null/empty transitions gracefully, as missing medical history is as important as changed history.

# Human Questions
- I will commit the spec to a new branch what is a good name for the branch
- I'm about to commit a change, should the CHANGELOG.md be updated for a spec.md being added?
- should the decisions.md be updated during the spec phase?