# QA Portfolio: testing a ground station booking service

[![tests](https://github.com/justhurky/qa-portfolio/actions/workflows/tests.yml/badge.svg)](https://github.com/justhurky/qa-portfolio/actions/workflows/tests.yml)
[![unit](https://img.shields.io/badge/unit-xUnit-512bd4)](tests/GroundStation.UnitTests)
[![selenium](https://img.shields.io/badge/E2E-Selenium%20WebDriver-43b02a)](tests/GroundStation.UiTests)
[![manual](https://img.shields.io/badge/manual-session%20based-0a6ebd)](manual-tests)
[![k6](https://img.shields.io/badge/performance-k6-7d64ff)](performance)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

A worked example of software testing: a documented specification, test cases derived from it with
named techniques, automation at the level where each case belongs, manual and exploratory testing
for the things automation covers badly, and defect reports for everything the testing found.

The application under test lives in this repository too, in
[`src/GroundStation.Api`](src/GroundStation.Api). It is not the point; it exists so the tests have
something real to run against, and so the whole thing can be cloned and run with one command.

**251 automated tests on three layers, plus manual and exploratory sessions. Four defects found:
three still open, one fixed with a regression test.**

## The defects, first

A suite where everything passes proves very little.

| | Defect | Severity | Status | How it was found |
|---|---|---|---|---|
| [DEF-001](docs/defects/DEF-001.md) | An emergency booking can be created entirely in the past: the priority override removes the lead time check instead of relaxing it. | Major | Open | Exploratory session EX-01 |
| [DEF-002](docs/defects/DEF-002.md) | The documented 15 minute minimum lead time is unreachable over HTTP, although the rule itself is correct. | Minor | Open | Boundary value analysis, confirmed by hand |
| [DEF-003](docs/defects/DEF-003.md) | The API rejected the priority value `ROUTINE` that it returns in its own responses, which broke the entire web interface. | Critical | **Fixed** | Exploratory session EX-02 |
| [DEF-004](docs/defects/DEF-004.md) | A malformed body bypasses the error contract: an empty 400 in production, a full stack trace in development. | Major | Open | Exploratory session EX-04 |

Two of these are worth a second look.

**DEF-002 is a disagreement between two layers.** The unit test proves the lead time rule is
correct, because it injects `now` and the boundary is exact. The integration test proves the same
rule is unreachable through HTTP, because the clock moves while the request travels. Both results
are true. Only running both shows the product is broken where the rule is not.

**DEF-003 is the one the automation missed.** The integration tests had been written with C# style
values, so they asserted what the code did rather than what the specification said, and they passed
happily while the web interface could not create a booking at all. Exploratory testing found it in
twenty minutes. The regression test now asserts a property, that every value the API renders is a
value the API accepts, rather than a list of literals that would be just as easy to get wrong a
second time.

The three open defects are not deleted and not relaxed. Each failing case is tagged `KnownDefect`:
the gating run excludes the category and stays green so a real regression is obvious, and a
separate CI job runs only the tagged cases so the findings stay on the dashboard.

## How the cases were designed

The full reasoning is in [docs/test-design.md](docs/test-design.md).

- **Which layer gets which case** is the first decision, before what to assert. A case belongs at
  the lowest level that can actually prove it.
- **Equivalence partitioning and boundary value analysis** on the three numeric ranges: duration,
  lead time and purpose length, each at `min-1`, `min`, `max`, `max+1`.
- **A decision table** for booking creation, where role, priority and timing decide the outcome
  together. Nine rules, all nine automated twice, including which check wins when two would refuse.
- **State transition testing** of the booking lifecycle: every valid transition walked, every
  invalid one refused, the longest path walked end to end, at both unit and HTTP level.
- **An access control matrix** across four roles and seven operations, where every "may not" cell
  is its own negative test.
- **Property based contract tests** after DEF-003 showed what asserting literals misses.
- **Risk based prioritisation**, so the depth goes where a defect would hurt.

Every case traces to a numbered requirement in [docs/test-cases.md](docs/test-cases.md), and the
three requirements with no automated coverage are listed there with the reason.

## The layers

| Layer | Stack | Tests | What it is for |
|-------|-------|-------|----------------|
| [Unit](tests/GroundStation.UnitTests) | xUnit, C# | 132 gating + 3 tagged | The rules in isolation, with `now` injected so boundaries are exact and time is not a hazard |
| [Integration](tests/GroundStation.ApiTests) | xUnit, `WebApplicationFactory` | 91 gating + 6 tagged | Status codes, error bodies, wiring and races, in process with no port to manage |
| [End to end](tests/GroundStation.UiTests) | Selenium WebDriver, NUnit, page objects | 19 | What only exists in the browser: client side validation, role dependent controls, output encoding |
| [Manual](manual-tests) | Scripted cases and session based exploration | 6 cases, 4 charters | Wording, layout, keyboard use, and going where no test case exists yet |
| [Performance](performance) | k6 | 3 scenarios with thresholds | A baseline that fails on a regression of an order of magnitude |

The browser suite uses `WebDriverWait` everywhere and no `Thread.Sleep` anywhere, with a page
object over stable `data-testid` selectors. The integration suite needs no running server: the host
runs in process, which is why 97 HTTP level tests finish in under a second.

## Manual testing

[`manual-tests/`](manual-tests) is not a folder of leftovers. It holds the cases that automation
covers badly, with steps and expected results, plus session based exploratory charters with
timeboxes and the notes taken while testing.

Two of the four defects came from those sessions, and one of them, DEF-003, had been sitting under
a green automated suite. The folder also records five observations that are not defects:
specification gaps and undocumented but acceptable behaviour, which are worth writing down rather
than remembering.

## Running it

Requirements: .NET 9 SDK, Chrome for the browser suite, k6 for the performance run.

```bash
dotnet test tests/GroundStation.UnitTests --filter "Category!=KnownDefect"
dotnet test tests/GroundStation.ApiTests  --filter "Category!=KnownDefect"
dotnet test tests/GroundStation.UiTests
```

The browser suite starts and stops the service itself, so nothing needs to be running first. To see
the defects instead of the gate:

```bash
dotnet test --filter "Category=KnownDefect"
```

To drive the application by hand:

```bash
dotnet run --project src/GroundStation.Api
```

Then open <http://localhost:5000> and sign in with `operator-key`, `supervisor-key`, `admin-key` or
`viewer-key` to see the role differences the tests assert.

## The system under test

A booking service for ground station antenna time, with rules chosen so the standard techniques
have something to work on: four roles with inherited permissions, a four state lifecycle with an
explicit transition table, three numeric ranges with real boundaries, per-station capacity where
overlapping and merely touching windows must be told apart, and a priority that overrides one rule
but not the others.

The domain rules live in [`Domain/`](src/GroundStation.Api/Domain) as pure functions that take
`now` as a parameter, which is what makes the unit layer both fast and deterministic. The
specification is in [docs/requirements.md](docs/requirements.md), with 40 numbered requirements.

## Documentation

| Document | Contents |
|----------|----------|
| [test-plan.md](docs/test-plan.md) | Scope, levels and why each exists, entry and exit criteria, risks, defect handling |
| [requirements.md](docs/requirements.md) | The 40 numbered requirements the tests were designed from |
| [test-design.md](docs/test-design.md) | Layer selection, partitions, boundaries, decision table, state machine, access matrix, risk ranking |
| [test-cases.md](docs/test-cases.md) | Every case with its requirement, technique and layer, plus the coverage gaps |
| [manual-tests/](manual-tests) | Manual cases, execution log and exploratory charters |
| [defects/](docs/defects) | Four defect reports with reproduction, impact and suggested fix |

## License

MIT, see [LICENSE](LICENSE).
