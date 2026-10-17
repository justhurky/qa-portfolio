# Test cases and traceability

Every case names the requirement it covers, the technique it came from, and the layer that runs it.
The table doubles as the traceability matrix: reading down the requirement column shows what is
covered, and the gaps at the end show what is not.

Layers: **UT** unit (xUnit), **IT** integration (xUnit with the in-process host), **UI** Selenium,
**MT** manual, **EX** exploratory, **PERF** k6.

## Unit: validation rules

| ID | Case | Requirement | Technique |
|----|------|-------------|-----------|
| UT-01 | Durations of 5, 6, 30, 119, 120 minutes are accepted | REQ-23 | Boundary value analysis |
| UT-02 | Durations of 0, 1, 4, 4.9, 120.1, 121, 1440 are refused | REQ-23 | Boundary value analysis |
| UT-03 | Both duration boundaries are inclusive | REQ-23 | Boundary value analysis |
| UT-04 | A reversed window fails the ordering rule | REQ-22 | Negative testing |
| UT-05 | A zero length window fails ordering before duration | REQ-22 | Validation order |
| UT-06 | Purposes of 3, 4, 100, 199, 200 characters are accepted | REQ-21 | Boundary value analysis |
| UT-07 | Purposes of 1, 2, 201, 500 characters are refused | REQ-21 | Boundary value analysis |
| UT-08 | Null, empty and whitespace purposes are refused | REQ-21 | Equivalence partitioning |
| UT-09 | The length is measured after trimming | REQ-21 | Specification based |
| UT-10 | Lead times of 15, 16, 60 minutes and 30 days are accepted | REQ-24 | Equivalence partitioning |
| UT-11 | Lead times of 14.9, 14, 0, −1, −1440 are refused | REQ-24 | Boundary value analysis |
| UT-12 | The minimum lead boundary is inclusive | REQ-24 | Boundary value analysis |
| UT-13 | Lead times beyond 30 days are refused | REQ-25 | Boundary value analysis |
| UT-14 | An emergency may start inside the minimum lead time | REQ-28 | Decision table R8 |
| UT-15 | An emergency may not start in the past | REQ-28 | Boundary of the override |
| UT-16 | The far horizon applies to emergencies too | REQ-25 | Equivalence partitioning |

## Unit: capacity and time windows

| ID | Case | Requirement | Technique |
|----|------|-------------|-----------|
| UT-20 | An empty station accepts a booking | REQ-29 | Specification based |
| UT-21 | A single slot station refuses an overlap | REQ-29 | Specification based |
| UT-22 | Capacity two accepts a second overlap, refuses a third | REQ-29 | Boundary value analysis |
| UT-23 | Windows that only touch do not overlap | REQ-30 | Boundary value analysis |
| UT-24 | A contained window overlaps the one containing it | REQ-29 | Equivalence partitioning |
| UT-25 | A one minute overlap still clashes | REQ-29 | Boundary value analysis |
| UT-26 | Non-overlapping windows are ignored | REQ-29 | Equivalence partitioning |
| UT-50 | A window reports its duration in minutes | REQ-23 | Specification based |
| UT-51 | Ordering is strict | REQ-22 | Boundary value analysis |
| UT-52 | Lead time is measured from the supplied instant | REQ-24 | Specification based |
| UT-53 | Overlap is symmetric | REQ-29 | Property |
| UT-54 | A window does not overlap one starting when it ends | REQ-30 | Boundary value analysis |
| UT-55 | A window overlaps itself | REQ-29 | Property |

## Unit: state machine and roles

| ID | Case | Requirement | Technique |
|----|------|-------------|-----------|
| UT-30 | A new booking starts in Requested | REQ-40 | State transition |
| UT-31 | The four documented transitions are permitted | REQ-41 | State transition, 0-switch |
| UT-32 | Eight other transitions are refused | REQ-42 | State transition, negative |
| UT-33 | Completed and Cancelled are terminal | REQ-41 | State transition |
| UT-34 | Requested and Confirmed are not terminal | REQ-41 | State transition |
| UT-35 | No state transitions to itself | REQ-42 | Property |
| UT-36 | Every state is declared in the table | REQ-41 | Completeness |
| UT-37 | A refusal names the state it came from | REQ-42 | Specification based |
| UT-40 | A role satisfies itself and everything below | REQ-03 | Equivalence partitioning |
| UT-41 | Creating a booking requires Operator or higher | REQ-20 | Access control matrix |
| UT-42 | The priority decision table holds | REQ-27 | Decision table |
| UT-43 | A refused priority names the role needed | REQ-27 | Specification based |
| UT-44 | Only an emergency overrides the lead time | REQ-28 | Decision table |
| UT-45 | Deciding requires Supervisor or higher | REQ-43 | Access control matrix |
| UT-46 | Cancellation depends on ownership then role | REQ-44 | Decision table |
| UT-47 | Only an administrator may manage stations | REQ-13 | Access control matrix |

