# Test cases and traceability

Every case below names the requirement it covers, the technique it came from, and the automated test
that executes it. The table doubles as the traceability matrix: reading down the requirement column
shows what is covered, and the gaps at the bottom show what is not.

Layers: **API** is the Java suite in [`api-tests/`](../api-tests), **CON** the Python contract and
concurrency suite in [`python-tests/`](../python-tests), **UI** the Playwright suite in
[`ui-tests/`](../ui-tests), **PERF** the k6 script in [`performance/`](../performance).

## Authentication and identity

| ID | Case | Requirement | Technique | Layer |
|----|------|-------------|-----------|-------|
| TC-01 | Health responds without a key | REQ-01 | Specification based | API |
| TC-02 | Protected endpoints reject an anonymous caller | REQ-01 | Negative testing | API |
| TC-03 | An unknown key is rejected | REQ-02 | Negative testing | API |
| TC-04 | An empty key is treated as missing | REQ-01 | Boundary of "present" | API |
| TC-05 | Each seeded key resolves to its identity and role | REQ-03, REQ-04 | Data driven | API |
| TC-06 | The identity response does not echo the key back | REQ-04 | Security review | API |

## Booking field validation

| ID | Case | Requirement | Technique | Layer |
|----|------|-------------|-----------|-------|
| TC-20 | Durations of 5, 30 and 120 minutes are accepted | REQ-23 | Boundary value analysis | API |
| TC-21 | Durations of 4, 121 and 240 minutes are refused | REQ-23 | Boundary value analysis | API |
| TC-22 | A zero length booking fails on ordering, not duration | REQ-22 | Validation order | API |
| TC-23 | A reversed window is refused | REQ-22 | Negative testing | API |
| TC-24 | Purposes of 3, 50 and 200 characters are accepted | REQ-21 | Boundary value analysis | API |
| TC-25 | Purposes of 1, 2, 201 and 500 characters are refused | REQ-21 | Boundary value analysis | API |
| TC-26 | A whitespace only purpose is refused | REQ-21 | Equivalence partitioning | API |
| TC-27 | Each mandatory field is enforced | REQ-21, REQ-22 | Data driven | API |
| TC-28 | Malformed timestamps are refused | REQ-22 | Negative testing | API |
| TC-29 | A non-string purpose is refused rather than coerced | REQ-21 | Type confusion | API |
| TC-30 | Unknown or wrongly cased priorities are refused | REQ-26 | Equivalence partitioning | API |
| TC-31 | Priority defaults to ROUTINE | REQ-26 | Specification based | API |
| TC-32 | The purpose is stored trimmed | REQ-21 | Specification based | API |

## Lead time, capacity and overlap

| ID | Case | Requirement | Technique | Layer |
|----|------|-------------|-----------|-------|
| TC-33 | Lead times of 60 minutes and 2 days are accepted | REQ-24 | Equivalence partitioning | API |
| TC-33b | A lead time of exactly 15 minutes is accepted | REQ-24 | Boundary value analysis | API |
| TC-34 | Lead times of −60, 0 and 14 minutes are refused | REQ-24 | Boundary value analysis | API |
| TC-35 | Exactly 30 days ahead is accepted | REQ-25 | Boundary value analysis | API |
| TC-36 | Beyond 30 days is refused | REQ-25 | Boundary value analysis | API |
| TC-37 | An emergency may start inside the lead window | REQ-28 | Decision table rule R8 | API |
| TC-38 | An emergency may not start in the past | REQ-28 | Boundary of the override | API |
| TC-39 | A second overlap on a single slot station is refused | REQ-29 | Specification based | API |
| TC-40 | Capacity two admits two overlaps and refuses the third | REQ-29 | Boundary value analysis | API |
| TC-41 | Back to back bookings that only touch are accepted | REQ-30 | Boundary value analysis | API |
| TC-42 | Cancelling frees the slot | REQ-29, REQ-41 | Specification based | API |
| TC-43 | An inactive station refuses bookings | REQ-31 | Negative testing | API |
| TC-44 | An unknown station is reported as not found | REQ-12 | Negative testing | API |
| TC-45 | Station codes are matched case-insensitively | REQ-11 | Equivalence partitioning | API |

