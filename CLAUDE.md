# Assignment Project

## Current Phase
IMPLEMENTATION — follow all rules below.

## Stack
- Language: C# (.NET 8)
- Framework: ASP.NET Core Web API
- Test runner: xUnit
- Database: file-based store
- Project name: transactional-audit-log

## Docs
- Spec: docs/spec.md
- Design: docs/design.md
- Decisions: docs/decisions.md
- Changelog: CHANGELOG.md
- Build/run: README.md

## Rules
- Read docs/design.md before writing any code
- Implement one slice at a time — stop and confirm before starting the next
- Write tests alongside implementation, not after
- Use feature flags (appsettings.json with environment variable override support) to gate incomplete features
- Never implement beyond the current slice's scope
- Keep PRs under 300 lines of diff
- When making a non-obvious technical decision, append it to docs/decisions.md
- At the end of each slice, remind me to run /simplify, /security-review, and /review before updating any docs
- After I confirm the code is ready, update CHANGELOG.md, bump the version in the .csproj, and update README.md if any new setup steps, environment variables, or feature flags were added — stop before committing, I will review and commit
- After I confirm the slice is ready, display the git add, git commit, git push, and gh pr create commands for me to copy and run — do not run them
- Commit messages must: use conventional commits (feat:, fix:, chore:, test:), use imperative mood, explain why the change was necessary and any side effects, be atomic

## .NET Conventions
- Use MVC Controllers, not Minimal API — keeps focus on problem-solving rather than framework features
- Controllers thin — delegate to service layer; decorate with [ProducesResponseType] and [ApiController]
- Design POST and PUT endpoints to be idempotent
- Seal classes by default — unseal only when inheritance is explicitly designed for
- Never use new HttpClient() — use IHttpClientFactory
- Pass CancellationToken through the entire async call chain, not just at the top level
- Use ArgumentNullException.ThrowIfNull() guard clauses at method entry
- No magic strings — route names, policy names, claim types are constants or enums
- Repository pattern for data access; materialize at the boundary with .ToListAsync() — never return IQueryable from a repository

## Error Handling
- Use Result<T> pattern for expected failures (not found, validation, business rules) — implement as a lightweight record, no library dependency
- Reserve exceptions for unexpected failures — infrastructure down, unrecoverable state
- Implement IExceptionHandler for centralized unhandled exception handling returning sanitized ProblemDetails (RFC 7807)

## Logging
- Inject ILogger<T>; structured logging with named parameters not string interpolation
- Consistent property names across log statements for the same concept — enables log queries
- Correct log levels — Debug for diagnostics, Information for business events, Warning for recoverable issues, Error for failures
- Correlation ID middleware so all statements within a request share a traceable identifier
- Enrich all logs globally in Program.cs with machine name, environment name, and app version

## Testing
- Unit tests for domain and service logic; integration tests via WebApplicationFactory<T> for API endpoints
- Test naming: MethodName_Scenario_ExpectedResult
- Test behaviour not implementation — tests must survive refactoring
- Cover unhappy paths and edge cases, not just the happy path

## Security
- Sanitize and validate all input at the controller boundary
- Configure CORS explicitly — no wildcard origins
- Apply rate limiting via AddRateLimiter for public endpoints

## Design Principles
- Separate concerns: domain logic out of controllers and data access layer
- Depend on interfaces, not concrete implementations
- Patterns emerge from the problem — never imposed for their own sake

## Stub Repository Pattern
- Use stub repositories to keep API slices independently mergeable before the real DB implementation exists
- Each stub implements the same interface as the real repository and returns hardcoded data
- Swap real vs stub via DI in Program.cs using IConfiguration — no stub logic leaks into the service layer:
  var useStub = builder.Configuration.GetValue<bool>("Features:UseStubRepository");
  if (useStub)
      builder.Services.AddScoped<IUserRepository, StubUserRepository>();
  else
      builder.Services.AddScoped<IUserRepository, UserRepository>();
- Stubs are temporary — every stub must be replaced by a real implementation before version 1.0.0

## Feature Flags
- Define in appsettings.json: "Features": { "FeatureName": false }
- Inject IOptionsSnapshot<FeatureFlags> — scoped per request, picks up live config changes without redeployment
- Environment variables override via __ separator (e.g. Features__FeatureName=true) — required if containerized
- Flag checks at controller or service entry point only — not scattered through logic

## Versioning & API
- Semantic versioning in .csproj <Version> — pre-release slices use 0.x.0, reaches 1.0.0 when all requirements are met
- Tag each completed slice: git tag v0.x.0
- API routes versioned: /api/v1/
- Enable OpenAPI/Swagger with XML doc comments on controllers

## Reliability
- Health check endpoint (/health) via Microsoft.Extensions.Diagnostics.HealthChecks
- Resiliency patterns (retry, circuit breaker, timeout) on external HTTP calls and database operations via Microsoft.Extensions.Resilience — not on internal service calls
- If using IHostedService, use IHostApplicationLifetime to ensure in-flight work completes and logs flush before exit
- Observability via Activity and Meter (System.Diagnostics) — optional final slice only if all core requirements are complete and tested

## What Reviewers Are Evaluating
- Every requirement in docs/spec.md is demonstrably met
- Tests cover behaviour and unhappy paths
- Git history shows incremental value delivery
- docs/decisions.md shows reasoning and tradeoffs behind key choices
- README.md is accurate and the project runs from scratch following it
- Code is readable — a reviewer should understand each method without tracing its callers