## Unit: wire contract

| ID | Case | Requirement | Technique |
|----|------|-------------|-----------|
| UT-60 | The documented wire values parse | REQ-26 | Specification based |
| UT-61 | Parsing is case sensitive | REQ-26 | Equivalence partitioning |
| UT-62 | Unknown and padded values are refused | REQ-26 | Negative testing |
| UT-63 | An absent value falls back to ROUTINE | REQ-26 | Specification based |
| UT-64 | Every priority renders as its documented value | REQ-26 | Specification based |
| UT-65 | What the API returns is what the API accepts | REQ-26, REQ-53 | Property, DEF-003 regression |
| UT-66 | Every priority is covered by the mapping | REQ-26 | Completeness |

## Integration: HTTP behaviour

| ID | Case | Requirement | Technique |
|----|------|-------------|-----------|
| IT-01 | Health is reachable without a key | REQ-01 | Smoke |
| IT-02 | Protected endpoints reject anonymous callers | REQ-01 | Negative testing |
| IT-03 | An unknown key is rejected | REQ-02 | Negative testing |
| IT-04 | Each seeded key resolves to its identity | REQ-03, REQ-04 | Data driven |
| IT-05 | The identity response does not echo the key | REQ-04 | Security review |
| IT-10, IT-11 | Duration boundaries over HTTP | REQ-23 | Boundary value analysis |
| IT-12 | Purpose length over HTTP | REQ-21 | Boundary value analysis |
| IT-13 | The purpose is stored trimmed | REQ-21 | Specification based |
| IT-14 | A reversed window is refused | REQ-22 | Negative testing |
| IT-15 | Malformed timestamps are refused | REQ-22 | Negative testing |
| IT-16 | An unknown priority is refused | REQ-26 | Equivalence partitioning |
| IT-17 | Priority defaults to ROUTINE | REQ-26 | Specification based |
| IT-18 | Station codes are case insensitive | REQ-11 | Equivalence partitioning |
| IT-19 | An unknown station is reported | REQ-12 | Negative testing |
| IT-20 | An inactive station refuses bookings | REQ-31 | Negative testing |
| IT-21 | The server owns state and requester | REQ-32 | Mass assignment probe |
| IT-30, IT-31 | Lead time partitions over HTTP | REQ-24 | Equivalence partitioning |
| IT-32 | The exact 15 minute boundary is accepted | REQ-24 | Boundary value analysis |
| IT-33 | Beyond the far horizon is refused | REQ-25 | Boundary value analysis |
| IT-34 | An emergency may start inside the lead window | REQ-28 | Decision table R8 |
| IT-35 | An emergency may not be backdated | REQ-28 | Boundary of the override |
| IT-36 to IT-39 | Overlap, capacity, touching slots, freeing a slot | REQ-29, REQ-30 | Boundary value analysis |
| IT-40 | The nine row decision table end to end | REQ-20, REQ-27, REQ-28 | Decision table |
| IT-41 | The role check wins over the timing check | REQ-27 | Rule precedence |
| IT-42 | A viewer may read but not create | REQ-20 | Access control matrix |
| IT-43 | Only an administrator may create a station | REQ-13 | Access control matrix |
| IT-44 | A duplicate station code is refused | REQ-14 | Negative testing |
| IT-45 | Station capacity is validated | REQ-15 | Boundary value analysis |
| IT-50 to IT-53 | The lifecycle over HTTP, valid and invalid | REQ-40 to REQ-42, REQ-45 | State transition |
| IT-54 to IT-56 | Cancellation ownership and decision rights | REQ-43, REQ-44 | Access control matrix |
| IT-57, IT-58 | Unknown booking and unknown action | REQ-42 | Negative testing |
| IT-60 to IT-62 | Ordering and the three filters | REQ-50, REQ-52 | Specification based |
| IT-63 to IT-65 | Pagination, overrun and invalid values | REQ-51 | Boundary value analysis |
| IT-66 | An unknown state filter is rejected | REQ-50 | Negative testing |
| IT-67 | An unknown API route is reported | REQ-53 | Negative testing |
| IT-68 | Responses declare a nosniff policy | REQ-63 | Security testing |
| IT-68b | A malformed body carries an error code | REQ-53, REQ-60 | Negative testing |
| IT-68c | A malformed body leaks no stack trace | REQ-64 | Security testing |
| IT-69 | The web interface is served at the root | REQ-62 | Smoke |
| IT-70 to IT-72 | Capacity and confirmation under simultaneous requests | REQ-29, REQ-41 | Race condition |

