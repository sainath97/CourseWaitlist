# Course Waitlist Service

A minimal .NET 10 in-memory service for enrollment, drops, prerequisite checks,
and FIFO waitlist promotion for one course offering. No starter project was
provided, so the minimal in-memory service fallback was used.

## Prerequisite and commands

Install the **.NET 10 SDK**. Run these commands from the repository root:

```powershell
dotnet restore .\CourseWaitlist.slnx
dotnet build .\CourseWaitlist.slnx
dotnet test .\CourseWaitlist.slnx
dotnet test .\CourseWaitlist.slnx --collect:"XPlat Code Coverage"
dotnet run --project CourseWaitlist.Api
```

The API prints its URL at startup. These examples use the verified demo URL
`http://localhost:5000`. The demo offering is `CS201-2026-FALL` (capacity 2,
prerequisite `CS101`); learners 001-003 have completed the prerequisite and
learner-004 has not. Run the requests in order against a fresh API process.
In PowerShell, `--%` passes the curl arguments literally.
Expected outcomes in order: `201 Enrolled`, `200 AlreadyEnrolled`, `201 Enrolled`,
`202 Waitlisted` (position 1), `200 Dropped` (promotes learner-003), `200 NotEnrolled`.

```powershell
curl.exe --% -sS -i -X POST http://localhost:5000/offerings/CS201-2026-FALL/enrollments -H "Content-Type: application/json" -d "{\"learnerId\":\"learner-001\"}"
curl.exe --% -sS -i -X POST http://localhost:5000/offerings/CS201-2026-FALL/enrollments -H "Content-Type: application/json" -d "{\"learnerId\":\"learner-001\"}"
curl.exe --% -sS -i -X POST http://localhost:5000/offerings/CS201-2026-FALL/enrollments -H "Content-Type: application/json" -d "{\"learnerId\":\"learner-002\"}"
curl.exe --% -sS -i -X POST http://localhost:5000/offerings/CS201-2026-FALL/enrollments -H "Content-Type: application/json" -d "{\"learnerId\":\"learner-003\"}"
curl.exe --% -sS -i -X DELETE http://localhost:5000/offerings/CS201-2026-FALL/enrollments/learner-001
curl.exe --% -sS -i -X DELETE http://localhost:5000/offerings/CS201-2026-FALL/enrollments/learner-001
```

Enrollment and learner-record state is in memory and resets when the API
restarts. More manual requests are in [Api.http](./CourseWaitlist.Api/Api.http).

## Repository map

- [Design specification](./docs/design-spec.md) — boundaries, API, invariants,
  concurrency, assumptions, and limitations.
- [Test plan](./docs/test-plan.md) — cases mapped to acceptance criteria.
- [AI-use log](./docs/ai-use-log.md) — prompts, review decisions, and
  verification evidence.

## Limitations and follow-up risks

- One offering per service instance; an in-process lock does not coordinate
  multiple instances.
- Waitlist lookups are O(n), and the waitlist is unbounded.
- Eligibility changes are not re-scanned in the background. Eligible waiters
  are reconsidered at a validated Enroll or enrolled-learner Drop; dropping a
  waitlisted learner alone does not reconcile free seats.