## Authorisation

| ID | Case | Requirement | Technique | Layer |
|----|------|-------------|-----------|-------|
| R1 to R9 | The nine rows of the booking decision table | REQ-20, REQ-27, REQ-28 | Decision table | API |
| TC-50 | The role check wins over the timing check | REQ-27 | Rule precedence | API |
| TC-51 | A viewer may read but not create | REQ-20 | Access control matrix | API, UI |
| TC-52 | Only an administrator may create a station | REQ-13 | Access control matrix | API |
| TC-53 | The server owns state, requester and id | REQ-32 | Mass assignment probe | API |

## Lifecycle

| ID | Case | Requirement | Technique | Layer |
|----|------|-------------|-----------|-------|
| TC-60 | A new booking starts in REQUESTED | REQ-40 | State transition | API |
| TC-61 | The four permitted transitions succeed | REQ-41, REQ-45 | State transition, 0-switch | API |
| TC-62 | Eight forbidden transitions are refused | REQ-42 | State transition, negative | API |
| TC-63 | The full path REQUESTED to COMPLETED | REQ-41 | State transition, 1-switch | API |
| TC-64 | An operator may cancel their own booking | REQ-44 | Access control matrix | API |
| TC-65 | Another operator may not cancel it | REQ-44 | Access control matrix | API |
| TC-66 | A supervisor may cancel any booking | REQ-44 | Access control matrix | API |
| TC-67 | An operator may not confirm or complete | REQ-43 | Access control matrix | API |
| TC-68 | An unknown booking id is reported | REQ-42 | Negative testing | API |
| TC-69 | An unknown action is reported | REQ-42 | Negative testing | API |

## Queries, representation and robustness

| ID | Case | Requirement | Technique | Layer |
|----|------|-------------|-----------|-------|
| TC-70 | Bookings are ordered by start time | REQ-52 | Specification based | API |
| TC-71 | The station filter narrows the result | REQ-50 | Specification based | API |
| TC-72 | The state filter narrows the result | REQ-50 | Specification based | API, UI |
| TC-73 | The requester filter narrows the result | REQ-50 | Specification based | API |
| TC-74 | Pagination slices the content and reports totals | REQ-51 | Boundary value analysis | API |
| TC-75 | A page beyond the end is empty, not an error | REQ-51 | Boundary value analysis | API |
| TC-76 | Invalid page and size values are rejected | REQ-51 | Boundary value analysis | API |
| TC-77 | Timestamps are ISO-8601 in UTC | REQ-54 | Specification based | API |
| TC-78 | Error bodies carry a code and a message | REQ-53 | Structural assertion | API |
| TC-79 | Malformed bodies are rejected without a stack trace | REQ-60, REQ-64 | Negative testing | API |
| TC-80 | An oversized body is answered with 413 | REQ-61 | Boundary value analysis | API |
| TC-81 | Path traversal attempts are refused | REQ-62 | Security testing | API |
| TC-82 | The web interface is served at the root | REQ-62 | Smoke | API |
| TC-83 | An unknown API route is reported | REQ-53 | Negative testing | API |
| TC-84 | Responses declare a nosniff policy | REQ-63 | Security testing | API |

## Contract and concurrency

| ID | Case | Requirement | Technique | Layer |
|----|------|-------------|-----------|-------|
| TC-90 | A created booking matches the booking schema | REQ-53, REQ-54 | Schema validation | CON |
| TC-91 | A booking page matches the page schema | REQ-51 | Schema validation | CON |
| TC-92 | A station page matches the station schema | REQ-10 | Schema validation | CON |
| TC-93 | Every error body matches the error schema | REQ-53 | Schema validation | CON |
| TC-94 | A decided booking reports who decided it | REQ-45 | Schema validation | CON |
| TC-95 | A created station matches the station schema | REQ-15 | Schema validation | CON |
| TC-96 | Two simultaneous bookings, one slot, one winner | REQ-29 | Race condition | CON |
| TC-97 | Four racers on two slots yield exactly two winners | REQ-29 | Race condition | CON |
| TC-98 | A booking cannot be confirmed twice in parallel | REQ-41 | Race condition | CON |
| TC-99 | Cancellation ownership holds under repetition | REQ-44 | Race condition | CON |

