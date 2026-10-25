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
- Verification (my own runs): `dotnet build CourseWaitlist.slnx -warnaserror` -> [0 warnings, 0 errors]; `dotnet test` -> [no tests available, expected]; `dotnet list package` -> FluentAssertions 7.2.0, Mvc.Testing 10.0.13, xUnit packages: 2.9.3; `git status` -> files listed.
- Files / decisions informed: solution structure, `.github/copilot-instructions.md` (rule tightening)
- Commits: `chore: scaffold course waitlist solution`; `docs: restrict copilot to repo root and named files; log scaffold`

### Entry 3: Test plan (Copilot Chat, Ask mode)
- Prompt (sanitized):
From #design-spec.md produce the Markdown for docs/test-plan.md. Output it in your reply only; do not create or edit files. Table columns: ID (T-01...), Slice, Scenario, Given, When, Then, Acceptance criterion (1-6 from the assignment), Level (unit / concurrency / integration). Group by these slices and include at least these cases:

C1 construction and validation: constructor throws ArgumentException for capacity 0 and -1; Enroll and Drop return InvalidRequest for null, "", "   " and a 65-character id; no state change.
C2 enroll with capacity: Enrolled with null waitlist position; id is trimmed; fills to capacity; capacity 1 edge; offering without prerequisite makes everyone eligible.
C3 prerequisite: PrerequisiteNotMet with a seat free and snapshot unchanged; learner who completed the prerequisite is Enrolled.
C4 waitlist when full: Waitlisted with positions 1, 2, 3; ineligible learner when full gets PrerequisiteNotMet and is not on the waitlist; enrolled never exceeds capacity.
C5 duplicates: repeat Enroll of an enrolled learner returns AlreadyEnrolled; repeat of a waitlisted learner returns AlreadyWaitlisted with the same position and a single record; case and whitespace variants of an id count as the same learner; an enrolled learner whose prerequisite was later removed still gets AlreadyEnrolled; snapshot unchanged.
C6 drop basics: drop enrolled with empty waitlist returns Dropped and the seat is reusable; drop unknown learner returns NotEnrolled; repeated drop returns NotEnrolled with state unchanged; drop of a waitlisted learner returns RemovedFromWaitlist, later positions shift, nobody is promoted; id variants work.
C7 FIFO promotion: drop promotes the earliest waiter and reports it in PromotedLearnerIds; successive drops promote in FIFO order; a learner who left the waitlist is not promoted.
C8 eligibility during promotion: a front-of-queue learner whose prerequisite was removed after joining is skipped, retained in the same position, and the next eligible learner is promoted; a skipped learner who regains eligibility is promoted at the next promotion ahead of later waiters; a waitlist containing only ineligible learners leaves the seat free with empty PromotedLearnerIds; when a seat is free and a skipped waiter has become eligible again, a new Enroll call gives the seat to the waiter and waitlists the newcomer (no queue jumping); the same waiter's own repeat Enroll returns AlreadyEnrolled (promoted) with the snapshot showing them enrolled; an ineligible newcomer arriving while a newly eligible skipped waiter holds the head of the queue gets PrerequisiteNotMet while the waiter is promoted.
C9 concurrency: 100 distinct learners enroll into capacity 10 (exactly 10 enrolled, 90 waitlisted, unique positions 1..90, no duplicates); 50 identical concurrent Enrolls of one learner (exactly one record); mixed concurrent Enrolls and Drops (invariants hold); 20 concurrent Drops of one enrolled learner (exactly one Dropped, 19 NotEnrolled, exactly one promotion).
A0 API composition: configured offering and capacity are resolvable from the service container; capacity 0 or a missing offering Id fails startup.
A1 POST enroll: 201 Enrolled; 202 Waitlisted with waitlistPosition 1; 200 AlreadyEnrolled; 200 AlreadyWaitlisted; 422 PrerequisiteNotMet as ProblemDetails; 400 for empty learnerId, missing body or null learnerId; 404 for an unknown offeringId.
A2 DELETE: 200 Dropped with the waitlisted learner in promotedLearnerIds; repeat DELETE 200 NotEnrolled; DELETE by a waitlisted learner 200 RemovedFromWaitlist; 404 for an unknown offeringId.
A3 GET: snapshot reflects enrolled and waitlist in order; 404 for an unknown offeringId.

- Response summary: Copilot produced a 58-case table grouped by slices C1-C9 and A0-A3 with Given/When/Then and levels.
- Decision: Edited
- What I changed:
  1. Rejected Copilot's invented acceptance-criterion legend; remapped every row to the assignment's actual criteria 1-6.
  2. Rewrote T-11, T-12 (they exercised full capacity inside the C2 slice) and T-19 (used drops inside C4).
  3. Replaced T-37, which only duplicated C5, with the scenario that exposes the free-seat/eligible-waiter defect found during spec review; made T-38 explicit.
  4. Removed vague outcomes (T-10, T-13, T-22, T-29, T-36, T-40, T-42) and fixed the concurrency expectations.
  5. Added T-59 to T-62 (64-character boundary, waitlisted-and-ineligible repeat, position shift, whitespace DELETE) and a coverage-versus-assignment section.
- Files / decisions informed: `docs/test-plan.md`; Phase 1 prompts for C2, C4, C5, C7, C8 and C9
- Verification: Cross-checked each row against the slice prompts and the design spec. Hand-traced T-37 against the original and revised Enroll order; automated verification comes when the C8 Phase 1 test fails and the Phase 2 test passes. [update later with real test names and results]
- Commit: `docs: add test plan`

### Entry 4: Core skeleton (Copilot Agent mode)
- Prompt (sanitized):
Step: Core skeleton. One concern: public types and signatures only; no behavior. Namespace CourseWaitlist.Core.
Files you may create: CourseWaitlist.Core/OfferingOptions.cs, EnrollOutcome.cs, DropOutcome.cs, EnrollResult.cs, DropResult.cs, OfferingSnapshot.cs, ILearnerRecords.cs, InMemoryLearnerRecords.cs, EnrollmentService.cs. Touch nothing else. Delete the template Class1.cs if it exists.
- OfferingOptions: sealed class with get/set properties (string Id = "", string CourseCode = "", int Capacity, string? PrerequisiteCourseCode) so the configuration binder can fill it.
- enum EnrollOutcome { Enrolled, Waitlisted, AlreadyEnrolled, AlreadyWaitlisted, PrerequisiteNotMet, InvalidRequest }
- enum DropOutcome { Dropped, RemovedFromWaitlist, NotEnrolled, InvalidRequest }
- record EnrollResult(EnrollOutcome Outcome, int? WaitlistPosition)
- record DropResult(DropOutcome Outcome, IReadOnlyList<string> PromotedLearnerIds)
- record OfferingSnapshot(string OfferingId, string CourseCode, int Capacity, IReadOnlyList<string> Enrolled, IReadOnlyList<string> Waitlist) with Waitlist in FIFO order.
- ILearnerRecords { bool HasCompleted(string learnerId, string courseCode); }
- InMemoryLearnerRecords : ILearnerRecords, FULLY implemented (it is a data holder, not behavior under test): Add(learnerId, courseCode), Remove(learnerId, courseCode), HasCompleted. Case-insensitive keys on both sides. Safe for concurrent reads, using an internal lock.
- EnrollmentService(OfferingOptions options, ILearnerRecords records): store the constructor arguments only. Enroll(string? learnerId) -> EnrollResult, Drop(string? learnerId) -> DropResult, GetSnapshot() -> OfferingSnapshot. Each method body is `throw new NotImplementedException();`.
Add XML-doc at the top of EnrollmentService listing the invariants from #file:docs/design-spec.md.
Run `dotnet build` and report the real output. STOP, list changed files, propose a Conventional Commit message.

