# Test Plan

The Acceptance criterion column uses the assignment's criteria:

| AC | Meaning |
| 1 | Enrollment succeeds when capacity remains and the prerequisite rule is met |
| 2 | When full, an eligible learner joins the waitlist once; duplicate requests create no duplicate records |
| 3 | Dropping an enrolled learner opens a seat and promotes the earliest eligible waitlisted learner |
| 4 | An ineligible learner is never enrolled or promoted; the skip-and-retain policy is applied consistently |
| 5 | Invalid requests and repeated drops return clear results without corrupting state |
| 6 | Tests cover capacity, duplicates, FIFO promotion, prerequisite failure, invalid or repeated drops, plus a concurrency test |

## C1 — construction and validation

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-01 | C1 | Constructor rejects zero capacity | Offering options with `Capacity = 0` | Construct service | Constructor throws `ArgumentException` | 5 | unit |
| T-02 | C1 | Constructor rejects negative capacity | Offering options with `Capacity = -1` | Construct service | Constructor throws `ArgumentException` | 5 | unit |
| T-03 | C1 | Enroll rejects null learner ID | Valid offering, `learnerId = null` | `Enroll` | Returns `InvalidRequest`; state unchanged | 5 | unit |
| T-04 | C1 | Enroll rejects empty learner ID | Valid offering, `learnerId = ""` | `Enroll` | Returns `InvalidRequest`; state unchanged | 5 | unit |
| T-05 | C1 | Enroll rejects whitespace learner ID | Valid offering, `learnerId = "   "` | `Enroll` | Returns `InvalidRequest`; state unchanged | 5 | unit |
| T-06 | C1 | Enroll rejects 65-character learner ID | Valid offering, ID length = 65 | `Enroll` | Returns `InvalidRequest`; state unchanged | 5 | unit |
| T-07 | C1 | Drop rejects invalid learner ID | Valid offering, `learnerId = null` / `""` / `"   "` / 65 chars | `Drop` | Returns `InvalidRequest`; state unchanged | 5 | unit |
| T-08 | C1 | No state change on invalid input | Valid offering with empty state | Invalid `Enroll` / `Drop` calls | Snapshot remains empty; no enrolled or waitlist entries are created | 5 | unit |

## C2 — enroll with capacity

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-09 | C2 | Enrolled response has no waitlist position | Empty offering with capacity 2 | Enroll learner `A` | Response outcome is `Enrolled`; `waitlistPosition` is absent/null | 1 | unit |
| T-10 | C2 | Learner ID is trimmed | Offering capacity 2; learner id is `"  alice  "` | Enroll | Snapshot lists the learner as `alice` (trimmed) | 1 | unit |
| T-11 | C2 | Fill to capacity | Capacity 2; empty state | Enroll `A`, then `B` | Both return `Enrolled`; snapshot shows exactly 2 enrolled and an empty waitlist | 1 | unit |
| T-12 | C2 | Capacity 1 edge | Offering capacity 1; empty state | Enroll `A` | Returns `Enrolled`; snapshot shows 1 enrolled and an empty waitlist | 1 | unit |
| T-13 | C2 | No prerequisite requirement means eligible | No prerequisite configured | A learner with no completed courses calls `Enroll` | Returns `Enrolled` | 1 | unit |
| T-59 | C2 | 64-character ID is accepted (boundary) | Capacity 2 | Enroll a 64-character ID | Returns `Enrolled` | 1 | unit |

## C3 — prerequisite

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-14 | C3 | Prerequisite not met with seat free | Offering requires `CS101`; learner has not completed it; seat available | Enroll learner | Returns `PrerequisiteNotMet`; snapshot unchanged; seat remains free | 4 | unit |
| T-15 | C3 | Completed prerequisite allows enrollment | Offering requires `CS101`; learner has completed it; seat available | Enroll learner | Returns `Enrolled`; learner appears in enrolled list | 1 | unit |