## Interface

| ID | Case | Requirement | Technique | Layer |
|----|------|-------------|-----------|-------|
| UI-01 | The workspace stays hidden until a key is accepted | REQ-01 | State based | UI |
| UI-02 | An unknown key is reported, workspace stays hidden | REQ-02 | Negative testing | UI |
| UI-03 | An empty key is refused before any request | REQ-01 | Client side validation | UI |
| UI-04 | An operator signs in and sees their role | REQ-04 | Specification based | UI |
| UI-05 | A viewer sees a read only workspace | REQ-20 | Access control matrix | UI |
| UI-06 | Only active stations are offered | REQ-31 | Specification based | UI |
| UI-07 | A valid booking appears as REQUESTED | REQ-40 | End to end | UI |
| UI-08 | The form is cleared after success | — | Usability | UI |
| UI-09 | A short purpose is caught before the request | REQ-21 | Client side validation | UI |
| UI-10 | A server rejection is shown to the user | REQ-23, REQ-53 | Error handling | UI |
| UI-11 | An operator may not request an emergency slot | REQ-27 | Access control matrix | UI |
| UI-12 | A supervisor can confirm a requested booking | REQ-43 | End to end | UI |
| UI-13 | An operator sees no confirm button | REQ-43 | Access control matrix | UI |
| UI-14 | The state filter narrows the table | REQ-50 | Specification based | UI |
| UI-15 | The empty state is shown when nothing matches | — | Usability | UI |
| UI-16 | Markup in a purpose is rendered as text | REQ-63 | Security testing | UI |
| UI-17 | Signing out hides the workspace and clears the key | REQ-01 | State based | UI |
| UI-18 | A capacity clash is surfaced as an error | REQ-29 | Error handling | UI |

## Performance

| ID | Case | Requirement | Technique | Layer |
|----|------|-------------|-----------|-------|
| PERF-01 | Reading the schedule under 10 concurrent users | — | Load, steady state | PERF |
| PERF-02 | Creating bookings under a ramp to 5 users | — | Load, ramp | PERF |
| PERF-03 | Every response is 201 or an explained 409 | REQ-29 | Correctness under load | PERF |

A 409 from a saturated station is a correct answer, not a failure, so the booking request declares
`expectedStatuses(201, 409)`. Without that, `http_req_failed` counts every legitimate capacity
rejection as an error and the threshold fails for the wrong reason, which is exactly what happened
on the first CI run.

Thresholds: failed requests under 1 percent, overall p95 under 300 ms and p99 under 800 ms, list
p95 under 150 ms, create p95 under 250 ms, and check pass rate above 99 percent.

## Requirements with no automated coverage

| Requirement | Why not |
|-------------|---------|
| REQ-14 | Duplicate station codes are covered manually in the exploratory notes but not automated, because station creation is admin-only and rarely exercised. |
| REQ-64 | A genuine unexpected server error cannot be triggered from outside without changing the system under test. The closest automated check is TC-79, which proves no stack trace leaks from the paths that can be reached. |

## Known failures

| Case | Defect | Behaviour |
|------|--------|-----------|
| TC-33b | [DEF-002](defects/DEF-002.md) | The documented 15 minute boundary is refused |
| TC-38 | [DEF-001](defects/DEF-001.md) | An emergency booking can be backdated |
| TC-80 | [DEF-003](defects/DEF-003.md) | The connection is dropped instead of answering 413 |

These three are tagged `known-defect`. The gating run excludes the tag and is green; a separate CI
job runs only those three so the findings stay visible rather than being deleted to keep the build
quiet.