- Response summary: Copilot created nine files in CourseWaitlist.Core: options class, two outcome enums, three result types, ILearnerRecords, a fully implemented InMemoryLearnerRecords, and EnrollmentService with stubbed methods and the invariants XML-doc. It reported a clean build.
- Decision: Accepted
- What I changed: nothing. I noted one design risk for later tests: record equality on `IReadOnlyList` properties compares by reference, so snapshot comparisons must be structural.
- Files / decisions informed: Core public API; C1 test-support helpers
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test` -> [no tests, expected]; `git diff --stat` -> [only the nine Core files]
- Commit: `feat(core): add enrollment service skeleton`

### Entry 5: C1 Phase 1, validation tests (Copilot Agent mode)
- Prompt (sanitized):
Slice C1 (construction and input validation). Phase 1: tests only.
Files you may create: CourseWaitlist.Tests/Support/TestServiceBuilder.cs, CourseWaitlist.Tests/Support/InvariantAssertions.cs, CourseWaitlist.Tests/Support/SnapshotAssertions.cs, CourseWaitlist.Tests/EnrollmentValidationTests.cs. Touch nothing else. Do not edit production code.
- TestServiceBuilder: WithCapacity(int), WithPrerequisite(string?), WithCompleted(courseCode, params string[] learnerIds); exposes the InMemoryLearnerRecords instance so tests can Add/Remove later; Build() returns EnrollmentService. Seed learner ids without surrounding whitespace.
- InvariantAssertions.AssertInvariants(OfferingSnapshot): enrolled count <= capacity, no overlap between enrolled and waitlist, no case-insensitive duplicates inside either list.
- SnapshotAssertions.ShouldMatch(this OfferingSnapshot actual, OfferingSnapshot expected): compares OfferingId, CourseCode, Capacity and the Enrolled and Waitlist lists element by element in order (FluentAssertions Equal on the lists). Never compare snapshots or results containing lists with Be/Equals, because record equality compares the lists by reference.
- Implement the C1 cases (T-01 to T-08) from #file:docs/test-plan.md with xUnit + FluentAssertions 7.x only. Use [Theory] with [InlineData] for null, empty and whitespace ids; build the 65-character id with new string('a', 65) in a separate [Fact] or [MemberData], since InlineData needs constants. For constructor tests assert Action.Should().Throw<ArgumentException>(). Names Method_Scenario_ExpectedResult, Arrange/Act/Assert comments, [Trait("Slice","C1")] on each test. Call AssertInvariants wherever a snapshot is available.
Run dotnet build, then dotnet test, and quote the real output. Expected failures: constructor tests fail with "expected ArgumentException but none was thrown"; Enroll, Drop and GetSnapshot tests fail with NotImplementedException. Report any other failure reason. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot added TestServiceBuilder, InvariantAssertions, SnapshotAssertions and EnrollmentValidationTests (12 tests). Its first run failed during setup because tests called GetSnapshot before the operation under test; it restructured the tests to act first and re-ran. Final run: 12 failed, 0 passed; failures were ArgumentException-not-thrown and NotImplementedException.
- Decision: Accepted with edits
- What I changed: added [Trait("Case","T-xx")] traits by hand; added T-63 to the test plan for the extra GetSnapshot test.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test --filter Slice=C1` -> [12 failed, reasons checked]; `git status` -> [only test files].
- Commit: `test(core): add C1 validation tests`

### Entry 6: C1 Phase 2, validation implementation (Copilot Agent mode)
- Prompt (sanitized):
Phase 2. Slice C1. You may edit only CourseWaitlist.Core/EnrollmentService.cs. Do not edit any test; if you believe a test must change, stop and tell me. Minimum to pass the C1 tests:
- The constructor throws ArgumentException when options.Capacity < 1.
- A private normalizer: Trim(); null, empty, whitespace or length > 64 is invalid.
- Enroll returns EnrollResult(EnrollOutcome.InvalidRequest, null) and Drop returns DropResult(DropOutcome.InvalidRequest, an empty list) for invalid ids.
- GetSnapshot returns OfferingSnapshot with the options' Id, CourseCode and Capacity and empty Enrolled and Waitlist lists (no state exists yet).
- Enroll and Drop for VALID ids stay NotImplementedException. No locking.
Run dotnet build, then dotnet test (all tests), quote the real output, STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot edited only EnrollmentService.cs: capacity check in the constructor, a private learner-id normalizer, InvalidRequest results for invalid ids, and a GetSnapshot that returns the configured offering with empty lists. Valid Enroll/Drop remain NotImplemented. Build clean; 12 passed, 0 failed.
- Decision: Accepted
- What I changed: nothing
- Review notes: constructor throws ArgumentException exactly; trim-then-length matches the spec; no test files were edited; GetSnapshot currently returns Array.Empty, so C2 must replace it with real copies.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test` -> [12 passed]; `git diff --stat` -> [only EnrollmentService.cs]
- Commit: `feat(core): implement C1 enrollment validation`

### Entry 7: C2 Phase 1, enrollment capacity tests (Copilot Agent mode)
- Prompt (sanitized):
Slice C2 (enroll while seats are available). Phase 1: tests only.
Files you may create: CourseWaitlist.Tests/EnrollmentCapacityTests.cs. Files you may edit: CourseWaitlist.Tests/Support/TestServiceBuilder.cs (only to add the helper below). Touch nothing else, and do not edit EnrollmentValidationTests.cs or any production file.
- In TestServiceBuilder add an instance method ExpectedSnapshot(string[] enrolled, string[]? waitlist = null) that returns an OfferingSnapshot using the builder's CURRENT offering id, course code and capacity, so tests do not hard-code those values. Keep the existing defaults unchanged.
- Implement T-09, T-10, T-11, T-12, T-13 and T-59 from #file:docs/test-plan.md in EnrollmentCapacityTests. Use xUnit + FluentAssertions 7.x only. Compare snapshots with ShouldMatch and call AssertInvariants on every snapshot. Add [Trait("Slice","C2")] and [Trait("Case","T-xx")] to every test. Names Method_Scenario_ExpectedResult, Arrange/Act/Assert comments.
- T-09: result Outcome is Enrolled and WaitlistPosition is null.
- T-10: Enroll "  alice  " returns Enrolled; the snapshot's enrolled list is exactly ["alice"].
- T-11: capacity 2; Enroll A then B both return Enrolled; snapshot enrolled is exactly ["A","B"] in that order with an empty waitlist.
- T-12: capacity 1; Enroll A returns Enrolled; snapshot enrolled is ["A"] with an empty waitlist.
- T-13: no prerequisite configured; a learner with no completed courses gets Enrolled.
- T-59: a 64-character id (new string('a', 64)) returns Enrolled and appears in the snapshot.
- Do NOT enroll beyond capacity or test the waitlist or prerequisites in this slice.
Run dotnet build, then dotnet test, and quote the real output. Expected: the new tests fail with NotImplementedException from Enroll; the 12 C1 tests still pass. Report any other failure reason. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot added EnrollmentCapacityTests (6 tests, T-09/10/11/12/13/59) and an ExpectedSnapshot helper on TestServiceBuilder. Run: 6 failed (NotImplementedException from Enroll), 12 passed, total 18; build clean.
- Decision: Accepted
- What I changed: nothing
- Review notes: traits present; assertions check outcome, exact snapshot and invariants; no production or C1 test files touched. Gap noted: snapshot-copy behavior has no test yet (optional T-64).
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test --filter Slice=C2` -> [6 failed, all at Enroll stub]; `dotnet test --filter Slice=C1` -> [12 passed]; `git diff --stat` -> [two test files]
- Commit: `test(core): add C2 enrollment capacity tests`

### Entry 8: C2 Phase 2, enroll within capacity (Copilot Agent mode)
- Prompt (sanitized):
Phase 2. Slice C2. Edit only CourseWaitlist.Core/EnrollmentService.cs. Do not edit any test; if you believe a test must change, stop and tell me. Minimum to pass the C2 tests:
- Add a private enrolled list (List<string>).
- In Enroll, after validation, add the trimmed learner id and return EnrollResult(EnrollOutcome.Enrolled, null).
- GetSnapshot returns the configured offering plus a COPY of the enrolled list and an empty waitlist.
- The full-capacity path stays NotImplementedException (not covered by C2 tests): if the enrolled count is already at capacity, throw NotImplementedException.
- Drop for valid ids stays NotImplementedException. No locking, no duplicate handling, no eligibility check yet.
Run dotnet build, then dotnet test (all tests), quote the real output, STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot edited only EnrollmentService.cs: a private enrolled list, Enroll adds the trimmed id and returns Enrolled while a seat is free, GetSnapshot returns a copy of the enrolled list. Full-capacity path and valid Drop remain NotImplemented. Build clean; 18 passed.
- Decision: Accepted
- What I changed: nothing
- Review notes: no duplicate, eligibility, waitlist or locking logic added early; snapshot is a copy; no tests edited. Known intentional gap: repeated Enroll of the same id adds a second entry until slice C5.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test` -> [18 passed]; `git diff --stat` -> [only EnrollmentService.cs]
- Commit: `feat(core): support enrollment within capacity`

### Entry 9: C3 Phase 1, prerequisite tests (Copilot Agent mode)
- Prompt (sanitized):
Slice C3 (prerequisite eligibility). Phase 1: tests only.
Files you may create: CourseWaitlist.Tests/EnrollmentPrerequisiteTests.cs. Touch nothing else, including TestServiceBuilder.cs and all production files.
Implement T-14 and T-15 from #file:docs/test-plan.md with xUnit + FluentAssertions 7.x only. Use the existing TestServiceBuilder (WithPrerequisite, WithCompleted, ExpectedSnapshot), ShouldMatch and AssertInvariants. Add [Trait("Slice","C3")] and [Trait("Case","T-xx")] to every test. Names Method_Scenario_ExpectedResult, Arrange/Act/Assert comments.
- T-14: capacity 1, prerequisite "CS101", learner "A" has NOT completed it. Enroll("A") returns PrerequisiteNotMet with a null WaitlistPosition; the snapshot is unchanged (empty enrolled and waitlist). Then prove the seat is still free: learner "B", who completed CS101, calls Enroll and returns Enrolled.
- T-15: capacity 2, prerequisite "CS101", learner "A" completed CS101 (seeded via WithCompleted). Enroll("A") returns Enrolled and the snapshot enrolled list is ["A"].
Do not test full capacity, duplicates or drops in this slice.
Run dotnet build, then dotnet test, and quote the real output. Expected: T-14 fails with NotImplementedException is NOT acceptable here; it must fail on an assertion (Enroll currently returns Enrolled instead of PrerequisiteNotMet). T-15 may already PASS because no eligibility check exists yet and the learner is eligible; if so, report that explicitly and do not delete or change it. Report any other failure reason. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot added EnrollmentPrerequisiteTests (T-14, T-15). First build failed on a test setup reference (records accessed through the service instead of the builder); Copilot corrected it in the new file and re-ran. Final run: 1 failed, 19 passed, total 20. T-14 failed on the outcome assertion (expected PrerequisiteNotMet, found Enrolled); T-15 passed early.
- Decision: Accepted
- What I changed: nothing
- Review notes: T-15 is an unexpectedly passing test, kept on purpose as a regression guard for eligible enrollment. T-14 never reached the full-capacity stub. No production or other test files changed.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test --filter Case=T-14` -> [failed on outcome assertion]; `--filter Case=T-15` -> [passed]; `git diff --stat` -> [only the new test file]
- Commit: `test(core): add C3 prerequisite tests`

