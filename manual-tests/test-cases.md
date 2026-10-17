# Manual test cases

Scripted cases for the areas automation covers badly. Each one names the requirement it traces to,
so the manual effort is visible in the coverage picture rather than being folklore.

Preconditions for every case: the service is running, the browser is at the root page, and
`POST /api/test/reset` has been called so the seeded data is in its known state.

---

## MT-01 First use without instructions

**Traces to** REQ-01, REQ-04 · **Type** Usability · **Duration** 5 minutes

| # | Step | Expected |
|---|------|----------|
| 1 | Open the root page with no prior knowledge | It is obvious that a key is needed before anything can be done; no booking controls are visible |
| 2 | Click *Sign in* with the field empty | A message explains what is missing, in a sentence rather than an error code |
| 3 | Enter `operator-key` and sign in | The identity and role appear, and the workspace becomes visible in one step |
| 4 | Look for how to make a booking | The form is visible without scrolling on a 1280×900 window |

**Result:** Pass. Step 4 is the weakest point: on a 1366×768 laptop the bookings table starts below
the fold, which is acceptable but worth knowing.

---

## MT-02 Error messages are understandable

**Traces to** REQ-53 · **Type** Usability · **Duration** 10 minutes

| # | Step | Expected |
|---|------|----------|
| 1 | As `operator-key`, request a 4 hour slot | The message names the limit in minutes and does not show a code or a stack trace |
| 2 | Request a slot 5 minutes from now | The message explains the minimum lead time in plain words |
| 3 | Request a second overlapping slot on KIR | The message says the station already has an overlapping booking |
| 4 | As `operator-key`, choose Emergency | The message names the role required, not just "forbidden" |

**Result:** Pass with one observation, recorded as OBS-01 in the execution log: the lead time
message says "at least 15 minutes from now" while the request was for exactly 15 minutes, which
reads as a contradiction. The underlying cause is [DEF-002](../docs/defects/DEF-002.md).

---

## MT-03 Keyboard only operation

**Traces to** REQ-63 · **Type** Accessibility · **Duration** 10 minutes

| # | Step | Expected |
|---|------|----------|
| 1 | From page load, press Tab repeatedly | Focus reaches the key field, sign in, then every form control in visual order |
| 2 | Sign in using only Enter | The workspace opens |
| 3 | Fill the form with Tab and typing, submit with Enter | The booking is created without touching the mouse |
| 4 | Tab to a row action and press Enter | The action runs |

**Result:** Pass. Every control is reachable and the order matches the visual order.

---

## MT-04 Small screen layout

**Traces to** — · **Type** Visual · **Duration** 10 minutes

| # | Step | Expected |
|---|------|----------|
| 1 | Resize to 375 px wide | The form stacks into one column, nothing overflows horizontally |
| 2 | Create a booking | The confirmation message is visible without horizontal scrolling |
| 3 | Inspect the bookings table | The table scrolls or wraps rather than pushing the page wider than the viewport |

**Result:** Fail at step 3. The table does not wrap and the page scrolls sideways below roughly
640 px. Recorded as OBS-02; not raised as a defect because no requirement covers small screens, but
it is the first thing to fix if the interface is ever used on a phone.

---

## MT-05 Cross browser smoke

**Traces to** REQ-63 · **Type** Compatibility · **Duration** 15 minutes

| # | Step | Expected |
|---|------|----------|
| 1 | Repeat the happy path in Chrome | Booking created, row appears |
| 2 | Repeat in Firefox | Same |
| 3 | Repeat in Edge | Same |
| 4 | Compare the `datetime-local` control in all three | Each offers a usable picker; the submitted value is the same instant |

**Result:** Pass in Chrome and Edge. In Firefox the `datetime-local` picker presents the fields in
a different order, which is browser behaviour rather than a defect, but it is the reason the
automated UI suite sets the value through the DOM rather than by typing into the control.

---

## MT-06 The automated suite is telling the truth

**Traces to** — · **Type** Verification of the tests themselves · **Duration** 10 minutes

| # | Step | Expected |
|---|------|----------|
| 1 | Break one domain rule on purpose, for example change the minimum duration to 1 | The unit suite fails, and the failing test names the rule |
| 2 | Revert, then break the capacity check | Unit and integration suites both fail |
| 3 | Revert, then change an error code string | The tests asserting the code fail rather than passing silently |
| 4 | Revert and run everything | Green again |

**Result:** Pass. A suite that cannot be made to fail is not evidence of anything, so this is worth
repeating whenever a large block of tests is added.
