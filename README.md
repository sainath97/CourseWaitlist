# Course Waitlist Service

A minimal .NET 10 in-memory service for enrolling and dropping learners in one
course offering, with prerequisite checks and FIFO waitlist promotion.

## Prerequisites

- .NET 10 SDK

## Build and test

From the repository root:

```powershell
dotnet build .\CourseWaitlist.slnx
dotnet test .\CourseWaitlist.slnx
```

## Run the API

From the repository root:

```powershell
dotnet run --project .\CourseWaitlist.Api
```

Use the URL printed by `dotnet run` as the host when sending requests. For
example, the included [Api.http](./CourseWaitlist.Api/Api.http) file defaults
to `http://localhost:5000`; update its `@host` value if your run prints a
different URL. The requests demonstrate the configured offering,
`CS201-2026-FALL`, with capacity 2 and prerequisite `CS101`.

The service stores enrollment, waitlist, and learner-record state in memory.
The demo prerequisite records are configured in
[appsettings.json](./CourseWaitlist.Api/appsettings.json). Enrollment state
is reset whenever the API process restarts.

## API routes

- `GET /offerings/{offeringId}` — read the offering snapshot.
- `POST /offerings/{offeringId}/enrollments` — enroll or waitlist a learner.
- `DELETE /offerings/{offeringId}/enrollments/{learnerId}` — drop a learner or
  remove them from the waitlist.

Unknown offering IDs return 404. Learner IDs are trimmed, must be non-empty,
and may contain at most 64 characters. IDs are matched case-insensitively.
When the offering is full, eligible learners join the waitlist. During
promotion, ineligible waitlisted learners are skipped but retained in their
original queue position and rechecked at a later reconciliation.