### Entry 10: C3 Phase 2, prerequisite enforcement (Copilot Agent mode)
- Prompt (sanitized):
Phase 2. Slice C3. Edit only CourseWaitlist.Core/EnrollmentService.cs. Do not edit any test; if you believe a test must change, stop and tell me. Minimum to pass the C3 tests:
- Add a private IsEligible(string learnerId): returns true when options.PrerequisiteCourseCode is null, empty or whitespace; otherwise returns _records.HasCompleted(learnerId, options.PrerequisiteCourseCode).
- In Enroll, immediately after validation and BEFORE the capacity check, return EnrollResult(EnrollOutcome.PrerequisiteNotMet, null) when the learner is not eligible. No state changes on that path.
- Everything else stays as is: full-capacity path and valid Drop stay NotImplementedException; no duplicate handling, no waitlist, no locking.
Run dotnet build, then dotnet test (all tests), quote the real output, STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot edited only EnrollmentService.cs: added a private IsEligible (true when no prerequisite is configured, else records.HasCompleted) and an early PrerequisiteNotMet return placed after validation and before the capacity check. Build clean; 20 passed, 0 failed.
- Decision: Accepted
- What I changed: nothing
- Review notes: eligibility is evaluated before capacity, as the spec requires and slice C4 relies on; no state change on the ineligible path; no duplicate, waitlist or lock code added early; no tests edited.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test` -> [20 passed]; `git diff --stat` -> [only EnrollmentService.cs]
- Commit: `feat(core): enforce enrollment prerequisites`

### Entry 11: C4 Phase 1, waitlist tests (Copilot Agent mode)
- Prompt (sanitized):
Slice C4 (join the waitlist when full). Phase 1: tests only.
Files you may create: CourseWaitlist.Tests/EnrollmentWaitlistTests.cs. Touch nothing else, including TestServiceBuilder.cs and all production files.
Implement T-16, T-17, T-18 and T-19 from #file:docs/test-plan.md with xUnit + FluentAssertions 7.x only. Use the existing TestServiceBuilder (WithCapacity, WithPrerequisite, WithCompleted, ExpectedSnapshot), ShouldMatch and AssertInvariants. Add [Trait("Slice","C4")] and [Trait("Case","T-xx")] to every test. Names Method_Scenario_ExpectedResult, Arrange/Act/Assert comments. Call AssertInvariants after every enroll call that has a snapshot taken.
- T-16: capacity 1, no prerequisite. Enroll A returns Enrolled; Enroll B returns Waitlisted with WaitlistPosition 1. Snapshot: enrolled ["A"], waitlist ["B"].
- T-17: capacity 1, no prerequisite. Enroll A, B, C, D in order. A is Enrolled; B, C, D are Waitlisted with positions 1, 2, 3. Snapshot: enrolled ["A"], waitlist ["B","C","D"] in that order.
- T-18: capacity 1, prerequisite "CS101"; A has completed CS101 and B has not (seed with WithCompleted). Enroll A returns Enrolled. Enroll B returns PrerequisiteNotMet with a null WaitlistPosition. Snapshot: enrolled ["A"], waitlist empty.
- T-19: capacity 2, no prerequisite. Enroll 5 distinct learners in order. The first two return Enrolled with null positions; the next three return Waitlisted with positions 1, 2, 3. After EVERY call, call AssertInvariants and assert the snapshot's enrolled count is never above 2. Final snapshot: enrolled has exactly 2 entries, waitlist exactly 3, in enrollment order.
Do not use Drop, duplicates, promotion or concurrency in this slice.
Run dotnet build, then dotnet test, and quote the real output. Expected: T-16, T-17 and T-19 fail with NotImplementedException from Enroll (the full-capacity stub); T-18 may already PASS because eligibility is checked before capacity; if so, report that explicitly and do not delete or change it. Report any other failure reason. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot added EnrollmentWaitlistTests (T-16, T-17, T-18, T-19). Run: 3 failed with NotImplementedException from the full-capacity path, 21 passed, total 24; build clean.
- Decision: Accepted
- What I changed: nothing
- Review notes: T-18 passed early because eligibility is checked before capacity; kept on purpose as a guard that ineligible learners never reach the waitlist. Positions and snapshot lists are asserted exactly; no production files touched.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test --filter Slice=C4` -> [3 failed at the Enroll stub, 1 passed]; `git diff --stat` -> [only the new test file]
- Commit: `test(core): add C4 waitlist tests`

### Entry 12: C4 Phase 2, FIFO waitlist (Copilot Agent mode)
- Prompt (sanitized):
Phase 2. Slice C4. Edit only CourseWaitlist.Core/EnrollmentService.cs. Do not edit any test; if you believe a test must change, stop and tell me. Minimum to pass the C4 tests:
- Add a private nested sealed record WaitlistEntry(string LearnerId, long Sequence), a private List<WaitlistEntry> _waitlist, and a private long _nextSequence (monotonic counter; no timestamps).
- In Enroll, replace the full-capacity NotImplementedException: when _enrolled.Count >= Capacity, append new WaitlistEntry(normalizedLearnerId, _nextSequence++) and return EnrollResult(EnrollOutcome.Waitlisted, the 1-based position, which equals _waitlist.Count after the append).
- GetSnapshot returns copies: the enrolled array and the waitlist learner ids in ascending Sequence order.
- Keep the order: validation, then eligibility, then capacity. Drop for valid ids stays NotImplementedException. No duplicate handling, no locking.
Run dotnet build, then dotnet test (all tests), quote the real output, STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot edited only EnrollmentService.cs: private WaitlistEntry record, a monotonic long sequence counter, append to the waitlist when the offering is full and return Waitlisted with a 1-based position, and a snapshot that projects waitlisted ids in sequence order. Build clean; 24 passed, 0 failed.
- Decision: Accepted
- What I changed: nothing
- Review notes: order of checks (validation, eligibility, capacity) preserved; no timestamps; no duplicate, drop or lock code added early; no tests edited. Known intentional gap: repeated Enroll still creates duplicates until slice C5.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test` -> [24 passed]; `git diff --stat` -> [only EnrollmentService.cs]
- Commit: `feat(core): add FIFO waitlist entries`

### Entry 13: C5 Phase 1, duplicate request tests (Copilot Agent mode)
- Prompt (sanitized):
Slice C5 (idempotent duplicate requests). Phase 1: tests only.
Files you may create: CourseWaitlist.Tests/EnrollmentDuplicateTests.cs. Touch nothing else, including TestServiceBuilder.cs and all production files.
Implement T-20, T-21, T-22, T-23, T-24 and T-60 from #file:docs/test-plan.md with xUnit + FluentAssertions 7.x only. Use the existing TestServiceBuilder (WithCapacity, WithPrerequisite, WithCompleted, Records, ExpectedSnapshot), ShouldMatch and AssertInvariants. Add [Trait("Slice","C5")] and [Trait("Case","T-xx")] to every test. Names Method_Scenario_ExpectedResult, Arrange/Act/Assert comments. Assert the OUTCOME first, then the snapshot. Never call Drop.
Write exactly these 8 tests:
1. T-20: capacity 2, no prerequisite. Enroll A returns Enrolled; Enroll A again returns AlreadyEnrolled with a null WaitlistPosition. Snapshot enrolled ["A"], waitlist empty.
2. T-21: capacity 1, no prerequisite. Enroll A (Enrolled), B (Waitlisted position 1), C (Waitlisted position 2). Enroll B again returns AlreadyWaitlisted with position 1; Enroll C again returns AlreadyWaitlisted with position 2. Snapshot enrolled ["A"], waitlist ["B","C"] (one record each).
3. T-22 (enrolled variants): capacity 2, no prerequisite. Enroll " Alice " returns Enrolled; Enroll "alice" and Enroll "ALICE" each return AlreadyEnrolled. Snapshot enrolled ["Alice"] (trimmed, first-seen casing), waitlist empty.
4. T-22 (waitlisted variants): capacity 1, no prerequisite. Enroll A (Enrolled), " Bob " (Waitlisted position 1); Enroll "bob" and "BOB" each return AlreadyWaitlisted at position 1. Snapshot enrolled ["A"], waitlist ["Bob"].
5. T-23: capacity 1, prerequisite "CS101", A completed it (WithCompleted). Enroll A returns Enrolled. Then builder.Records.Remove("A","CS101"). Enroll A again returns AlreadyEnrolled (NOT PrerequisiteNotMet). Snapshot enrolled ["A"].
6. T-24 (enrolled duplicate): capacity 2, no prerequisite. Enroll A, take a snapshot, Enroll A again, take another; the second ShouldMatch the first.
7. T-24 (waitlisted duplicate): capacity 1, no prerequisite. Enroll A, B (waitlisted), take a snapshot, Enroll B again, take another; the second ShouldMatch the first.
8. T-60: capacity 1, prerequisite "CS101"; A and B both completed it. Enroll A (Enrolled), B (Waitlisted position 1). Then builder.Records.Remove("B","CS101"). Enroll B again returns AlreadyWaitlisted at position 1 (NOT PrerequisiteNotMet). Snapshot enrolled ["A"], waitlist ["B"].
Run dotnet build, then dotnet test, and quote the real output. Expected: all 8 new tests FAIL on assertions (for example "expected AlreadyEnrolled but found Enrolled/Waitlisted" or "found PrerequisiteNotMet"), not on NotImplementedException and not on compile errors. If any new test passes, report it explicitly and do not delete or change it. Report any other failure reason. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot added EnrollmentDuplicateTests with eight tests (T-20, T-21, T-22 x2, T-23, T-24 x2, T-60). Run: 8 failed, 24 passed, total 32; build clean. All failures were outcome assertions (found Enrolled, Waitlisted or PrerequisiteNotMet instead of AlreadyEnrolled or AlreadyWaitlisted); none hit stubs.
- Decision: Accepted
- What I changed: nothing
- Review notes: T-23 and T-60 guard the rule that the duplicate check runs before the eligibility check. Snapshot-unchanged assertions in T-24 only become effective after Phase 2 (the outcome assertion fails first). No unexpectedly passing tests. No production files touched.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test --filter Slice=C5` -> [8 failed, reasons checked]; `git diff --stat` -> [only the new test file]
- Commit: `test(core): add C5 duplicate enrollment tests`

