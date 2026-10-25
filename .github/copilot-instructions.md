# Workflow rules
- Smallest possible change per step, one concern per step. Do not touch files I did not name.
- TDD two-phase: Phase 1 = write tests only, run `dotnet test`, confirm they fail for the expected reason, then STOP. Phase 2 (only when I say "Phase 2") = minimal implementation to pass; run ALL tests; STOP. Never edit a test in Phase 2 without telling me.
- NEVER run git commit. At the end of each phase list the changed files and propose a Conventional Commit message.
- Always run `dotnet build` and `dotnet test` and report the real output. Never claim results you did not run.
- No secrets or personal data anywhere.

# Clarifications to the workflow rules
- Phase 1 may only add or edit files in CourseWaitlist.Tests that I name. If a new test unexpectedly passes before implementation, report it and explain; do not delete it.
- "Expected reason" means a NotImplementedException from a stub, or an assertion failure on the behavior under test. A compile error or unrelated exception is NOT an expected reason.
- Do not run git commands that change the repository (commit, add, push, reset, checkout, stash). Read-only git (status, diff) is fine.
- Run every command with the repository root (the folder containing CourseWaitlist.slnx) as the working directory; verify it before running dotnet new or any delete. Never create, modify or delete anything outside the repository root.
- Do not delete files I did not name. If a template generates files I did not list, leave them and tell me.
- Quote the actual build and test output (counts and key lines), not a paraphrase. Run build and test sequentially, never in parallel.

# Stack
- C# on net10.0. Nullable enabled, implicit usings, file-scoped namespaces, TreatWarningsAsErrors.
- Solution CourseWaitlist: CourseWaitlist.Core (classlib: domain + service), CourseWaitlist.Api (ASP.NET Core Minimal API, no controllers), CourseWaitlist.Tests (xUnit).
- In-memory state only. Do NOT add EF Core, any database, MediatR, AutoMapper, FluentValidation, Swagger/Swashbuckle, Moq, NSubstitute, Docker, auth, or any NuGet package I did not name.
- Test packages allowed: the xunit template defaults, FluentAssertions 7.x, Microsoft.AspNetCore.Mvc.Testing 10.0.x.

# Domain rules
- Scope is ONE course offering: enroll, drop, FIFO waitlist promotion.
- Expected outcomes (duplicate, full, ineligible, invalid input) are returned as result enums/records, never thrown. Throw only for programmer errors.
- Duplicate requests are idempotent by state: the same (offering, learner) request never creates a second record and never changes state.
- FIFO order is a monotonic long sequence, never timestamps.
- Learner ids: Trim(), non-empty, max 64 chars, compared with StringComparer.OrdinalIgnoreCase.
- Ineligible learners are never enrolled or promoted. At promotion time, ineligible waitlisted learners are SKIPPED but RETAINED in their original position and re-checked on every later promotion.

# Concurrency rule
- Do NOT add locking until the slice named "C9 concurrency". Before then write plain single-threaded code, keep all state private inside EnrollmentService, and return copies from GetSnapshot() so a single lock can later wrap every public method.
- In C9 use ONE private lock object. Synchronous code only; no async over in-memory state.

# Style
- One-line comment for each non-obvious design decision. If you disagree with a rule, say so instead of silently deviating.