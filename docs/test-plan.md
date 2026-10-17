# Test plan

## What is being tested

The Ground Station Booking service in [`src/GroundStation.Api`](../src/GroundStation.Api): an
ASP.NET Core API with a small web interface, where operators reserve antenna time slots and
supervisors approve them. It has four roles, a booking state machine and a set of scheduling rules.
It is a purpose-built system under test, small enough to specify completely in
[requirements.md](requirements.md) and rich enough that the standard design techniques have
something to bite on.

## Objectives

1. Show that the documented requirements hold, each at the level where it is best checked.
2. Find the places where the implementation and the specification disagree, and report them so that
   the difference becomes a decision rather than an accident.
3. Keep the automated suite fast and deterministic enough to gate every commit.

## Scope

**In scope.** The domain rules, the HTTP contract, the role model, the state machine, the web
interface as a user drives it, a performance baseline, and the security properties this application
can realistically get wrong: authentication, authorisation, output encoding and path traversal.

**Out of scope.** Penetration testing, soak testing, installation and upgrade testing, and browser
coverage beyond Chrome in automation. The reasoning is in [test-design.md](test-design.md).

## Levels, and why each exists

| Level | Tool | Count | Purpose |
|-------|------|-------|---------|
| Unit | xUnit | 135 | The rules in isolation. `now` is a parameter, so time is deterministic and the boundaries can actually be hit |
| Integration | xUnit with the in-process host | 97 | Status codes, error bodies, wiring and concurrency, without a network or a port |
| End to end | Selenium WebDriver, NUnit | 19 | What only exists in the browser: client side validation, role dependent controls, output encoding |
| Manual | Scripted cases and session based exploration | 6 + 4 | Judgement: wording, layout, keyboard use, and going where no case exists yet |
| Performance | k6 | 3 scenarios | A baseline with thresholds |

Running a case at two levels is deliberate where the levels can disagree. The lead time rule is
unit tested with an injected clock and integration tested with the real one, and the two results
differ: that difference is [DEF-002](defects/DEF-002.md).

## Entry criteria

- The solution builds and `GET /api/health` answers 200.
- `POST /api/test/reset` is available, so every case starts from known seeded data.
- The requirements are documented and numbered.

## Exit criteria

- Every case in [test-cases.md](test-cases.md) has run.
- The gating suite is green; nothing fails for a reason that is not an accepted, documented defect.
- Every known failure has a defect report with reproduction steps, impact and a suggested fix.
- The performance thresholds hold.

## How defects are handled

A failing test is not deleted and not relaxed to make a build green. It is reproduced by hand,
written up in [`docs/defects/`](defects) with steps, actual and expected behaviour, an impact
assessment and a suggested fix, and then tagged `KnownDefect`. The gating run excludes the
category so the pipeline reports new regressions clearly; a separate job runs only the tagged cases
so the open findings stay on the dashboard. When a defect is fixed the tag comes off and the case
rejoins the gate, which is what happened with [DEF-003](defects/DEF-003.md).

Current state: DEF-001, DEF-002 and DEF-004 open; DEF-003 fixed with a regression test.

## Environment and data

A single process with in-memory state. Seeded data is fixed: four stations with capacities of 1, 2,
1 and an inactive one, and five API keys covering every role. Every case resets the state first,
which is what lets the suite run in any order.

Two environment notes that turned out to matter. The scheduling rules are relative to the clock, so
every timestamp in the tests is computed from `now` at run time; a hard-coded date passes today and
fails next month. And the integration tests run in the Development environment while a deployed
service runs in Production, which is how [DEF-004](defects/DEF-004.md) came to show two different
symptoms.

## Risks to the test effort

| Risk | Mitigation |
|------|------------|
| Time dependent rules make tests flaky near boundaries | Boundaries are computed from `now`; the one boundary that cannot be hit is reported as DEF-002 rather than papered over with a sleep |
| Shared state causes order dependence | Every case resets the system under test first; each test class gets its own host instance |
| The browser suite becomes slow and flaky | Page object with stable `data-testid` selectors, `WebDriverWait` everywhere, no `Thread.Sleep` anywhere |
| Tests assert the implementation instead of the contract | DEF-003 is exactly this failure. The regression test asserts a property rather than literals, and the integration tests were rewritten to use documented values |
| Performance numbers vary with the runner | Thresholds sit well above observed values, so they catch regressions of an order of magnitude rather than noise |

## Reporting

Each suite emits machine readable results: TRX from `dotnet test` and a JSON summary from k6. CI
publishes them as artefacts and writes the open defects into the run summary.