### Entry 14: C5 Phase 2, idempotent duplicates (Copilot Agent mode)
- Prompt (sanitized):
Phase 2. Slice C5. Edit only CourseWaitlist.Core/EnrollmentService.cs. Do not edit any test; if you believe a test must change, stop and tell me. Minimum to pass the C5 tests:
- In Enroll, immediately after validation and BEFORE the eligibility check, add the duplicate checks using StringComparer.OrdinalIgnoreCase on the already trimmed id:
  1. If the learner is in _enrolled, return EnrollResult(EnrollOutcome.AlreadyEnrolled, null).
  2. If the learner is in _waitlist, return EnrollResult(EnrollOutcome.AlreadyWaitlisted, position), where position is the entry's zero-based index in _waitlist plus 1 (entries are only appended, so list order equals sequence order). Do NOT use _waitlist.Count for this.
- Neither duplicate path changes any state, and the stored id keeps its first-submitted trimmed casing.
- Keep the order: validation, duplicates, eligibility, capacity. Drop for valid ids stays NotImplementedException. No FillFreeSeats, no locking.
Run dotnet build, then dotnet test (all tests), quote the real output, STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot edited only EnrollmentService.cs: after validation and before the eligibility check, Enroll now finds an existing enrolled or waitlisted learner (OrdinalIgnoreCase on the trimmed id) and returns AlreadyEnrolled, or AlreadyWaitlisted with the entry's index plus one, without mutating state. Build clean; 32 passed, 0 failed.
- Decision: Accepted
- What I changed: nothing
- Review notes: check order is validation, duplicates, eligibility, capacity (required by T-23 and T-60); waitlist position derived from list index, not Count; first-seen casing preserved; no Drop, lock or FillFreeSeats code added early; no tests edited.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test` -> [32 passed]; `git diff --stat` -> [only EnrollmentService.cs]
- Commit: `feat(core): make duplicate enrollments idempotent`

### Entry 15: C6 Phase 1, drop tests (Copilot Agent mode)
- Prompt (sanitized):
Slice C6 (drop, without promotion). Phase 1: tests only.
Files you may create: CourseWaitlist.Tests/DropTests.cs. Touch nothing else, including TestServiceBuilder.cs and all production files.
Implement T-25, T-26, T-27, T-28 and T-29 from #file:docs/test-plan.md with xUnit + FluentAssertions 7.x only. Use the existing TestServiceBuilder (WithCapacity, WithPrerequisite, ExpectedSnapshot), ShouldMatch and AssertInvariants. Add [Trait("Slice","C6")] and [Trait("Case","T-xx")] to every test. Names Method_Scenario_ExpectedResult, Arrange/Act/Assert comments. Assert the OUTCOME first, then the snapshot. Use no prerequisite in any test.
Write exactly these 6 tests:
1. T-25: capacity 1. Enroll A. Drop A returns Dropped and PromotedLearnerIds is empty. Snapshot enrolled and waitlist both empty. Then Enroll B returns Enrolled (the seat is reusable) and the snapshot enrolled is ["B"].
2. T-26: capacity 2. Enroll A. Drop Z (never enrolled) returns NotEnrolled and PromotedLearnerIds is empty. Snapshot enrolled ["A"], waitlist empty (unchanged).
3. T-27: capacity 2. Enroll A. Drop A returns Dropped. Take a snapshot. Drop A again returns NotEnrolled with an empty PromotedLearnerIds; the second snapshot ShouldMatch the first.
4. T-28: capacity 1. Enroll A (Enrolled), B (Waitlisted position 1), C (Waitlisted position 2). Drop B returns RemovedFromWaitlist with an empty PromotedLearnerIds. Snapshot enrolled ["A"], waitlist ["C"]. Then Enroll C again returns AlreadyWaitlisted with position 1 (positions shifted).
5. T-29 (enrolled variant): capacity 2. Enroll "Alice". Drop "  ALICE " returns Dropped. Snapshot enrolled empty.
6. T-29 (waitlisted variant): capacity 1. Enroll A, then "Bob" (Waitlisted position 1). Drop " bob " returns RemovedFromWaitlist. Snapshot enrolled ["A"], waitlist empty.
IMPORTANT: no test may Drop an enrolled learner while the waitlist is non-empty. Promotion belongs to slice C7.
Run dotnet build, then dotnet test, and quote the real output. Expected: all 6 new tests FAIL with NotImplementedException thrown from Drop; the existing 32 tests still pass. Report any other failure reason or any new test that passes, and do not delete or change it. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot added DropTests with six tests (T-25, T-26, T-27, T-28, T-29 x2). Run: 6 failed with NotImplementedException from Drop, 32 passed, total 38; build clean.
- Decision: Accepted
- What I changed: nothing
- Review notes: no test drops an enrolled learner while the waitlist is non-empty (promotion belongs to C7); later assertions (seat reuse, position shift) only become effective after Phase 2. No unexpectedly passing tests.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test --filter Slice=C6` -> [6 failed at the Drop stub]; `git diff --stat` -> [only the new test file]
- Commit: `test(core): add C6 drop tests`

