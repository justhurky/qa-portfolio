# QA Portfolio: testing a ground station booking service

[![tests](https://github.com/justhurky/qa-portfolio/actions/workflows/tests.yml/badge.svg)](https://github.com/justhurky/qa-portfolio/actions/workflows/tests.yml)
[![Java](https://img.shields.io/badge/API%20tests-JUnit%205%20%2B%20REST%20Assured-orange)](api-tests)
[![Playwright](https://img.shields.io/badge/E2E-Playwright-45ba4b)](ui-tests)
[![k6](https://img.shields.io/badge/Performance-k6-7d64ff)](performance)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

This repository is a worked example of testing a system properly: a documented specification, test
cases derived from it with named techniques, four layers of automation, and defect reports for the
three places where the implementation and the specification disagree.

The application being tested is in this repository too, in [`sut/`](sut). It is not the point of
the repository; it exists so the tests have something real to run against, and so the whole thing
can be checked out and executed in one command.

**148 automated tests across four layers. Three defects found, reproduced and reported.**

## The defects, first

A test suite where everything passes proves very little. These were found by applying the
techniques in [test-design.md](docs/test-design.md) and then reproduced by hand.

| | Defect | Severity | How it was found |
|---|---|---|---|
| [DEF-001](docs/defects/DEF-001.md) | An emergency booking can be created entirely in the past. The priority override removes the lead time check instead of relaxing it. | Major | Asking what is on the other side of a boundary the specification only describes from one side |
| [DEF-002](docs/defects/DEF-002.md) | The documented 15 minute minimum lead time is unreachable: a request for exactly `now + 15 min` is always refused, and the error reports 14. | Minor | Boundary value analysis |
| [DEF-003](docs/defects/DEF-003.md) | An oversized request body closes the TCP connection instead of answering 413, so the client sees a transport error with no code to react to. | Major | Checking the documented payload limit |

They are not fixed in the system under test, and the tests that expose them are not deleted. Each
one is tagged `known-defect`: the gating run excludes the tag and stays green so a real regression
is obvious, and a separate CI job runs only those three so the findings stay on the dashboard.

## How the tests were designed

The full reasoning is in [docs/test-design.md](docs/test-design.md). In short:

- **Equivalence partitioning and boundary value analysis** on the three numeric ranges: duration
  (5 to 120 minutes), lead time (15 minutes to 30 days) and purpose length (3 to 200 characters).
  Each is tested at `min-1`, `min`, `max`, `max+1`.
- **A decision table** for booking creation, where the outcome depends on role, priority and
  timing at the same time. Nine rules, all nine automated, including the precedence question of
  which check fails first when two of them would.
- **State transition testing** of the booking lifecycle: every valid transition walked, every
  invalid one sampled from each state, and the longest valid path walked end to end.
- **An access control matrix** across four roles and seven operations, where every "may not" cell
  is its own negative test, because an authorisation rule that is only tested positively is an
  assumption.
- **Risk based prioritisation** so the depth goes where a defect would hurt, not evenly everywhere.

Every case traces to a requirement in [docs/test-cases.md](docs/test-cases.md), and the two
requirements with no automated coverage are listed there with the reason.

## The layers

| Layer | Stack | Tests | What it is for |
|-------|-------|-------|----------------|
| [API functional](api-tests) | Java 21, JUnit 5, REST Assured, AssertJ | 114 gating + 3 tagged | The business rules live in the API, so most cases belong here |
| [Contract and concurrency](python-tests) | Python, pytest, JSON Schema | 13 | Response shape asserted separately from behaviour, plus four race condition tests |
| [End to end](ui-tests) | Playwright, page objects | 18 | The browser-only parts: client side validation, role dependent controls, output encoding |
| [Performance](performance) | k6 | 3 scenarios with thresholds | A baseline that fails on an order of magnitude regression |

Different stacks on purpose: the right tool differs per layer, and in practice a tester is expected
to be able to pick one up rather than bend everything to a single framework.

## Running it

Requirements: Node 22, JDK 21, Python 3.12, and k6 if you want the performance run.

```bash
node sut/server.js
```

Then, in another shell, with `SUT_BASE_URL=http://localhost:3000`:

```bash
cd api-tests && ./mvnw test                      # 114 gating tests
cd api-tests && ./mvnw test -Pknown-defects      # adds the 3 that expose the defects
cd python-tests && pip install -r requirements.txt && pytest -v
cd ui-tests && npm install && npx playwright install chromium && npx playwright test
k6 run performance/booking-load.js
```

The web interface is at <http://localhost:3000>. Sign in with `operator-key`, `supervisor-key`,
`admin-key` or `viewer-key` to see the role differences the tests assert.

CI runs all four layers on every push and publishes the reports, the Playwright traces and the k6
summary as artefacts.

## The system under test

A booking service for ground station antenna time, with rules chosen so that the standard test
design techniques have something to work on:

- Four roles with inherited permissions, and an operation each role may not perform
- A four state booking lifecycle with an explicit transition table
- Numeric ranges with real boundaries on duration, lead time and text length
- Per-station capacity, so overlapping bookings have to be reasoned about, including the case where
  two slots merely touch
- A priority that overrides one rule but not the others, which is where DEF-001 lives

Zero runtime dependencies: plain Node, in-memory state, a `POST /api/test/reset` hook so every test
starts from the same seeded data. The specification is in
[docs/requirements.md](docs/requirements.md), with 40 numbered requirements.

## Documentation

| Document | Contents |
|----------|----------|
| [test-plan.md](docs/test-plan.md) | Scope, levels and why each one exists, entry and exit criteria, risks, defect handling |
| [requirements.md](docs/requirements.md) | The 40 numbered requirements the tests were designed from |
| [test-design.md](docs/test-design.md) | Partitions, boundaries, the decision table, the state machine, the access matrix, risk ranking |
| [test-cases.md](docs/test-cases.md) | Every case with its requirement, technique and automation, plus the coverage gaps |
| [defects/](docs/defects) | Three defect reports with reproduction, impact and suggested fix |

## License

MIT, see [LICENSE](LICENSE).