## C4 — waitlist when full

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-16 | C4 | Full offering creates FIFO waitlist | Capacity 1; two learners `A`, `B` | Enroll `A` then `B` | `A` enrolled; `B` waitlisted with position 1 | 2 | unit |
| T-17 | C4 | Waitlist positions are 1, 2, 3 | Capacity 1; learners `A`, `B`, `C`, `D` | Enroll in order | Waitlist positions are `1`, `2`, `3` as expected | 2 | unit |
| T-18 | C4 | Ineligible learner is not added to waitlist when full | Capacity 1; `A` enrolled; `B` ineligible and tries to enroll | Enroll `B` while full | Returns `PrerequisiteNotMet`; `B` is absent from waitlist; no queue mutation | 4 | unit |
| T-19 | C4 | Enrolled list never exceeds capacity | Capacity 2 | 5 distinct learners enroll | Enrolled count is exactly 2, waitlist has 3, and enrolled never exceeds 2 | 2 | unit |

## C5 — duplicates

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-20 | C5 | Repeat enrolled learner is idempotent | Learner `A` already enrolled | Enroll `A` again | Returns `AlreadyEnrolled`; state unchanged | 2 | unit |
| T-21 | C5 | Repeat waitlisted learner is idempotent | Learner `B` already waitlisted at position 2 | Enroll `B` again | Returns `AlreadyWaitlisted` with same position; only one waitlist record exists | 2 | unit |
| T-22 | C5 | Case and whitespace variants are same learner | Learner IDs `" Alice "`, `"alice"`, `"ALICE"` | Enroll `" Alice "`, then `"alice"`, then `"ALICE"` | First returns `Enrolled`, others `AlreadyEnrolled`; snapshot has exactly one entry stored as `"Alice"` (trimmed, first-seen casing). The same variants are also tested while the learner is waitlisted. | 2 | unit |
| T-23 | C5 | Prerequisite removal does not change enrolled duplicate response | Learner already enrolled; prerequisite record removed later | Enroll same learner again | Returns `AlreadyEnrolled`; no state change | 2 | unit |
| T-24 | C5 | Snapshot unchanged on duplicate enroll | Valid offering with current state | Reissue duplicate enroll | Snapshot before and after are identical | 2 | unit |
| T-60 | C5 | Waitlisted learner who lost eligibility repeats Enroll | `B` waitlisted at position 1; `B`'s prerequisite then removed | Enroll `B` again | Returns `AlreadyWaitlisted` at the same position; no state change | 2 | unit |

## C6 — drop basics

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-25 | C6 | Drop enrolled learner frees a seat | Capacity 1; `A` enrolled and no waiters | Drop `A` | Returns `Dropped`; seat is reusable; snapshot shows `A` removed | 3 | unit |
| T-26 | C6 | Drop unknown learner is no-op | Learner `Z` absent from both lists | Drop `Z` | Returns `NotEnrolled`; no state mutation | 5 | unit |
| T-27 | C6 | Repeated drop is no-op | Learner `A` already absent | Drop `A` again | Returns `NotEnrolled`; state unchanged | 5 | unit |
| T-28 | C6 | Drop a waitlisted learner removes them | Capacity 1; `A` enrolled; `B`, `C` waitlisted | Drop `B` | Returns `RemovedFromWaitlist`; later positions shift; no one promoted | 3 | unit |
| T-29 | C6 | ID variants drop the same learner | `Alice` enrolled | Drop `"  ALICE "` | Returns `Dropped` | 5 | unit |