### Entry 16: C6 Phase 2, drop implementation (Copilot Agent mode)
- Prompt (sanitized):
Phase 2. Slice C6. Edit only CourseWaitlist.Core/EnrollmentService.cs. Do not edit any test; if you believe a test must change, stop and tell me. Minimum to pass the C6 tests. Replace the NotImplementedException in Drop (valid ids only) with:
1. If the normalized id matches an entry in _enrolled (StringComparer.OrdinalIgnoreCase), remove it and return DropResult(DropOutcome.Dropped, an empty list).
2. Else if it matches an entry in _waitlist (OrdinalIgnoreCase), remove that entry and return DropResult(DropOutcome.RemovedFromWaitlist, an empty list). Remaining entries keep their order and their Sequence values.
3. Else return DropResult(DropOutcome.NotEnrolled, an empty list) with no state change.
Do NOT promote anyone, do NOT add FillFreeSeats, and do NOT change Enroll or add locking; promotion is slice C7. Keep the InvalidRequest path as is.
Run dotnet build, then dotnet test (all tests), quote the real output, STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot edited only EnrollmentService.cs: Drop now removes an enrolled learner (Dropped), else removes a matching waitlist entry (RemovedFromWaitlist), else returns NotEnrolled; OrdinalIgnoreCase on trimmed ids; no promotion. Build clean; 38 passed, 0 failed.
- Decision: Accepted
- What I changed: nothing
- Review notes: remaining waitlist entries keep order and Sequence values; NotEnrolled changes no state; Enroll untouched; no lock or promotion code added early. Known intermediate state: invariant 5 is not yet enforced (no promotion until C7, no reconciliation in Enroll until C8); tests T-30 to T-38 are planned and must fail first. Possible later refactor: Enroll and Drop duplicate the same lookups.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test` -> [38 passed]; `git diff --stat` -> [only EnrollmentService.cs]
- Commit: `feat(core): support dropping enrollments and waitlist entries`

### Entry 17: C7 Phase 1, FIFO promotion tests (Copilot Agent mode)
- Prompt (sanitized):
Slice C7 (FIFO promotion on drop). Phase 1: tests only.
Files you may create: CourseWaitlist.Tests/PromotionTests.cs. Touch nothing else, including TestServiceBuilder.cs and all production files.
Implement T-30, T-31, T-32 and T-61 from #file:docs/test-plan.md with xUnit + FluentAssertions 7.x only. Use the existing TestServiceBuilder (WithCapacity, WithPrerequisite, ExpectedSnapshot), ShouldMatch and AssertInvariants. Add [Trait("Slice","C7")] and [Trait("Case","T-xx")] to every test. Names Method_Scenario_ExpectedResult, Arrange/Act/Assert comments. Assert the Drop OUTCOME first, then PromotedLearnerIds (use .Should().Equal(...)), then the snapshot. Use no prerequisite in any test (every waiter is eligible; eligibility during promotion is slice C8). Do not use concurrency.
Write exactly these 4 tests:
1. T-30: capacity 1. Enroll A (Enrolled), B (Waitlisted position 1), C (Waitlisted position 2). Drop A returns Dropped with PromotedLearnerIds ["B"]. Snapshot enrolled ["B"], waitlist ["C"].
2. T-31: capacity 1. Enroll A, B, C, D (A enrolled; B, C, D waitlisted). Drop A returns Dropped with PromotedLearnerIds ["B"]. Then Drop B returns Dropped with PromotedLearnerIds ["C"]. Final snapshot enrolled ["C"], waitlist ["D"].
3. T-32: capacity 1. Enroll A, B, C. Drop B returns RemovedFromWaitlist with empty PromotedLearnerIds. Drop A returns Dropped with PromotedLearnerIds ["C"] (B is NOT promoted). Snapshot enrolled ["C"], waitlist empty.
4. T-61: capacity 1. Enroll A, B, C. Drop A returns Dropped with PromotedLearnerIds ["B"]. Then Enroll C again returns AlreadyWaitlisted at position 1 (not 2). Snapshot enrolled ["B"], waitlist ["C"].
Run dotnet build, then dotnet test, and quote the real output. Expected: all 4 new tests FAIL on assertions (for example PromotedLearnerIds expected ["B"] but found empty), not on NotImplementedException and not on compile errors; the existing 38 tests still pass. Report any other failure reason or any new test that passes, and do not delete or change it. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot added PromotionTests with four tests (T-30, T-31, T-32, T-61). Run: 4 failed on assertions (PromotedLearnerIds empty instead of the expected earliest waiter), 38 passed, total 42; build clean.
- Decision: Accepted
- What I changed: nothing
- Review notes: T-32 fails only at the second drop (first drop's RemovedFromWaitlist assertions pass), isolating the missing promotion. No prerequisites or concurrency used; no unexpectedly passing tests; no production files touched.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test --filter Slice=C7` -> [4 failed, all on PromotedLearnerIds]; `git diff --stat` -> [only the new test file]
- Commit: `test(core): add C7 FIFO promotion tests`

### Entry 18: C7 Phase 2, FIFO promotion on drop (Copilot Agent mode)
- Prompt (sanitized):
Phase 2. Slice C7. Edit only CourseWaitlist.Core/EnrollmentService.cs. Do not edit any test; if you believe a test must change, stop and tell me. Minimum to pass the C7 tests:
- Add a private method FillFreeSeats() that returns the learner ids it promoted, as a new list in promotion order. While _enrolled.Count < _options.Capacity and _waitlist.Count > 0: take the head entry of _waitlist (index 0; entries are appended in ascending Sequence order), remove it from _waitlist, add its LearnerId to _enrolled, and record the id.
- In Drop, call FillFreeSeats() ONLY in the branch that just removed an ENROLLED learner, and return DropResult(DropOutcome.Dropped, the promoted ids).
- Do NOT call FillFreeSeats from the waitlist-removal branch, from the NotEnrolled path, or from Enroll. Do NOT add any eligibility check inside FillFreeSeats; eligibility during promotion is slice C8.
- No locking, no other refactoring. Keep Enroll unchanged.
Run dotnet build, then dotnet test (all tests), quote the real output, STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot edited only EnrollmentService.cs: added a private FillFreeSeats() that moves the head of the waitlist into the enrolled list while a seat is free and returns the promoted ids, called only after an enrolled learner is removed. Build clean; 42 passed, 0 failed.
- Decision: Accepted
- What I changed: nothing
- Review notes: single call site; no eligibility check inside FillFreeSeats; Enroll unchanged; no lock; no tests edited. Known intermediate state: promotion ignores eligibility and Enroll does not reconcile free seats until C8; T-33 to T-38 are planned and must fail first.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test` -> [42 passed]; `git diff --stat` -> [only EnrollmentService.cs]
- Commit: `feat(core): promote waitlisted learners on drop`

### Entry 19: C8 Phase 1, eligibility during promotion tests (Copilot Agent mode)
- Prompt (sanitized):
Slice C8 (eligibility during promotion: skip and retain, no queue jumping). Phase 1: tests only.
Files you may create: CourseWaitlist.Tests/PromotionEligibilityTests.cs. Touch nothing else, including TestServiceBuilder.cs, InvariantAssertions.cs and all production files.
Implement T-33, T-34, T-35, T-36, T-37 and T-38 from #file:docs/test-plan.md with xUnit + FluentAssertions 7.x only. Use TestServiceBuilder (WithCapacity, WithPrerequisite, WithCompleted, Records, ExpectedSnapshot), ShouldMatch and AssertInvariants. Add [Trait("Slice","C8")] and [Trait("Case","T-xx")] to every test. Names Method_Scenario_ExpectedResult, Arrange/Act/Assert comments. Assert outcomes first (use .Should().Equal(...) for PromotedLearnerIds), then snapshots. Every test uses capacity 1, WithPrerequisite("CS101"), and changes eligibility with builder.Records.Remove(learnerId, "CS101") and builder.Records.Add(learnerId, "CS101") AFTER learners joined. Do not use concurrency.
Write exactly these 6 tests. Assert EVERY intermediate step listed; do not skip any, because later steps could pass for the wrong reason:
1. T-33: A, B, C completed CS101. Enroll A (Enrolled), B (Waitlisted 1), C (Waitlisted 2). Remove B's CS101. Drop A returns Dropped with PromotedLearnerIds ["C"] (B skipped). Snapshot enrolled ["C"], waitlist ["B"].
2. T-34: A, B, C, D completed. Enroll A, B, C, D. Remove B's CS101. Drop A returns Dropped with PromotedLearnerIds ["C"]; snapshot enrolled ["C"], waitlist ["B","D"]. Add B's CS101 back. Drop C returns Dropped with PromotedLearnerIds ["B"]; snapshot enrolled ["B"], waitlist ["D"].
3. T-35: A, B completed. Enroll A, B (Waitlisted 1). Remove B's CS101. Drop A returns Dropped with EMPTY PromotedLearnerIds. Snapshot enrolled empty, waitlist ["B"] (seat stays free).
4. T-36: A, B, C completed. Enroll A, B (Waitlisted 1). Remove B's CS101. Drop A returns Dropped with EMPTY PromotedLearnerIds; snapshot enrolled empty, waitlist ["B"]. Add B's CS101 back. Enroll C returns Waitlisted with position 1 (NOT Enrolled). Snapshot enrolled ["B"], waitlist ["C"].
5. T-37: A, B completed. Enroll A, B (Waitlisted 1). Remove B's CS101. Drop A returns Dropped with EMPTY PromotedLearnerIds; snapshot enrolled empty, waitlist ["B"]. Add B's CS101 back. Enroll B returns AlreadyEnrolled with null WaitlistPosition. Snapshot enrolled ["B"], waitlist empty.
6. T-38: A, B completed; C has NOT completed CS101. Enroll A, B (Waitlisted 1). Remove B's CS101. Drop A returns Dropped with EMPTY PromotedLearnerIds; snapshot enrolled empty, waitlist ["B"]. Add B's CS101 back. Enroll C returns PrerequisiteNotMet with null WaitlistPosition. Snapshot enrolled ["B"], waitlist empty (C is in neither list).
Run dotnet build, then dotnet test, and quote the real output. Expected: all 6 new tests FAIL on assertions (for example PromotedLearnerIds expected empty or ["C"] but found ["B"]), not on NotImplementedException and not on compile errors; the existing 42 tests still pass. If any new test passes, report it explicitly and do not delete or change it. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot added PromotionEligibilityTests with six tests (T-33 to T-38). Run: 6 failed on assertions (ineligible head waiter B was promoted), 42 passed, total 48; build clean.
- Decision: Accepted
- What I changed: nothing
- Review notes: I required every intermediate state to be asserted (seat free with the skipped waiter retained) so T-37 and T-38 could not pass by accident; traced each test against the intended FillFreeSeats-first behavior. T-36 to T-38 encode the free-seat defect found during spec review. No unexpectedly passing tests; no production files touched.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test --filter Slice=C8` -> [6 failed, all at the first Drop]; `git diff --stat` -> [only the new test file]
- Commit: `test(core): add C8 promotion eligibility tests`

