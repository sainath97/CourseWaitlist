# Course Waitlist Service — Design Specification

## 1. Service boundary

**In scope:** A minimal, in-memory service for one course offering: enrollment, dropping, prerequisite checks, and waitlist promotion. The implementation uses C# on .NET 10, with domain and service logic in `CourseWaitlist.Core`, a Minimal API in `CourseWaitlist.Api`, and xUnit tests in `CourseWaitlist.Tests`.

**Out of scope:** Frontend, course catalog, payments, identity or authentication, multiple terms, deployment, databases, and a configurable policy engine. No new infrastructure or packages beyond the test libraries.

## 2. Data model

- **`OfferingOptions`**: `Id`, `CourseCode`, `Capacity` (at least 1), and optional `PrerequisiteCourseCode`.
- **Enrolled list**: learner IDs for the offering.
- **`WaitlistEntry`**: `LearnerId` and `Sequence` (`long`). Sequence values increase monotonically and determine waitlist order; timestamps are not used.
- **`ILearnerRecords.HasCompleted(learnerId, courseCode)`**: reports whether a learner has completed a course.

Trim learner IDs, reject empty IDs and IDs longer than 64 characters, and compare IDs using `StringComparer.OrdinalIgnoreCase`. Return copies from `GetSnapshot()` so callers cannot mutate service state.

## 3. API

| Method and path | Behavior |
|---|---|
| `POST /offerings/{offeringId}/enrollments` | Enroll learner from body `{ "learnerId": "..." }`. |
| `DELETE /offerings/{offeringId}/enrollments/{learnerId}` | Drop the learner or remove them from the waitlist. |
| `GET /offerings/{offeringId}` | Get the offering snapshot. |

GET response: { "offeringId", "courseCode", "capacity", "enrolled": [learnerIds], "waitlist": [learnerIds in FIFO order] }.

Mutation response shape: `{ "outcome": "...", "learnerId": "...", "waitlistPosition": 1, "promotedLearnerIds": [] }`. Optional fields are omitted when not applicable. Serialize outcome enums as strings.

Enroll responses never include promotedLearnerIds. Drop responses always include promotedLearnerIds (an empty array when nobody was promoted) and never include waitlistPosition. The GET response always includes both the enrolled and waitlist arrays, empty when there is nothing to list.

| Outcome | HTTP |
|---|---:|
| `Enrolled` | 201 |
| `Waitlisted` | 202 |
| `AlreadyEnrolled`, `AlreadyWaitlisted` | 200 |
| `PrerequisiteNotMet` | 422 |
| `InvalidRequest` | 400 |
| Unknown `offeringId` | 404 (API layer only) |
| `Dropped`, `RemovedFromWaitlist`, `NotEnrolled` | 200 |

`NotEnrolled` covers a repeat drop or a drop for an unknown learner; it is an idempotent no-op.

## 4. Invariants

- Enrolled count never exceeds capacity.
- A learner is never both enrolled and waitlisted.
- Neither list contains duplicate learner IDs, compared case-insensitively.
- Waitlist entries are in strictly ascending sequence order.
- After every validated Enroll and every Drop of an enrolled learner, a free seat and a currently eligible waitlisted learner do not coexist. InvalidRequest, NotEnrolled and a Drop of a waitlisted learner do not reconcile free seats; seats are reconciled at the next validated Enroll or enrolled-learner Drop.

## 5. Rules

- **Enroll:** Validate the request (`InvalidRequest` otherwise). Call `FillFreeSeats()` so any free seat goes to an eligible waiter first. Then return `AlreadyEnrolled` or `AlreadyWaitlisted` (current 1-based position) for an existing learner. Then check eligibility; if the prerequisite is unmet, return `PrerequisiteNotMet` without adding the learner. Otherwise enroll the learner if a seat is free, or append them to the waitlist and return `Waitlisted` with a 1-based position.
- **Drop:** If enrolled, remove the learner, call `FillFreeSeats()`, and report promoted learner IDs. If waitlisted, remove them without promotion (free seats are reconciled at the next validated Enroll or enrolled-learner Drop) and return `RemovedFromWaitlist`. Otherwise return `NotEnrolled`.
- **`FillFreeSeats()`** (private): While a seat is free, scan from the waitlist head and promote the first learner eligible at that time. Skip but retain ineligible entries in their original positions; re-check them on each later promotion.

## 6. Concurrency

Use one private in-process lock to protect all service state and public operations. It is sufficient while the service’s state is private to one process: it serializes state transitions and prevents concurrent requests from violating invariants. Keep operations synchronous; do not perform asynchronous work while holding the lock.

Supporting multiple offerings may require narrower coordination and a clear boundary for each offering’s state and operations. Supporting multiple service instances requires coordination across processes and shared state; an in-process lock alone cannot protect the invariants.

Eligibility is evaluated inside the lock; this is acceptable because `ILearnerRecords` is in-memory, and a remote records service would need a different approach.

Lock order: every public operation takes the service lock first; eligibility checks then call InMemoryLearnerRecords, which takes its own internal lock. The records type never calls back into the service, so the order is always service lock, then records lock, and the two cannot deadlock. GetSnapshot takes the same lock, so readers briefly block writers; snapshots are always consistent.

## 7. Delivery approach

Built in small test-first slices; locking is added in the final concurrency slice and verified by tests that fail without it.

## 8. Assumptions, tradeoffs, and limitations

- Prerequisite eligibility is checked through `ILearnerRecords`; ineligible learners are neither enrolled nor promoted.
- There is no background re-scan when a learner becomes eligible. Re-evaluation occurs during a later promotion attempt.
- O(n) list lookups and an unbounded waitlist are acceptable at this scale.
- Duplicate enrollment requests are idempotent by state and do not create records or change state.
- Eligibility can change between operations because `ILearnerRecords` is mutable; the service reconciles free seats at the next validated Enroll or enrolled-learner Drop.
- A Drop of a waitlisted learner does not reconcile free seats; a skipped waiter who regains eligibility is promoted at the next validated Enroll or enrolled-learner Drop.
- Promotions triggered inside Enroll are not reported in the Enroll response; they are visible through GetSnapshot.
- The service stores the trimmed learner id exactly as first submitted; matching is case-insensitive, so later requests with different casing refer to the same learner.