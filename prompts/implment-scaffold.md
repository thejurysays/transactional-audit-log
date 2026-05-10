# TODO
Change the Claude.md file to: IMPLEMENTATION — follow all rules below.

# Claude Code Prompt
Read CLAUDE.md and docs/design.md.
  Scaffold the .NET 8 solution — do not implement any business logic yet.
  Create:
  - Solution file
  - src/[ProjectName] — main API project
  - src/[ProjectName].Tests — xUnit test project
  - Wire both projects into the solution
  - Add a .gitignore for .NET (bin/, obj/, .vs/, appsettings.Development.json)
  - Add appsettings.json with Features section for feature flags
  - Add appsettings.example.json as a safe committed reference (no secrets)
  - Add a /health endpoint
  - Stub repository interfaces and implementations for all repositories identified in docs/design.md
  - Confirm dotnet build and dotnet test both pass

  Stop when scaffold is complete. I will review before we proceed to Slice 1.

# Human Questions
- /simplify
- /security-review
- update Readme.md with slice 1 work
- is there any thing from slice 1 that should have a decision.md entry?
- update the CHANGELOG.md 