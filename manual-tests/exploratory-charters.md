# Exploratory testing charters

Session based testing: each session has a charter saying what is being explored, a timebox, and
notes written while testing rather than afterwards. Scripted cases check what is known; these
sessions look for what nobody thought to write down.

---

## EX-01 Explore the boundaries of the priority override

**Charter:** Explore the emergency priority override with boundary values to discover whether the
exemption is bounded on both sides.
**Timebox:** 30 minutes · **Tester:** D. Lóki · **Build:** 1.0.0

**Notes**

- The specification (REQ-28) says an emergency is exempt from the minimum lead time so it can start
  immediately. "Immediately" implies a floor of now. Does the implementation have one?
- Emergency at now + 1 minute: accepted, as intended.
- Emergency at now − 5 minutes: **accepted**. Unexpected.
- Emergency at now − 3 days: **accepted**.
- Emergency at now − 1 year: **accepted**. The booking appears in the schedule and can be confirmed
  and completed afterwards, producing a plausible looking record of a pass that never happened.
- The far horizon of 30 days is still enforced for emergencies, so the rule is bounded above but not
  below. The asymmetry is what gives it away.

**Outcome:** [DEF-001](../docs/defects/DEF-001.md). Automated afterwards as UT-15 and IT-35.

**Time split:** 20 minutes testing, 10 minutes writing the report.

---

## EX-02 Probe the API contract against its own documentation

**Charter:** Compare every documented wire value in the specification against what the API actually
accepts and returns, looking for places where the two disagree.
**Timebox:** 45 minutes · **Tester:** D. Lóki · **Build:** 1.0.0

**Notes**

- Started from REQ-26, which lists the priority values as `ROUTINE`, `PRIORITY`, `EMERGENCY`.
- Sent `"priority": "ROUTINE"`: **400 VALIDATION_FAILED**, and the message helpfully says
  "priority must be one of ROUTINE, PRIORITY, EMERGENCY", which is exactly what was sent.
- Sent `"priority": "Routine"`: **201 Created**, and the response body contains `"priority":
  "ROUTINE"`.
- So the API renders a value it will not accept back. Any client that reads a booking and writes it
  again is broken, and the web interface, whose dropdown sends `ROUTINE`, could not create a
  booking with an explicit priority at all.
- Checked the same question for the other enumerations: booking states in the `state` filter are
  matched case-insensitively and round trip correctly, station codes round trip correctly. The
  defect is specific to priority.
- Why the automated suite missed it: the integration tests had been written with C# style values
  (`"Emergency"`), because that is what the enum looks like in the test code. The tests encoded the
  implementation instead of the contract.

**Outcome:** [DEF-003](../docs/defects/DEF-003.md). Fixed, and locked down by UT-65, which asserts
the round trip property rather than a list of literals, so the same class of mistake cannot return.

**Time split:** 25 minutes testing, 10 minutes reporting, 10 minutes setup and re-checking.

---

## EX-03 Try to get two bookings onto one antenna

**Charter:** Explore the capacity rule by attacking it, rather than by checking the cases the
specification already describes.
**Timebox:** 30 minutes · **Tester:** D. Lóki · **Build:** 1.0.0

**Notes**

- Two overlapping requests sent at the same moment on a single slot station: one 201, one 409. The
  store serialises the capacity check, so the race is handled.
- Cancel a booking and immediately rebook the slot: accepted, as it should be, because a cancelled
  booking no longer occupies capacity.
- Book a slot, confirm it, then try to overlap: refused, so confirmed bookings also hold capacity.
- Complete a booking, then overlap it: **accepted**. Reasonable, because the pass is over, but it
  is not stated anywhere in the specification. Raised as a question rather than a defect:
  should a completed pass still block the antenna for its historical window? Recorded as OBS-03.
- Zero length and reversed windows are refused before capacity is considered, so they cannot be
  used to slip past the check.

**Outcome:** No defect. One specification gap, OBS-03, and three cases worth automating, which
became IT-36 to IT-39.

---

## EX-04 Behave like a careless client

**Charter:** Send the kinds of requests a real client sends by accident, and see whether the service
stays well behaved.
**Timebox:** 30 minutes · **Tester:** D. Lóki · **Build:** 1.0.0

**Notes**

- Body that is not JSON: 400, but with a **completely empty body**. No code, no message, nothing a
  client can react to.
- Body that is a JSON array rather than an object: the same empty 400.
- Ran the same requests against the in-process test host, which runs in the Development
  environment: the same 400 now carries a **full System.Text.Json stack trace**. Same root cause,
  two faces, and the severity depends on which environment is deployed. Worth knowing that probing
  only one configuration would have hidden half of it.
- Extra unknown fields including `state` and `requestedBy`: ignored, and the server supplies its
  own values. Checked the response to be sure they had not been taken.
- A 70000 character purpose: 400 on the length rule. The documented 64 KiB limit is never reached
  because the field rule rejects it first, so REQ-61 is unreachable through this endpoint.
  Recorded as OBS-04.
- Missing `Content-Type`: 404 rather than 415, because without a JSON content type the request no
  longer matches the endpoint and falls through to the catch-all. The body is at least well formed.
  Recorded as OBS-05.
- Path traversal attempts on the static files: refused.

**Outcome:** [DEF-004](../docs/defects/DEF-004.md), from the first two notes. Automated as IT-68b
and IT-68c. OBS-04 and OBS-05 record behaviour that is acceptable but undocumented.

---

## Observations log

Observations are things worth remembering that are not defects: specification gaps, risks, and
behaviour that is acceptable but undocumented.

| ID | Observation | From |
|----|-------------|------|
| OBS-01 | The lead time message says "at least 15 minutes from now" for a request that asked for exactly 15 minutes, which reads as a contradiction. Cause is DEF-002. | MT-02 |
| OBS-02 | The bookings table does not wrap below roughly 640 px and the page scrolls sideways. No requirement covers small screens. | MT-04 |
| OBS-03 | A completed booking no longer holds capacity. Sensible, but undocumented. | EX-03 |
| OBS-04 | The documented 64 KiB body limit cannot be reached through the booking endpoint, because field validation rejects the oversized payload first. | EX-04 |
| OBS-05 | A request without a `Content-Type` is answered with 404 rather than 415, because routing no longer matches the endpoint. | EX-04 |
