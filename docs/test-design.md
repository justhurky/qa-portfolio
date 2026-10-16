# Test design

How the test cases in [test-cases.md](test-cases.md) were derived. The point of this document is
that the cases are not a list of things that came to mind: each one comes from a technique applied
to a documented requirement, which is also why the suite is small rather than sprawling.

## 1. Equivalence partitioning and boundary value analysis

Three input fields carry numeric or length ranges, so each one is partitioned and then tested at its
boundaries. For a range `[min, max]` the interesting points are `min-1`, `min`, `max`, `max+1`, which
is what the parameterised tests feed in.

### Duration (REQ-23, 5 to 120 minutes)

| Partition | Representative | Expected |
|-----------|----------------|----------|
| Below range | 4 minutes | 422 `DURATION_OUT_OF_RANGE` |
| Lower boundary | 5 minutes | 201 |
| Inside range | 30 minutes | 201 |
| Upper boundary | 120 minutes | 201 |
| Above range | 121 minutes | 422 `DURATION_OUT_OF_RANGE` |
| Degenerate | 0 minutes, negative | 400 `VALIDATION_FAILED` on `endsAt` |

The degenerate row is separate on purpose: a zero or negative duration fails the ordering rule
(REQ-22) before the duration rule (REQ-23) is reached, so it produces a different status and code.
Validation order is itself a thing worth pinning down in tests.

### Lead time (REQ-24, REQ-25, 15 minutes to 30 days)

| Partition | Representative | Expected |
|-----------|----------------|----------|
| In the past | −60 minutes | 422 `LEAD_TIME_TOO_SHORT` |
| Below minimum | 14 minutes | 422 `LEAD_TIME_TOO_SHORT` |
| Lower boundary | 15 minutes | 201 |
| Inside range | 2 days | 201 |
| Upper boundary | 30 days | 201 |
| Above range | 30 days + 1 minute | 422 `LEAD_TIME_TOO_LONG` |

Lead time is relative to the clock, so these cases are built from `now` at run time rather than from
fixed timestamps. Tests that hard-code a date pass in the morning and fail after midnight.

### Purpose length (REQ-21, 3 to 200 characters)

Boundaries at 2, 3, 200 and 201 characters, plus whitespace-only input, which must be rejected
because the field is trimmed before it is measured.

## 2. Decision table

Creating a booking depends on three inputs at once: the caller's role, the requested priority, and
whether the start time is inside the normal lead window. A table makes the combinations explicit and
shows which ones the suite actually exercises.

| Rule | Role | Priority | Lead ≥ 15 min | Expected |
|------|------|----------|---------------|----------|
| R1 | VIEWER | any | any | 403 `ACCESS_DENIED` (REQ-20) |
| R2 | OPERATOR | ROUTINE | yes | 201 |
| R3 | OPERATOR | ROUTINE | no | 422 `LEAD_TIME_TOO_SHORT` |
| R4 | OPERATOR | PRIORITY | yes | 201 |
| R5 | OPERATOR | PRIORITY | no | 422 `LEAD_TIME_TOO_SHORT` |
| R6 | OPERATOR | EMERGENCY | any | 403 `PRIORITY_NOT_PERMITTED` (REQ-27) |
| R7 | SUPERVISOR | EMERGENCY | yes | 201 |
| R8 | SUPERVISOR | EMERGENCY | no | 201, the override in REQ-28 |
| R9 | ADMIN | EMERGENCY | no | 201, role inheritance from REQ-03 |

R6 is checked before the lead time rule, so the role failure wins over the timing failure. R8 is the
rule that turned out to be implemented too generously, which is [DEF-001](defects/DEF-001.md).

## 3. State transition testing

The booking lifecycle (REQ-41) is a state machine, so it is tested as one: every valid transition is
covered at least once, and the invalid ones are sampled from each state, including the terminal
states where nothing at all is allowed.

```
      ┌──────────── cancel ───────────┐
      │                               ▼
  REQUESTED ── confirm ──▶ CONFIRMED ──▶ COMPLETED
      │                        │
      └──── cancel ──▶ CANCELLED ◀── cancel
```

| From | Event | To | Valid | Covered by |
|------|-------|----|-------|------------|
| REQUESTED | confirm | CONFIRMED | yes | TC-40 |
| REQUESTED | cancel | CANCELLED | yes | TC-41 |
| REQUESTED | complete | — | no | TC-45 |
| CONFIRMED | complete | COMPLETED | yes | TC-42 |
| CONFIRMED | cancel | CANCELLED | yes | TC-43 |
| CONFIRMED | confirm | — | no | TC-46 |
| COMPLETED | any | — | no | TC-47 |
| CANCELLED | any | — | no | TC-48 |

The 0-switch coverage above walks every single transition. TC-44 additionally walks the longest
valid path end to end, `REQUESTED → CONFIRMED → COMPLETED`, because defects often hide in the second
step rather than the first.

## 4. Role based access control matrix

| Operation | VIEWER | OPERATOR | SUPERVISOR | ADMIN |
|-----------|--------|----------|------------|-------|
| Read stations and bookings | yes | yes | yes | yes |
| Create booking | no | yes | yes | yes |
| Create EMERGENCY booking | no | no | yes | yes |
| Cancel own booking | n/a | yes | yes | yes |
| Cancel another user's booking | no | no | yes | yes |
| Confirm, complete | no | no | yes | yes |
| Create station | no | no | no | yes |

Each `no` is a test case, because an authorisation rule that is never tested in the negative is an
assumption rather than a control.

## 5. Risk based prioritisation

Not every requirement deserves the same depth. The suite spends its effort where a defect would hurt
most.

| Area | Impact if wrong | Likelihood | Depth |
|------|-----------------|------------|-------|
| Authorisation and priority rules | A user performs an operation they must not | medium | Full decision table, every negative case |
| Overlap and capacity | Two missions pointed at one antenna at the same time | medium | Boundaries including touching slots |
| Lifecycle transitions | A cancelled booking is executed anyway | low | Every transition, valid and invalid |
| Validation boundaries | Bad data is stored | high | Parameterised boundaries |
| Pagination and ordering | Cosmetic, recoverable | high | Smoke level only |
| Error body shape | Clients cannot react programmatically | medium | One structural assertion reused everywhere |

## 6. What is deliberately not covered

- No load beyond the k6 baseline in [`performance/`](../performance); the system under test keeps
  everything in memory, so a sustained load test would measure Node rather than the application.
- No cross-browser matrix beyond Chromium and Firefox in CI.
- No penetration testing. The security checks here are limited to authentication, authorisation,
  path traversal and output encoding, which are the ones this application can realistically get
  wrong.
