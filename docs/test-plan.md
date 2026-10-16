# Test plan

## What is being tested

The Ground Station Booking API in [`sut/`](../sut), a service where operators reserve antenna time
slots and supervisors approve them. It has a REST API, a small web interface, four roles, a booking
state machine and a set of scheduling rules. It is a purpose-built system under test: small enough
to specify completely in [requirements.md](requirements.md), and rich enough that the usual test
design techniques have something to bite on.

## Objectives

1. Show that the documented requirements hold, at the level where each one is best checked.
2. Find the places where the implementation and the specification disagree, and report them so the
   difference is a decision rather than an accident.
3. Keep the suite fast and deterministic enough to gate every commit.

## Scope

**In scope.** Functional behaviour of the API, the booking rules, the role model and the state
machine; the contract shape of every response; the web interface as a user drives it; baseline
performance; and the handful of security properties this application can realistically get wrong,
namely authentication, authorisation, path traversal and output encoding.

**Out of scope.** Penetration testing, sustained load and soak testing, accessibility beyond
semantic markup, installation and upgrade testing, and browser coverage beyond Chromium. The
reasoning for each is in [test-design.md](test-design.md).

## Test levels and the reason for each

| Level | Tool | What it is for |
|-------|------|----------------|
| API functional | Java, JUnit 5, REST Assured | The rules live in the API, so most cases belong here: fast, precise and able to set up exactly the state a case needs. |
| Contract | Python, pytest, JSON Schema | Separate from the functional cases on purpose. A response can be functionally right and still break a client by changing shape, so the shape is asserted independently. |
| Concurrency | Python, pytest, threads | Capacity and state transitions are the two places where two users racing each other could both win. |
| End to end | Playwright | Proves the interface and the API agree, and covers the parts that only exist in the browser: client side validation, role dependent controls, output encoding. |
| Performance | k6 | A baseline with thresholds, so a change that makes the service ten times slower fails rather than being noticed later. |

Cases are written at the lowest level that can prove them. Role rules are checked through the API
rather than the interface, except where the interface itself makes a decision, such as hiding a
button, because that decision is only visible in the browser.

## Entry criteria

- The system under test starts and answers `GET /api/health` with 200.
- `POST /api/test/reset` is available, so each test begins from known seeded data.
- The requirements are documented and have identifiers.

## Exit criteria

- Every case in [test-cases.md](test-cases.md) has run.
- The gating suite is green; no case fails for a reason that is not an accepted, documented defect.
- Every known failure has a defect report with reproduction steps and an assessment.
- Performance thresholds hold.

## Approach to defects

A failing test is not deleted or relaxed to make a build green. It is reproduced by hand, written up
in [`docs/defects/`](defects) with steps, actual and expected behaviour, an impact assessment and a
suggested fix, and then tagged `known-defect`. The gating run excludes that tag so the pipeline
reports new regressions clearly, and a separate job runs only the tagged cases so the open findings
stay visible. When a defect is fixed, the tag comes off and the case rejoins the gate.

Three defects were found and are open: [DEF-001](defects/DEF-001.md),
[DEF-002](defects/DEF-002.md) and [DEF-003](defects/DEF-003.md).

## Environment and data

A single Node process with in-memory state, started from `sut/`. Seeded data is fixed: four
stations with capacities of 1, 2, 1 and an inactive one, and five API keys covering every role.
Tests reset the state before each case rather than sharing it, which is what lets the suite run in
any order.

Time matters here, because the scheduling rules are relative to the current clock. Every timestamp
in the tests is computed from `now` at run time. A test that hard-codes a date passes today and
fails next month.

## Risks to the test effort

| Risk | Mitigation |
|------|------------|
| Time dependent rules make tests flaky near boundaries | Boundaries are computed from `now`; the one boundary that cannot be hit reliably is reported as [DEF-002](defects/DEF-002.md) rather than papered over with a sleep |
| Shared state between cases causes order dependence | Every case resets the system under test first |
| The interface suite becomes slow and flaky | Page object with stable `data-testid` selectors, auto-waiting assertions, no fixed sleeps anywhere |
| Performance numbers vary with the runner | Thresholds are set well above observed values, so they catch regressions of an order of magnitude rather than noise |

## Reporting

Each suite emits machine readable results: Surefire XML from Maven, JUnit XML from pytest and
Playwright, and a k6 summary. CI publishes them as artefacts, together with Playwright traces and
screenshots for any failure.