### Entry 20: C8 Phase 2, skip-and-retain promotion (Copilot Agent mode)
- Prompt (sanitized):
Phase 2. Slice C8. Edit only CourseWaitlist.Core/EnrollmentService.cs. Do not edit any test; if you believe a test must change, stop and tell me. Do not change the XML-doc or other comments. Minimum to pass the C8 tests:
- Change FillFreeSeats: while _enrolled.Count < _options.Capacity, find the FIRST waitlist entry (lowest index) whose learner IsEligible right now; if there is none, stop; otherwise remove that entry from _waitlist, add its LearnerId to _enrolled and record the id. Ineligible entries are skipped WITHOUT being removed, reordered, or having their Sequence changed. Return the promoted ids in promotion order. The loop must always terminate.
- In Enroll, call FillFreeSeats() immediately after validation succeeds and BEFORE the enrolled and waitlist duplicate checks; ignore its return value.
- Keep Drop calling FillFreeSeats only in the branch that just removed an ENROLLED learner. Do NOT call it in the waitlist-removal or NotEnrolled branches.
- Keep the order of the remaining Enroll checks unchanged: duplicates, eligibility, capacity. No locking and no other refactoring.
Run dotnet build, then dotnet test (all tests), quote the real output, STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot edited only EnrollmentService.cs: FillFreeSeats now promotes the first currently eligible waiter, leaving ineligible entries in place; Enroll calls FillFreeSeats right after validation and before the duplicate checks; Drop still reconciles only after removing an enrolled learner. Build clean; 48 passed, 0 failed.
- Decision: Accepted
- What I changed: nothing
- Review notes: two call sites for FillFreeSeats; skipped entries keep index and Sequence; loop terminates; no lock; no tests edited; XML-doc left untouched as instructed. Design finding: invariant 5 holds after every validated Enroll and every enrolled-learner Drop, but not after a Drop of a waitlisted learner; the invariant wording says "every Drop that removes a learner", which is too strong. Fixed in a separate docs step.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test` -> [48 passed]; `git diff --stat` -> [only EnrollmentService.cs]
- Commit: `feat(core): skip ineligible waiters during promotion`

### Entry 21: C9 Phase 1, concurrency tests (Copilot Agent mode)
- Prompt (sanitized):
Slice C9 concurrency. Phase 1: tests only.
Files you may create: CourseWaitlist.Tests/ConcurrencyTests.cs. Files you may edit: CourseWaitlist.Tests/Support/InvariantAssertions.cs (ADD one method only; do not change AssertInvariants). Touch nothing else, and do not touch any production file.
- Add InvariantAssertions.AssertNoFreeSeatBesideEligibleWaiter(OfferingSnapshot snapshot, Func<string, bool> isEligible): when snapshot.Enrolled.Count < snapshot.Capacity, no learner in snapshot.Waitlist may satisfy isEligible.
- In ConcurrencyTests (xUnit + FluentAssertions 7.x only; [Trait("Slice","C9")] and [Trait("Case","T-39".."T-42")]; names Method_Scenario_ExpectedResult) add a private static helper RunConcurrently(IReadOnlyList<Action> actions): create one task per action with Task.Factory.StartNew(..., TaskCreationOptions.LongRunning) so each gets a dedicated thread; every task waits on one shared ManualResetEventSlim gate, then runs its action inside try/catch and adds any exception to a ConcurrentBag<Exception>; set the gate after ALL tasks are created; await Task.WhenAll with a 30-second timeout and fail the test on timeout; return the exceptions. No Thread.Sleep. Each action writes its result into its own slot of a pre-sized array (index = task index).
- Every test uses a fresh service per iteration, no prerequisite (everyone eligible), and repeats the scenario 30 times in a for loop, putting the iteration number in assertion messages. In every test assert FIRST that the exception bag is empty.
Write exactly these 4 tests:
1. T-39: capacity 10; 100 distinct ids L001..L100 call Enroll concurrently. Exactly 10 return Enrolled and 90 Waitlisted; the 90 waitlist positions are exactly the set 1..90 with no repeats; the snapshot has 10 enrolled and 90 waitlisted, AssertInvariants passes, and all 100 ids appear exactly once across both lists.
2. T-40: capacity 10; 50 concurrent Enroll("A"). Exactly one Enrolled and 49 AlreadyEnrolled; snapshot enrolled ["A"], waitlist empty.
3. T-41: capacity 10. Sequentially set up E01..E10 (all Enrolled) and W01..W40 (all Waitlisted). Then run concurrently: 10 Drops of E01..E10, 40 Enrolls of N01..N40, and 4 reader actions that each call GetSnapshot 200 times and call AssertInvariants on every snapshot. Assert: every Drop returned Dropped; every N Enroll returned Waitlisted; final enrolled is exactly W01..W10 in that order; final waitlist is W11..W40 in order followed by the 40 N ids in any order; waitlist count is 70; AssertInvariants and AssertNoFreeSeatBesideEligibleWaiter(snapshot, _ => true) pass.
4. T-42: capacity 1; sequentially Enroll A (Enrolled) and B (Waitlisted). Then 20 concurrent Drop("A"). Exactly one Dropped and 19 NotEnrolled; the concatenation of all PromotedLearnerIds across the 20 results is exactly ["B"]; snapshot enrolled ["B"], waitlist empty.
The service has NO locking yet, so these tests are expected to FAIL. For this slice, race symptoms are expected reasons: assertion failures (wrong counts, repeated positions, duplicates) and exceptions thrown inside the service by races (InvalidOperationException, ArgumentException, IndexOutOfRangeException, ArgumentOutOfRangeException) reported through the exception bag. A compile error, a timeout without a race explanation, or NotImplementedException is NOT acceptable.
Run dotnet build, then run dotnet test THREE times sequentially (never in parallel) and quote each real result. The existing 48 tests must pass in every run. If any new C9 test passes in all three runs, report it explicitly, then increase contention in that test only (more iterations or more tasks) and tell me what you changed. Keep total C9 runtime under about 15 seconds. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot added ConcurrencyTests (T-39 to T-42, 30 iterations each, barrier-started threads) and one new helper in InvariantAssertions. A compile error in T-42 (Equal overload) was fixed in the new file. First three full runs: 3, 2 and 4 failures out of the 4 new tests; existing 48 passed each time.
- Decision: Edited
- Review finding: first red evidence was flaky (3 of 4 C9 tests failing on average across 3 runs). Per-test 10-run table: T-39 10/10, T-41 10/10, T-40 4/10, T-42 1/10. Cause: tiny race windows in T-40 and T-42 plus a blocking start gate. Follow-up: spin-gate start and more iterations (T-40 300, T-42 1000).
- Verification (my own runs): 10-run per-test failure table: T-39 10/10, T-41 10/10, T-40 4/10, T-42 1/10; `git diff --stat`: [two test files]
- Commit: `test(core): add C9 concurrency tests`

### Entry 22: C9 Phase 2, single lock (Copilot Agent mode)
- Prompt (sanitized):
Phase 2. Slice C9 concurrency. Edit only CourseWaitlist.Core/EnrollmentService.cs. Do not edit any test; if you believe a test must change, stop and tell me. Minimum to pass the C9 tests:
- Add ONE private readonly object field named _gate with the single-line comment: "Single lock guards all mutable state; eligibility is evaluated inside it."
- Wrap the entire bodies of the public methods Enroll, Drop and GetSnapshot in lock (_gate).
- Do not add any other synchronization primitive, do not use async, do not change FillFreeSeats, IsEligible, the XML-doc or any other line.
Run dotnet build, then run `dotnet test --no-build` three times sequentially and quote each real result. Expected: 52 passed, 0 failed in every run. Report any failure by test name. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot edited only EnrollmentService.cs: added one private lock object and wrapped the bodies of Enroll, Drop and GetSnapshot in it. Build clean; three sequential full runs: 52 passed, 0 failed (about 25 s each).
- Decision: Accepted
- What I changed: nothing
- Review notes: every access to shared state is inside the three public methods or helpers called from them; no other primitive or async; XML-doc untouched; no tests edited. Lock order is service lock then the records' internal lock, which is deadlock-free because the records type never calls back.
- Verification (my own runs): locked 10-run green: [10/10 runs passed 52]; `git diff` -> [one field and three lock wrappers]
- Commit: `feat(core): serialize service operations with a single lock`

### Entry 23: A0 Phase 1, API composition tests (Copilot Agent mode)
- Prompt (sanitized):
Slice A0 (API composition and config validation). Phase 1: tests only.
Files you may create: CourseWaitlist.Tests/Api/ApiFactory.cs and CourseWaitlist.Tests/Api/CompositionTests.cs. Touch nothing else, including all production files.
- ApiFactory: a class deriving from WebApplicationFactory<Program>. Its constructor takes an optional IDictionary<string, string?> overrides. In ConfigureWebHost use ConfigureAppConfiguration to add an in-memory collection: defaults Offering:Id=CS201-TEST, Offering:CourseCode=CS201, Offering:Capacity=2, Offering:PrerequisiteCourseCode=CS101, LearnerRecords:learner-001:0=CS101, LearnerRecords:learner-002:0=CS101, LearnerRecords:learner-003:0=CS101 (learner-004 has no courses). Apply the overrides AFTER the defaults so they win. Use UseEnvironment("Testing").
- Implement T-43, T-44 and T-45 from #file:docs/test-plan.md in CompositionTests with xUnit + FluentAssertions 7.x only. Use `using var factory` in each test. Add [Trait("Slice","A0")] and [Trait("Case","T-xx")]. Names Method_Scenario_ExpectedResult, Arrange/Act/Assert comments.
  - T-43: default factory. Resolve EnrollmentService from factory.Services (GetRequiredService) and call GetSnapshot: OfferingId "CS201-TEST", CourseCode "CS201", Capacity 2, empty Enrolled and empty Waitlist.
  - T-44: override Offering:Capacity = "0". Calling factory.CreateClient() must throw; assert the thrown exception, or any exception in its InnerException chain, is a Microsoft.Extensions.Options.OptionsValidationException (write a small helper that walks the chain).
  - T-45: override Offering:Id = "" (blank stands in for a missing id). factory.CreateClient() must throw with the same chain assertion.
Do not test any HTTP endpoint in this slice.
Run dotnet build, then dotnet test, and quote the real output. Expected: T-43 fails with InvalidOperationException saying EnrollmentService is not registered (service registration is the behavior under test); T-44 and T-45 fail with "Expected a <Exception> to be thrown, but no exception was thrown"; the existing 52 tests still pass. Report any other failure reason, including compile errors, or any new test that passes, and do not delete or change it. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot added ApiFactory (WebApplicationFactory with layered in-memory config) and CompositionTests (T-43, T-44, T-45). Run: 3 failed, 52 passed, total 55; build clean. T-43 failed on the unregistered EnrollmentService; T-44 and T-45 failed with "no exception thrown".
- Decision: Accepted
- What I changed: nothing
- Review notes: the failures prove the host starts with the template Program and the test configuration; overrides are applied after defaults; no endpoint tests in this slice; no production files touched.
- Verification (my own runs): `dotnet test --filter Slice=A0` -> [3 failed, reasons checked]; `git diff --stat` -> [two test files]
- Commit: `test(api): add A0 composition tests`

### Entry 24: A0 Phase 2, API composition (Copilot Agent mode)
- Prompt (sanitized):
Phase 2. Slice A0. Files you may create or edit: CourseWaitlist.Api/Program.cs and CourseWaitlist.Api/appsettings.json (create it; the scaffold removed it). Touch nothing else, including the .csproj files. Do not edit any test; if you believe a test must change, stop and tell me. Minimum to pass the A0 tests, with NO endpoints:
- Keep `public partial class Program { }` and leave the template's existing root endpoint as it is.
- Bind OfferingOptions lazily: builder.Services.AddOptions<OfferingOptions>().BindConfiguration("Offering").Validate(o => !string.IsNullOrWhiteSpace(o.Id) && !string.IsNullOrWhiteSpace(o.CourseCode) && o.Capacity >= 1, "Offering requires Id, CourseCode and Capacity >= 1.").ValidateOnStart(). Do NOT read builder.Configuration eagerly anywhere in Program.cs, because test overrides are applied later.
- Register InMemoryLearnerRecords as a singleton with a factory lambda: take IConfiguration from the service provider; for each child of the "LearnerRecords" section, treat the child's Key as the learner id and each of its children's Value as a completed course code (skip null values) and call records.Add(learnerId, courseCode). Also register ILearnerRecords to resolve to that same InMemoryLearnerRecords instance.
- Register EnrollmentService as a singleton with a factory lambda: new EnrollmentService(sp.GetRequiredService<IOptions<OfferingOptions>>().Value, sp.GetRequiredService<ILearnerRecords>()).
- appsettings.json demo values: Offering { Id: "CS201-2026-FALL", CourseCode: "CS201", Capacity: 2, PrerequisiteCourseCode: "CS101" }; LearnerRecords { "learner-001": ["CS101"], "learner-002": ["CS101"], "learner-003": ["CS101"], "learner-004": [] }; standard Logging section.
No locking, no endpoints, no JSON options, no other packages.
Run dotnet build, then dotnet test (all tests) and quote the real output, STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot edited Program.cs (lazy options binding with validation and ValidateOnStart, singleton learner records seeded from configuration, singleton EnrollmentService) and created appsettings.json with demo values. Build clean; 55 passed, 0 failed.
- Decision: Accepted
- What I changed: nothing
- Review notes: no eager configuration reads, so test overrides apply (T-43 shows the test id instead of the demo id); both record registrations resolve to one instance; template root endpoint intentionally left for A1; no csproj changes.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test --filter Slice!=C9` -> [passed]; `dotnet run` with the demo config -> [started]; with Offering__Capacity=0 -> [failed at startup with OptionsValidationException]
- Commit: `feat(api): compose offering enrollment services`