## Selenium: the browser

| ID | Case | Requirement | Technique |
|----|------|-------------|-----------|
| UI-01 | The workspace stays hidden until a key is accepted | REQ-01 | State based |
| UI-02 | An unknown key is reported, workspace stays hidden | REQ-02 | Negative testing |
| UI-03 | An empty key is refused before any request | REQ-01 | Client side validation |
| UI-04 | An operator signs in and sees their role | REQ-04 | Specification based |
| UI-05 | A viewer sees a read only workspace | REQ-20 | Access control matrix |
| UI-06 | Only active stations are offered | REQ-31 | Specification based |
| UI-07 | A valid booking appears as REQUESTED | REQ-40 | End to end |
| UI-08 | The form is cleared after success | — | Usability |
| UI-09 | A short purpose is caught before the request | REQ-21 | Client side validation |
| UI-10 | A server rejection is shown to the user | REQ-23, REQ-53 | Error handling |
| UI-11 | An operator may not request an emergency slot | REQ-27 | Access control matrix |
| UI-12 | A supervisor can confirm a requested booking | REQ-43 | End to end |
| UI-13 | An operator sees no confirm button | REQ-43 | Access control matrix |
| UI-14 | The state filter narrows the table | REQ-50 | Specification based |
| UI-15 | The empty state is shown when nothing matches | — | Usability |
| UI-16 | Markup in a purpose is rendered as text | REQ-63 | Security testing |
| UI-17 | Signing out clears the session | REQ-01 | State based |
| UI-18 | A capacity clash is surfaced as an error | REQ-29 | Error handling |
| UI-19 | A cancelled booking loses its actions | REQ-41 | State based |

## Manual and exploratory

| ID | Case | Requirement | Technique |
|----|------|-------------|-----------|
| MT-01 | First use without instructions | REQ-01, REQ-04 | Usability |
| MT-02 | Error messages are understandable | REQ-53 | Usability |
| MT-03 | Keyboard only operation | REQ-63 | Accessibility |
| MT-04 | Small screen layout | — | Visual |
| MT-05 | Cross browser smoke | REQ-63 | Compatibility |
| MT-06 | The automated suite can be made to fail | — | Verification of the tests |
| EX-01 | Boundaries of the priority override | REQ-28 | Exploratory, found DEF-001 |
| EX-02 | The API contract against its documentation | REQ-26 | Exploratory, found DEF-003 |
| EX-03 | Attacking the capacity rule | REQ-29 | Exploratory |
| EX-04 | Behaving like a careless client | REQ-60, REQ-64 | Exploratory, found DEF-004 |

## Performance

| ID | Case | Technique |
|----|------|-----------|
| PERF-01 | Reading the schedule under 10 concurrent users | Load, steady state |
| PERF-02 | Creating bookings under a ramp to 5 users | Load, ramp |
| PERF-03 | Every response is 201 or an explained 409 | Correctness under load |

A 409 from a saturated station is a correct answer, not a failure, so the booking request declares
`expectedStatuses(201, 409)`. Without that, `http_req_failed` counts every legitimate capacity
rejection as an error and the threshold fails for the wrong reason.

Thresholds: failed requests under 1 percent, overall p95 under 300 ms and p99 under 800 ms, list
p95 under 150 ms, create p95 under 250 ms, check pass rate above 99 percent.

## Requirements with no automated coverage

| Requirement | Why |
|-------------|-----|
| REQ-61 | The 64 KiB body limit cannot be reached through the booking endpoint, because field validation rejects an oversized payload first. Recorded as OBS-04. |
| REQ-64 | A genuine unexpected server error cannot be triggered from outside. The closest check is IT-68c, which is currently failing as part of [DEF-004](defects/DEF-004.md). |
| REQ-54 | Timestamp formatting is asserted indirectly through round trips rather than by a dedicated format test. |

## Known failures

| Case | Defect | Behaviour |
|------|--------|-----------|
| UT-15, IT-35 | [DEF-001](defects/DEF-001.md) | An emergency booking can be backdated |
| IT-32 | [DEF-002](defects/DEF-002.md) | The documented 15 minute boundary is refused |
| IT-68b, IT-68c | [DEF-004](defects/DEF-004.md) | A malformed body bypasses the error contract |

These are tagged `KnownDefect`. The gating run excludes the category and is green, so a new
regression is obvious; a separate job runs only the tagged cases so the open findings stay visible.
[DEF-003](defects/DEF-003.md) is not in this table because it was fixed, and its regression test
UT-65 now runs in the gate.
