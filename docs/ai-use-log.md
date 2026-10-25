### Entry 1: Design specification (Copilot Chat, Ask mode)
- Prompt (sanitized):
Using #copilot-instructions.md, produce the Markdown for docs/design-spec.md. Output it in your reply only; do not create or edit any file (I will paste it). Maximum 2 pages. No starter project exists, so this is the minimal in-memory service fallback. Sections:

1. Service boundary: in scope / out of scope (no frontend, catalog, payments, identity, multi-term, deployment, database, configurable policy engine).
2. Data model: OfferingOptions (Id, CourseCode, Capacity >= 1, PrerequisiteCourseCode optional); enrolled list; WaitlistEntry (LearnerId, long Sequence); ILearnerRecords.HasCompleted(learnerId, courseCode).
3. API:
   POST   /offerings/{offeringId}/enrollments        body {learnerId}
   DELETE /offerings/{offeringId}/enrollments/{learnerId}
   GET    /offerings/{offeringId}
   Response body: { outcome, learnerId, waitlistPosition?, promotedLearnerIds? }, enums serialized as strings.
   Outcome -> HTTP: Enrolled 201; Waitlisted 202; AlreadyEnrolled/AlreadyWaitlisted 200 (idempotent repeat); PrerequisiteNotMet 422; InvalidRequest 400; unknown offeringId 404 (API layer only); Dropped 200; RemovedFromWaitlist 200; NotEnrolled 200 (repeat or unknown drop is an idempotent no-op).
4. Invariants: enrolled <= capacity; no learner both enrolled and waitlisted; no duplicates in either list (case-insensitive); waitlist order = strictly ascending sequence; after any Enroll or Drop completes, a free seat and a currently-eligible waitlisted learner never coexist.
5. Rules:
   - Enroll: validate -> AlreadyEnrolled / AlreadyWaitlisted -> eligibility (PrerequisiteNotMet, no state change) -> FillFreeSeats() -> Enrolled if a seat is free, otherwise append to the waitlist (Waitlisted, 1-based position).
   - Drop: enrolled -> remove then FillFreeSeats(), report promoted ids; waitlisted -> remove, no promotion; otherwise NotEnrolled.
   - FillFreeSeats (private): while a seat is free, scan the waitlist from the head and promote the first learner eligible NOW. Ineligible entries are skipped, kept in place, re-evaluated next time.
6. Concurrency: single in-process lock, why it is sufficient, what must change for multiple offerings or multiple instances.
7. Delivery approach: TDD in small slices, lock added last, verified by a test that fails without it.
8. Assumptions, tradeoffs, limitations (e.g., no background re-scan when a learner becomes eligible without an enroll or drop; O(n) list lookups acceptable at this scale; unbounded waitlist).
Do not propose alternative designs. If you see a problem, list it under "Open questions" at the end instead of changing decisions.

- Response summary: Copilot produced a two-page spec covering boundary, data model, API with outcome-to-HTTP mapping, invariants, rules, concurrency, assumptions. It also raised two open questions: (1) what the GET response body should be, (2) whether the early returns in Enroll can leave a free seat beside an eligible waiter.
- Decision: Edited (most of the structure accepted; four parts changed)
- What I changed:
  1. Resolved open question 1: defined the GET body as { offeringId, courseCode, capacity, enrolled[], waitlist[] } in FIFO order.
  2. Resolved open question 2 (accepted as a real defect): I constructed the failing case myself (capacity 1, A enrolled, B waitlisted, B's prerequisite revoked, A drops, prerequisite restored, B re-enrolls and gets AlreadyWaitlisted while a seat is free). Changed Enroll to call FillFreeSeats() right after validation, before the duplicate and eligibility checks. Restated invariant 5 so it applies after every validated Enroll and every Drop that removes a learner.
  3. Rejected the process language Copilot copied into the spec ("Do not add unapproved packages...", the Phase 1/Phase 2 workflow in section 7). Replaced it with a neutral sentence; the workflow belongs in the README and this log.
  4. Added limitations: eligibility is evaluated inside the lock (fine for in-memory records only); promotions triggered inside Enroll are not reported in the Enroll response.
- Files / decisions informed: `docs/design-spec.md`; Enroll ordering; test-plan cases added to slice C8; GET contract for slice A3
- Verification: Checked the spec against acceptance criteria 1-5 and the scope boundaries. Hand-walked the scenario in change 2 against the original and edited Enroll order to confirm the original left the invariant broken. Automated verification pending: the new C8 tests (waiter's own repeat Enroll after becoming eligible; ineligible newcomer) must fail in Phase 1 and pass in Phase 2. [update with test names and results later]
- Commit: `docs: add design specification`

### Entry 2: Solution scaffold (Copilot Agent mode)
- Prompt (sanitized):
Following #copilot-instructions.md and #design-spec.md

Step: scaffold. One concern: project structure only, no feature logic.
Files you may create or edit: CourseWaitlist.slnx (or the solution file `dotnet new sln` creates on this SDK), the three .csproj files, Directory.Build.props, .gitignore, .editorconfig, CourseWaitlist.Api/Program.cs. Touch nothing else. Delete the template placeholder files Class1.cs and UnitTest1.cs.
- Projects: CourseWaitlist.Core (classlib), CourseWaitlist.Api (web, Minimal API), CourseWaitlist.Tests (xunit template), all net10.0. Api references Core. Tests references Core and Api.
- Directory.Build.props: TargetFramework net10.0, Nullable enable, ImplicitUsings enable, TreatWarningsAsErrors true.
- Tests packages: FluentAssertions pinned to 7.2.0 and Microsoft.AspNetCore.Mvc.Testing 10.0.* (latest stable 10.0.x), plus the template defaults.
- Program.cs: keep the template content and append `public partial class Program { }`.
- Add a dotnet .gitignore and a minimal .editorconfig (file-scoped namespaces, 4-space indent).
Show the CLI commands you ran. Run `dotnet build` and report the real output. STOP, list changed files, propose a Conventional Commit message.

- Response summary: Copilot created the .slnx solution and three net10.0 projects, wired references, added Directory.Build.props, .editorconfig, .gitignore, removed the Class1.cs and UnitTest1.cs placeholders, appended `public partial class Program { }`, and proposed the commit message `chore: scaffold course waitlist solution`.
- Decision: Edited
- Deviations I found on review:
  1. First scaffold ran from the parent directory; Copilot then deleted its generated output there and re-ran in the repo. I checked the parent folder: nothing else was affected.
  2. Deleted appsettings.json, appsettings.Development.json and excluded launchSettings.json, none of which I named. Accepted the outcome (appsettings is recreated in slice A0) but added a rule to the instructions file forbidding unnamed deletions and out-of-repo commands.
  3. Wrote Microsoft.AspNetCore.Mvc.Testing as a floating `10.0.*`; I pinned it to `10.0.13` by hand for reproducible builds.
  4. Summarized build/test output instead of quoting it; I ran the commands myself.
- Verification (my own runs): `dotnet build CourseWaitlist.slnx -warnaserror` -> [0 warnings, 0 errors]; `dotnet test` -> [no tests available, expected]; `dotnet list package` -> FluentAssertions 7.2.0, Mvc.Testing 10.0.13, xUnit packages: 2.9.3; `git status` -> [clean / files listed].
- Files / decisions informed: solution structure, `.github/copilot-instructions.md` (rule tightening), A0 slice (appsettings.json must now be created)
- Commits: `chore: scaffold course waitlist solution`; `docs: restrict copilot to repo root and named files; log scaffold`