### Entry 25: A1 Phase 1, POST enroll endpoint tests (Copilot Agent mode)
- Prompt (sanitized):
Slice A1 (POST /offerings/{offeringId}/enrollments). Phase 1: tests only.
Files you may create: CourseWaitlist.Tests/Api/EnrollEndpointTests.cs. Touch nothing else, including ApiFactory.cs and all production files.
Implement T-46 to T-52 from #file:docs/test-plan.md with xUnit + FluentAssertions 7.x only. Use ApiFactory (default configuration: offering CS201-TEST, capacity 2, prerequisite CS101; learner-001, learner-002 and learner-003 completed it; learner-004 did not). Create `using var factory = new ApiFactory();` and `factory.CreateClient()` INSIDE each test (fresh state per test). Do not reference any type from CourseWaitlist.Api; read bodies with ReadFromJsonAsync<JsonElement>() (System.Net.Http.Json). Post JSON with PostAsJsonAsync or StringContent("...", Encoding.UTF8, "application/json"). Add [Trait("Slice","A1")] and [Trait("Case","T-xx")]. Names Method_Scenario_ExpectedResult, Arrange/Act/Assert comments. URL base: /offerings/CS201-TEST/enrollments.
Response contract to assert (see docs/design-spec.md section 3): JSON properties are camelCase; enums serialize as strings; `outcome`, `learnerId`; `waitlistPosition` is present only when it applies and omitted otherwise; ProblemDetails responses use media type application/problem+json and contain `status`.
Write these tests:
1. T-46: POST {"learnerId":"learner-001"} -> 201; outcome "Enrolled"; learnerId "learner-001"; the property waitlistPosition is absent (TryGetProperty returns false).
2. T-47: POST learner-001, learner-002, then learner-003 -> the third returns 202; outcome "Waitlisted"; waitlistPosition 1; learnerId "learner-003".
3. T-48: POST learner-001 twice -> the second returns 200; outcome "AlreadyEnrolled".
4. T-49: POST learner-001, learner-002, learner-003, then learner-003 again -> the repeat returns 200; outcome "AlreadyWaitlisted"; waitlistPosition 1.
5. T-50: POST learner-004 -> 422; content type application/problem+json; body status 422 and outcome "PrerequisiteNotMet".
6. T-51: a [Theory] with raw request bodies: "" (empty body sent with content type application/json), "{}", {"learnerId":null}, {"learnerId":""}, {"learnerId":"   "} -> each returns 400; content type application/problem+json; body status 400 and outcome "InvalidRequest".
7. T-52: POST {"learnerId":"learner-001"} to /offerings/UNKNOWN/enrollments -> 404; content type application/problem+json; body status 404.
Do not test DELETE or GET, and do not call any service type directly in this slice.
Run dotnet build, then dotnet test, and quote the real output. Expected: ALL 11 new test cases FAIL on assertions (for example "Expected status 201 but found 404"; T-52 fails on the missing application/problem+json content type), not on compile errors; the existing 55 tests still pass. Report any other failure reason or any new test that passes, and do not delete or change it. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot added EnrollEndpointTests (T-46 to T-52, 11 cases). The first build failed (JsonElement wrapped in a using); fixed in the new file. Copilot also added a body-presence assertion to T-52 after observing a JSON parse exception. Run: 11 failed, 55 passed, total 66.
- Decision: Edited
- Review finding: the content-type assertions in T-50, T-51 and T-52 used `?.`, which skips the assertion when the header is absent. Copilot said the unmapped route returned problem+json; the follow-up run showed T-52 failing with "ContentType not to be <null>", so the header was absent and the claim was unsupported. I replaced the three assertions with NotBeNull plus a media-type check. T-50 and T-51 fail at the status check, so their content-type assertions are only exercised once the endpoint exists.
- Verification (my own runs): `dotnet test --filter Case=T-52` -> [message above]; `dotnet test --filter Slice!=C9` -> [11 failed, 41 passed]; `git diff --stat` -> [only the new test file]
- Commit: `test(api): add A1 enroll endpoint tests`

