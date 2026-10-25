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