## C7 — FIFO promotion

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-30 | C7 | Earliest waiter is promoted on drop | Capacity 1; `A` enrolled; waitlist is `B`, `C` | Drop `A` | Returns `Dropped` and `promotedLearnerIds = ["B"]` | 3 | unit |
| T-31 | C7 | Successive drops promote in FIFO order | Capacity 1; `A` enrolled; waitlist `B`, `C`, `D` | Drop `A`, then `B` | Promotion order is `B` then `C`, and `D` remains later | 3 | unit |
| T-32 | C7 | A learner who left the waitlist is not promoted | Capacity 1; waitlist includes `B`, then `B` is removed | Later drop occurs | `B` is not promoted again; next eligible learner is promoted | 3 | unit |
| T-61 | C7 | Positions shift after promotion | Capacity 1; `A` enrolled; waitlist `B`, `C` | Drop `A`, then Enroll `C` again | `B` is enrolled; `C` returns `AlreadyWaitlisted` at position 1 | 3 | unit |

## C8 — eligibility during promotion

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-33 | C8 | Front-of-queue learner becomes ineligible after enqueue | Capacity 1; `B` at head of queue, then becomes ineligible | Drop `A` to trigger promotion | `B` is skipped and kept in place; next eligible learner is promoted | 4 | unit |
| T-34 | C8 | Skipped learner regains eligibility | Capacity 1; `B` skipped earlier, later becomes eligible | Next promotion after `A` drops | `B` is promoted before later waiters | 4 | unit |
| T-35 | C8 | Waitlist of only ineligible learners keeps seat free | Capacity 1; queue contains only ineligible learners | Drop `A` | Seat remains free; `PromotedLearnerIds` is empty | 4 | unit |
| T-36 | C8 | Seat free with skipped eligible waiter | Capacity 1; `A` enrolled, `B` waitlisted; `B`'s prerequisite is removed | `A` drops (seat stays free, `B` skipped); `B`'s prerequisite is restored; `C` calls Enroll | `B` is enrolled; `C` is `Waitlisted` at position 1 | 4 | unit |
| T-37 | C8 | Waiter's own repeat enroll after promotion (guards "reconcile free seats before the duplicate check") | Capacity 1; `A` enrolled; `B` waitlisted; `B`'s prerequisite is removed | `A` drops (seat stays free, `B` skipped); restore `B`'s prerequisite; `B` calls Enroll | Returns `AlreadyEnrolled`; snapshot shows `B` enrolled exactly once | 4 | unit |
| T-38 | C8 | Ineligible newcomer while eligible waiter is head | Capacity 1; seat free; `B` skipped earlier but is now eligible; `C` is ineligible | `C` calls Enroll | `C` gets `PrerequisiteNotMet`; `B` is enrolled; `C` is absent from both lists | 4 | unit |

## C9 — concurrency

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-39 | C9 | Many distinct learners fill to capacity | Capacity 10; 100 unique learners concurrently enroll | Parallel `Enroll` calls | Exactly 10 are enrolled; 90 are waitlisted; positions are 1..90 unique; no duplicates | 6 | concurrency |
| T-40 | C9 | Concurrent duplicate enroll is deduplicated | Capacity 10; same learner enrolls 50 times concurrently | Parallel `Enroll` calls | Exactly one `Enrolled`, 49 `AlreadyEnrolled`, one record in the snapshot | 6 | concurrency |
| T-41 | C9 | Mixed concurrent enroll and drop operations preserve invariants | Capacity 10; mixed concurrent enroll and drop calls | Parallel operations | `enrolled <= capacity`, no learner is both enrolled and waitlisted, no duplicates, waitlist order is monotonic; after all tasks finish, the enrolled count equals capacity when enough learners exist | 6 | concurrency |
| T-42 | C9 | Concurrent drops of same learner are serialized | Capacity 1; `A` enrolled; `B` waitlisted | 20 concurrent `Drop` calls for `A` | Exactly one `Dropped`, 19 `NotEnrolled`, and `B` appears in `PromotedLearnerIds` exactly once across all results | 6 | concurrency |