### Entry 26: A1 Phase 2, POST enrollment endpoint (Copilot Agent mode)
- Prompt (sanitized):
Phase 2. Slice A1. Files you may create or edit: CourseWaitlist.Api/Program.cs, CourseWaitlist.Api/Endpoints/EnrollmentEndpoints.cs (new), CourseWaitlist.Api/Contracts/ApiContracts.cs (new). Touch nothing else. Do not edit any test; if you believe a test must change, stop and tell me. Implement ONLY the POST endpoint (no DELETE, no GET). Minimum to pass the A1 tests:
- Contracts (record types, in ApiContracts.cs): EnrollRequest(string? LearnerId) and MutationResponse(string Outcome, string LearnerId, int? WaitlistPosition, IReadOnlyList<string>? PromotedLearnerIds). Both will be reused by the DELETE slice.
- Program.cs: call builder.Services.ConfigureHttpJsonOptions so SerializerOptions adds JsonStringEnumConverter and sets DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull (web defaults already give camelCase). Remove the template `app.MapGet("/", ...)` Hello World endpoint. Call a new extension method app.MapEnrollmentEndpoints() defined in EnrollmentEndpoints.cs. Keep everything from A0 and `public partial class Program { }`.
- EnrollmentEndpoints.cs: a static class with a public static extension method MapEnrollmentEndpoints(this IEndpointRouteBuilder). Use app.MapGroup("/offerings/{offeringId}") and MapPost("/enrollments", handler). The handler takes: string offeringId, EnrollRequest? request (nullable so an empty or missing body reaches my validation and does NOT produce a framework error), EnrollmentService service, IOptions<OfferingOptions> options, ILogger<Program> logger.
- Handler order: (1) if offeringId does not equal options.Value.Id with StringComparison.OrdinalIgnoreCase, return Results.Problem(statusCode: 404, title: "Offering not found"); (2) call service.Enroll(request?.LearnerId); (3) map the outcome:
  Enrolled -> Results.Json(response, statusCode: 201)
  Waitlisted -> Results.Json(response, statusCode: 202)
  AlreadyEnrolled and AlreadyWaitlisted -> Results.Json(response, statusCode: 200)
  PrerequisiteNotMet -> Results.Problem(statusCode: 422, title: "Prerequisite not met", extensions: a dictionary containing "outcome" = "PrerequisiteNotMet" and "learnerId" = the trimmed request learner id)
  InvalidRequest -> Results.Problem(statusCode: 400, title: "Invalid request", extensions: a dictionary containing "outcome" = "InvalidRequest")
  where response = new MutationResponse(result.Outcome.ToString(), learner id as submitted trimmed, result.WaitlistPosition, null). Never throw for any outcome. Use a switch expression over EnrollOutcome with no default arm so a future outcome causes a compile warning. Do not echo untrimmed ids.
- Log one structured line per request with ILogger: outcome and learner id only (for example logger.LogInformation("Enroll outcome {Outcome} for learner {LearnerId}", ...)). Log nothing else.
No locking, no new packages, no other endpoints.
Run dotnet build, then dotnet test (all tests) and quote the real output, STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot edited Program.cs (JSON options with string enums and null omission, removed the Hello World route, mapped the endpoints) and created EnrollmentEndpoints.cs and ApiContracts.cs. The first build failed on a missing using and on warning CS8524; both were fixed in the authorized files. Final: build clean, 66 passed, 0 failed.
- Decision: Accepted with one disclosed deviation
- Deviation: Copilot wrapped the outcome switch in `#pragma warning disable CS8524` to satisfy warnings-as-errors. I accepted it: CS8524 only concerns unnamed enum values; adding a new named outcome still triggers CS8509 and fails the build, so the exhaustiveness check I wanted is preserved.
- Review notes: offering check precedes Enroll; body parameter is nullable so empty bodies reach validation; 404/422/400 use Results.Problem. Follow-ups for limitations: response echoes the trimmed request id (not stored casing); user-supplied ids are logged without a length limit; malformed JSON and unsupported content types are not covered by tests.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test --filter Slice!=C9` -> [63 passed]; manual POSTs against the demo config -> [201 then 422]; `git diff --stat` -> [expected files only]
- Commit: `feat(api): add enrollment POST endpoint`

### Entry 27: A2 Phase 1, DELETE endpoint tests (Copilot Agent mode)
- Prompt (sanitized):
Slice A2 (DELETE /offerings/{offeringId}/enrollments/{learnerId}). Phase 1: tests only.
Files you may create: CourseWaitlist.Tests/Api/DropEndpointTests.cs. Touch nothing else, including ApiFactory.cs, EnrollEndpointTests.cs and all production files. Define your own small private helpers inside the new file.
Implement T-53, T-54, T-55, T-56 and T-62 from #file:docs/test-plan.md with xUnit + FluentAssertions 7.x only. Use ApiFactory (default configuration: offering CS201-TEST, capacity 2, prerequisite CS101; learner-001, learner-002, learner-003 completed it; learner-004 did not). Create `using var factory = new ApiFactory();` and `factory.CreateClient()` INSIDE each test. Do not reference any type from CourseWaitlist.Api; read bodies with ReadFromJsonAsync<JsonElement>(). Set up state by POSTing to /offerings/CS201-TEST/enrollments with {"learnerId":"..."}. Add [Trait("Slice","A2")] and [Trait("Case","T-xx")]. Names Method_Scenario_ExpectedResult, Arrange/Act/Assert comments.
Contract to assert: drop responses return JSON with `outcome` (string), `learnerId`, and ALWAYS a `promotedLearnerIds` array (empty when nobody was promoted); `waitlistPosition` is absent. ProblemDetails responses must be asserted strictly: ContentType.Should().NotBeNull() first, then ContentType!.MediaType equals "application/problem+json", then the body `status`. Never use the null-conditional on ContentType.
Write exactly these 5 tests:
1. T-53: POST learner-001, learner-002 (both Enrolled) and learner-003 (Waitlisted). DELETE /offerings/CS201-TEST/enrollments/learner-001 -> 200; outcome "Dropped"; learnerId "learner-001"; promotedLearnerIds equals ["learner-003"].
2. T-54: POST learner-001. DELETE it -> 200 "Dropped". DELETE it again -> 200; outcome "NotEnrolled"; promotedLearnerIds is an empty array.
3. T-55: POST learner-001, learner-002, learner-003 (waitlisted). DELETE learner-003 -> 200; outcome "RemovedFromWaitlist"; promotedLearnerIds is an empty array. Then DELETE learner-001 -> 200 "Dropped" with promotedLearnerIds an empty array (the removed waiter is not promoted).
4. T-56: DELETE /offerings/UNKNOWN/enrollments/learner-001 -> 404 with the strict problem+json assertions and body status 404.
5. T-62: POST learner-001 (Enrolled). DELETE /offerings/CS201-TEST/enrollments/%20 -> 400 with the strict problem+json assertions, body status 400 and outcome "InvalidRequest". Then POST learner-001 again -> 200 "AlreadyEnrolled" (proves the invalid DELETE changed nothing).
Do not test GET in this slice.
Run dotnet build, then dotnet test, and quote the real output. Expected: all 5 new tests FAIL on assertions at the DELETE call (status 404 found where 200 or 400 is expected; T-56 fails at the ContentType NotBeNull check), not on compile errors, and not during the POST setup; the existing 66 tests still pass. Report any other failure reason or any new test that passes, and do not delete or change it. STOP, list changed files, and propose a Conventional Commit message.

- Response summary: Copilot added DropEndpointTests (T-53, T-54, T-55, T-56, T-62). Run: 5 failed, 66 passed, total 71. T-53/54/55/62 failed at the DELETE status (404 instead of 200 or 400); T-56 failed at the strict Content-Type NotBeNull assertion. Setup POSTs were asserted and succeeded. Copilot moved the repeat POST in T-62 before the DELETE assertions so the state check runs while DELETE is still unimplemented.
- Decision: Accepted
- Review finding: the first build ran with 5 MSB3026 warnings because an API process from my manual smoke test was still running and locking the DLL. I stopped it and re-ran with -warnaserror.
- Verification (my own runs): `dotnet build -warnaserror` -> [0 warnings, 0 errors]; `dotnet test --filter Slice=A2` -> [5 failed]; `git diff --stat` -> [only the new test file]
- Commit: `test(api): add A2 drop endpoint tests`