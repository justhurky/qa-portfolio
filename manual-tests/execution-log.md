# Manual test execution log

What was actually run, against which build, and what happened. A test case that has never been
executed is a plan, not evidence, so the two are kept apart.

## Cycle 1 — build 1.0.0

**Environment:** Windows 11, Chrome 131, Firefox 133, Edge 131, service running from source with
`dotnet run`, Production environment, seeded data reset before each case.
**Tester:** D. Lóki

| Case | Result | Notes |
|------|--------|-------|
| MT-01 First use without instructions | Pass | Step 4 is the weakest point: on a 1366×768 laptop the bookings table starts below the fold |
| MT-02 Error messages are understandable | Pass with observation | OBS-01: the lead time message contradicts a request for exactly 15 minutes. Cause is DEF-002 |
| MT-03 Keyboard only operation | Pass | Focus order matches visual order; every control reachable |
| MT-04 Small screen layout | **Fail** | OBS-02: the table does not wrap below roughly 640 px and the page scrolls sideways |
| MT-05 Cross browser smoke | Pass | Firefox orders the `datetime-local` fields differently, which is browser behaviour |
| MT-06 The automated suite can be made to fail | Pass | Mutating the minimum duration, the capacity check and an error code each produced the expected failures |

**Exploratory sessions in this cycle:** EX-01, EX-02, EX-03, EX-04. Charters and notes in
[exploratory-charters.md](exploratory-charters.md).

**Defects raised:** [DEF-001](../docs/defects/DEF-001.md) from EX-01,
[DEF-003](../docs/defects/DEF-003.md) from EX-02, [DEF-004](../docs/defects/DEF-004.md) from EX-04.
[DEF-002](../docs/defects/DEF-002.md) was raised from boundary analysis and confirmed here in
MT-02.

**Observations raised:** OBS-01 to OBS-05.

**Conclusion:** MT-04 fails against no requirement, so it does not block. The three open defects
are accepted and tagged. The build is fit for the purpose of this repository, which is to be tested.

## Cycle 2 — build 1.0.0 after the DEF-003 fix

**Scope:** Confirm the fix and check it broke nothing nearby.

| Check | Result |
|-------|--------|
| `priority: "ROUTINE"`, `"PRIORITY"`, `"EMERGENCY"` over the API | Pass, all accepted |
| `priority: "Routine"`, `"routine"`, `"URGENT"` | Pass, all refused with `VALIDATION_FAILED` |
| Creating a booking from the web interface with each priority | Pass, the dropdown now works for all three |
| Round trip: read a booking, post its priority back | Pass |
| Full automated gate after the fix | Pass, 242 tests green |
| Known defect suite after the fix | 9 failures, all expected: DEF-001, DEF-002, DEF-004 |

**Conclusion:** DEF-003 closed. The regression is locked down by UT-65, which asserts the round
trip property rather than a list of values.

## Regression checklist for the next cycle

Short list of what to re-run by hand after any change to the booking rules, since these are the
places where automation is weakest.

1. MT-02, because error wording is not asserted anywhere.
2. MT-05, because the automated browser suite only runs Chrome.
3. A spot check of the priority dropdown, because that is where DEF-003 showed up.
4. MT-06, if a large block of tests was added, to confirm they can still fail.