## A0 — API composition / startup

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-43 | A0 | Offering and capacity resolve from service container | App configuration contains valid offering and capacity | Start app / build service provider | The configured offering is resolvable and capacity is available | - | integration |
| T-44 | A0 | Startup fails for zero capacity | App config contains `Capacity = 0` | Start app | Startup fails before serving requests | - | integration |
| T-45 | A0 | Startup fails for missing offering ID | App config omits offering `Id` | Start app | Startup fails before serving requests | - | integration |

## A1 — POST enroll API

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-46 | A1 | Enroll succeeds with 201 | Valid offering and learner ID | `POST /offerings/{id}/enrollments` with body `{ learnerId: "alice" }` | Status `201 Created`; outcome is `Enrolled` | 1 | integration |
| T-47 | A1 | Waitlist succeeds with 202 | Offering full and valid new learner | `POST` while capacity is exhausted | Status `202 Accepted`; outcome is `Waitlisted`; `waitlistPosition = 1` | 2 | integration |
| T-48 | A1 | Duplicate enrolled learner returns 200 | Learner already enrolled | Repeat `POST` | Status `200 OK`; outcome is `AlreadyEnrolled` | 2 | integration |
| T-49 | A1 | Duplicate waitlisted learner returns 200 | Learner already waitlisted | Repeat `POST` | Status `200 OK`; outcome is `AlreadyWaitlisted`; same position preserved | 2 | integration |
| T-50 | A1 | Prerequisite failure is 422 with ProblemDetails | Learner is ineligible | `POST` with unmet prerequisite | Content type `application/problem+json`, status 422; `ProblemDetails` contains failure details | 4 | integration |
| T-51 | A1 | Invalid body/input yields 400 | Missing body, null learnerId, or empty learnerId | `POST` request | Status `400 Bad Request`; no mutation | 5 | integration |
| T-52 | A1 | Unknown offering ID yields 404 | Offering ID not present | `POST` | Status `404 Not Found` at API layer | 5 | integration |

## A2 — DELETE API

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-53 | A2 | Drop enrolled learner returns 200 and promotes | Capacity 1; `A` enrolled; `B` waiting | `DELETE /offerings/{id}/enrollments/{A}` | Status `200 OK`; outcome `Dropped`; `promotedLearnerIds = ["B"]` | 3 | integration |
| T-54 | A2 | Repeat drop is idempotent | Learner already absent | `DELETE` same learner again | Status `200 OK`; outcome `NotEnrolled` | 5 | integration |
| T-55 | A2 | Drop waitlisted learner returns 200 | Learner is waitlisted | `DELETE` waitlisted learner | Status `200 OK`; outcome `RemovedFromWaitlist` | 3 | integration |
| T-56 | A2 | Unknown offering ID yields 404 | Offering ID not present | `DELETE` | Status `404 Not Found` | 5 | integration |
| T-62 | A2 | DELETE with a whitespace ID | Valid offering | `DELETE /offerings/{id}/enrollments/%20` | Status `400`; content type `application/problem+json`; no state change | 5 | integration |

## A3 — GET API

| ID | Slice | Scenario | Given | When | Then | Acceptance criterion | Level |
|---|---|---|---|---|---|---|---|
| T-57 | A3 | Snapshot reflects enrolled and waitlist order | Offering with enrolled and waitlisted learners | `GET /offerings/{id}` | Returns snapshot with enrolled IDs and waitlist in exact FIFO order | 3 | integration |
| T-58 | A3 | Unknown offering ID yields 404 | Offering ID not present | `GET` | Status `404 Not Found` | 5 | integration |

## Coverage against the assignment's required tests (AC6)
- Available capacity: T-09, T-11, T-12, T-15
- Full capacity: T-16, T-17, T-18, T-19
- Duplicate requests: T-20 to T-24, T-60
- FIFO promotion: T-30, T-31, T-32, T-61
- Prerequisite failure: T-14, T-18, T-38, T-50
- Invalid or repeated drops: T-07, T-26, T-27, T-29, T-54, T-62
- Concurrency: T-39 to T-42