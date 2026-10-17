# Test design

How the cases in [test-cases.md](test-cases.md) were derived. They are not a list of things that
came to mind: each one comes from a technique applied to a numbered requirement, which is also why
the suite is focused rather than sprawling.

## Which layer gets which case

The first design decision is not *what* to test but *where*. A case belongs at the lowest layer
that can actually prove it.

| Question | Layer | Why |
|----------|-------|-----|
| Is the rule itself right? | Unit | Milliseconds, deterministic, `now` is a parameter so time is not a hazard |
| Do the rules compose into the documented HTTP behaviour? | Integration | Status codes, error bodies, wiring, concurrency |
| Does the person in front of the browser get what they need? | Selenium | Client side validation, role dependent controls, output encoding |
| Does it read well, work by keyboard, survive a small screen? | Manual | Judgement, not assertions |
| Is it fast enough, and does it stay correct under load? | k6 | Thresholds that fail on a regression of an order of magnitude |

[DEF-002](defects/DEF-002.md) is the clearest illustration. The unit test proves the lead time rule
is correct, because it injects `now`. The integration test proves the same rule is unreachable
through HTTP, because the clock moves between building the request and evaluating it. Both are
true, and only running both shows it.

## Equivalence partitioning and boundary value analysis

Three numeric ranges, each tested at `min-1`, `min`, `max`, `max+1` plus a value inside.

### Duration, 5 to 120 minutes (REQ-23)

| Partition | Values | Expected |
|-----------|--------|----------|
| Below | 0, 1, 4, 4.9 | refused, `DURATION_OUT_OF_RANGE` |
| At the lower bound | 5 | accepted |
| Inside | 6, 30, 119 | accepted |
| At the upper bound | 120 | accepted |
| Above | 120.1, 121, 1440 | refused |
| Degenerate | 0 and negative | refused earlier, on the ordering rule |

The degenerate row is separate deliberately: a zero or reversed window fails REQ-22 before REQ-23
is reached, so it produces a different status. `UT-05` pins that ordering down, because validation
order is a contract too.

### Lead time, 15 minutes to 30 days (REQ-24, REQ-25)

| Partition | Values | Expected |
|-----------|--------|----------|
| In the past | −1440, −1 | refused, `LEAD_TIME_TOO_SHORT` |
| Below | 0, 14, 14.9 | refused |
| At the lower bound | 15 | accepted, and at unit level it is |
| Inside | 16, 60, 2 days | accepted |
| At the upper bound | 43200 minutes | accepted |
| Above | 43201, 50000 | refused, `LEAD_TIME_TOO_LONG` |

### Purpose length, 3 to 200 characters (REQ-21)

Boundaries at 1, 2, 3, 4, 199, 200, 201 and 500, plus whitespace only and `null`, because the field
is trimmed before it is measured (`UT-09`).

## Decision table

Creating a booking depends on three inputs at once: the caller's role, the requested priority, and
whether the start time is inside the normal lead window.

| Rule | Role | Priority | Lead ≥ 15 min | Expected |
|------|------|----------|---------------|----------|
| R1 | Viewer | any | any | 403 `ACCESS_DENIED` |
| R2 | Operator | ROUTINE | yes | 201 |
| R3 | Operator | ROUTINE | no | 422 `LEAD_TIME_TOO_SHORT` |
| R4 | Operator | PRIORITY | yes | 201 |
| R5 | Operator | PRIORITY | no | 422 `LEAD_TIME_TOO_SHORT` |
| R6 | Operator | EMERGENCY | any | 403 `PRIORITY_NOT_PERMITTED` |
| R7 | Supervisor | EMERGENCY | yes | 201 |
| R8 | Supervisor | EMERGENCY | no | 201, the override in REQ-28 |
| R9 | Admin | EMERGENCY | no | 201, role inheritance |

All nine rows are automated twice: as pure rules in the unit suite and over HTTP in `IT-40`.
R6 also pins the precedence question, since both the role rule and the timing rule would refuse it
and only one of them can answer. R8 is the rule whose other side turned out to be missing, which is
[DEF-001](defects/DEF-001.md).

## State transition testing

```
      ┌──────────── cancel ───────────┐
      │                               ▼
  REQUESTED ── confirm ──▶ CONFIRMED ──▶ COMPLETED
      │                        │
      └──── cancel ──▶ CANCELLED ◀── cancel
```

0-switch coverage walks every single transition, valid and invalid, at unit level (`UT-31`,
`UT-32`) and again over HTTP (`IT-51`, `IT-52`). `UT-35` asserts that no state may transition to
itself, and `IT-53` walks the longest valid path end to end, because defects hide in the second
step more often than the first.

## Access control matrix

| Operation | Viewer | Operator | Supervisor | Admin |
|-----------|--------|----------|------------|-------|
| Read stations and bookings | yes | yes | yes | yes |
| Create booking | no | yes | yes | yes |
| Create EMERGENCY booking | no | no | yes | yes |
| Cancel own booking | n/a | yes | yes | yes |
| Cancel another user's booking | no | no | yes | yes |
| Confirm, complete | no | no | yes | yes |
| Create station | no | no | no | yes |

Every `no` is its own negative test. An authorisation rule that is only ever tested positively is
an assumption, not a control. The matrix is covered at unit level in `UT-41` to `UT-47` and through
HTTP in `IT-42` to `IT-45` and `IT-54` to `IT-56`.

## Contract testing

A response can be functionally right and still break a client by changing shape, so the wire
contract is asserted separately from the behaviour. `UT-60` to `UT-66` cover the priority
vocabulary, including the round trip property that what the API renders is what the API accepts.
That property test exists because asserting a list of literals is exactly as easy to get wrong the
second time as it was the first; see [DEF-003](defects/DEF-003.md).

## Concurrency

Capacity and state transitions are the two places where two users racing each other could both
win. `IT-70` to `IT-72` fire simultaneous requests with `Task.WhenAll` and assert the exact split:
one winner and one `409` on a single slot station, two winners and two rejections on a station with
capacity two, one `200` and one `409` when the same booking is confirmed twice at once.

## Risk based prioritisation

| Area | Impact if wrong | Depth |
|------|-----------------|-------|
| Authorisation and priority rules | A user performs an operation they must not | Full decision table at two layers, every negative case |
| Overlap and capacity | Two missions pointed at one antenna | Boundaries including touching and contained windows, plus races |
| Lifecycle transitions | A cancelled booking is executed anyway | Every transition, valid and invalid, at two layers |
| Validation boundaries | Bad data is stored | Parameterised boundaries, mostly at unit level where they are cheap |
| Wire contract | Clients silently break | Property based round trip, after one real defect |
| Pagination and ordering | Cosmetic, recoverable | Smoke level only |

## What is deliberately not covered

- No penetration testing. The security checks here are authentication, authorisation, output
  encoding and path traversal, which are the ones this application can realistically get wrong.
- No sustained load or soak testing. The service keeps everything in memory, so a long run would
  measure the runtime rather than the application.
- No browser matrix beyond Chrome in automation; Firefox and Edge are covered once per release by
  hand in [MT-05](../manual-tests/test-cases.md).
