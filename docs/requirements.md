# Requirements under test

The system under test is the Ground Station Booking API in [`sut/`](../sut): operators reserve
antenna time slots at ground stations, supervisors approve them. Every requirement below has an
identifier, and every test case in [test-cases.md](test-cases.md) traces back to one.

These requirements are the specification the tests were designed from. Where the implementation
disagrees with them, the difference is raised as a defect rather than silently accepted as the
expected result.

## Authentication and roles

| ID | Requirement |
|----|-------------|
| REQ-01 | Every endpoint except `GET /api/health` requires an `X-Api-Key` header. A missing key is answered with 401 `AUTHENTICATION_REQUIRED`. |
| REQ-02 | An unknown API key is answered with 401 `AUTHENTICATION_FAILED`. |
| REQ-03 | Four roles exist in increasing order of authority: `VIEWER`, `OPERATOR`, `SUPERVISOR`, `ADMIN`. A role inherits every permission of the roles below it. |
| REQ-04 | `GET /api/me` returns the username and role belonging to the presented key. |

## Stations

| ID | Requirement |
|----|-------------|
| REQ-10 | Any authenticated user may list stations and read a single station. |
| REQ-11 | Station codes are matched case-insensitively. |
| REQ-12 | An unknown station code is answered with 404 `STATION_NOT_FOUND`. |
| REQ-13 | Only `ADMIN` may create a station. Lower roles receive 403 `ACCESS_DENIED`. |
| REQ-14 | A station code must be unique. A duplicate is answered with 409 `STATION_ALREADY_EXISTS`. |
| REQ-15 | Station capacity is an integer between 1 and 8 and defaults to 1. |

## Creating a booking

| ID | Requirement |
|----|-------------|
| REQ-20 | `OPERATOR` or higher may create a booking. A `VIEWER` receives 403 `ACCESS_DENIED`. |
| REQ-21 | `purpose` is mandatory and must be between 3 and 200 characters after trimming. |
| REQ-22 | `startsAt` and `endsAt` are mandatory ISO-8601 timestamps. `endsAt` must be strictly after `startsAt`. |
| REQ-23 | Booking duration must be between 5 and 120 minutes, inclusive at both ends. |
| REQ-24 | A booking must start at least 15 minutes after the current time. |
| REQ-25 | A booking may not start more than 30 days after the current time. |
| REQ-26 | `priority` is one of `ROUTINE`, `PRIORITY`, `EMERGENCY` and defaults to `ROUTINE`. The value is case-sensitive. |
| REQ-27 | `EMERGENCY` priority requires `SUPERVISOR` or higher. A lower role receives 403 `PRIORITY_NOT_PERMITTED`. |
| REQ-28 | An `EMERGENCY` booking is exempt from the 15 minute minimum lead time, so that it may start immediately. It must still not start in the past. |
| REQ-29 | A station may hold at most `capacity` overlapping bookings in state `REQUESTED` or `CONFIRMED`. Exceeding it is answered with 409 `STATION_AT_CAPACITY`. |
| REQ-30 | Two bookings that merely touch, where one ends exactly when the next begins, do not overlap. |
| REQ-31 | A booking on an inactive station is answered with 409 `STATION_INACTIVE`. |
| REQ-32 | Fields the client is not allowed to set, such as `state` and `requestedBy`, are derived by the server and never taken from the request body. |

## Booking lifecycle

| ID | Requirement |
|----|-------------|
| REQ-40 | A new booking starts in state `REQUESTED`. |
| REQ-41 | The permitted transitions are `REQUESTED → CONFIRMED`, `REQUESTED → CANCELLED`, `CONFIRMED → COMPLETED` and `CONFIRMED → CANCELLED`. `COMPLETED` and `CANCELLED` are terminal. |
| REQ-42 | Any other transition is answered with 409 `ILLEGAL_STATE_TRANSITION`, and the response names the current state and the permitted targets. |
| REQ-43 | Confirming and completing require `SUPERVISOR` or higher. |
| REQ-44 | A booking may be cancelled by the operator who requested it, or by `SUPERVISOR` or higher. Another operator receives 403 `ACCESS_DENIED`. |
| REQ-45 | A transition records who decided it and when. |

## Queries and representation

| ID | Requirement |
|----|-------------|
| REQ-50 | `GET /api/bookings` supports filtering by `stationCode`, `state` and `requestedBy`. |
| REQ-51 | Listing endpoints are paginated with `page` starting at 0 and `size` between 1 and 100, defaulting to 20. Out of range values are answered with 400 `VALIDATION_FAILED`. |
| REQ-52 | Bookings are returned ordered by start time, ascending. |
| REQ-53 | Every error response carries a machine readable `error.code` and a human readable `error.message`. |
| REQ-54 | Timestamps are returned as ISO-8601 in UTC. |

## Robustness and safety

| ID | Requirement |
|----|-------------|
| REQ-60 | A body that is not valid JSON is answered with 400 `MALFORMED_JSON`, never with a stack trace. |
| REQ-61 | A request body above 64 KiB is rejected with 413 `PAYLOAD_TOO_LARGE`. |
| REQ-62 | Static file serving must not escape the public directory. |
| REQ-63 | Text supplied by a user is rendered as text in the web interface and never executed as markup. |
| REQ-64 | An unexpected server error is answered with a generic 500 body that leaks no internal detail. |
