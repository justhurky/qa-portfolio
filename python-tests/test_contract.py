import jsonschema
import pytest

from conftest import ADMIN, OPERATOR, SUPERVISOR, VIEWER, booking

BOOKING_SCHEMA = {
    "type": "object",
    "required": ["id", "stationCode", "purpose", "priority", "state", "startsAt", "endsAt",
                 "requestedBy", "requestedAt"],
    "additionalProperties": False,
    "properties": {
        "id": {"type": "string",
               "pattern": "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"},
        "stationCode": {"type": "string", "pattern": "^[A-Z0-9-]{2,16}$"},
        "purpose": {"type": "string", "minLength": 3, "maxLength": 200},
        "priority": {"enum": ["ROUTINE", "PRIORITY", "EMERGENCY"]},
        "state": {"enum": ["REQUESTED", "CONFIRMED", "COMPLETED", "CANCELLED"]},
        "startsAt": {"type": "string", "format": "date-time"},
        "endsAt": {"type": "string", "format": "date-time"},
        "requestedBy": {"type": "string"},
        "requestedAt": {"type": "string", "format": "date-time"},
        "decidedBy": {"type": ["string", "null"]},
        "decidedAt": {"type": ["string", "null"]},
    },
}

PAGE_SCHEMA = {
    "type": "object",
    "required": ["content", "page", "size", "totalElements", "totalPages"],
    "additionalProperties": False,
    "properties": {
        "content": {"type": "array"},
        "page": {"type": "integer", "minimum": 0},
        "size": {"type": "integer", "minimum": 1, "maximum": 100},
        "totalElements": {"type": "integer", "minimum": 0},
        "totalPages": {"type": "integer", "minimum": 0},
    },
}

ERROR_SCHEMA = {
    "type": "object",
    "required": ["error"],
    "additionalProperties": False,
    "properties": {
        "error": {
            "type": "object",
            "required": ["code", "message"],
            "additionalProperties": False,
            "properties": {
                "code": {"type": "string", "pattern": "^[A-Z_]+$"},
                "message": {"type": "string", "minLength": 1},
                "details": {"type": "object"},
            },
        }
    },
}

STATION_SCHEMA = {
    "type": "object",
    "required": ["code", "name", "country", "capacity", "active"],
    "additionalProperties": False,
    "properties": {
        "code": {"type": "string"},
        "name": {"type": "string"},
        "country": {"type": "string"},
        "capacity": {"type": "integer", "minimum": 1, "maximum": 8},
        "active": {"type": "boolean"},
    },
}

validator = jsonschema.Draft202012Validator


def test_tc90_a_created_booking_matches_the_booking_schema(api):
    response = api.post("/api/bookings", OPERATOR, json=booking())

    assert response.status_code == 201
    validator(BOOKING_SCHEMA, format_checker=validator.FORMAT_CHECKER).validate(response.json())


def test_tc91_a_booking_page_matches_the_page_schema(api):
    api.post("/api/bookings", OPERATOR, json=booking())

    response = api.get("/api/bookings", VIEWER)

    assert response.status_code == 200
    body = response.json()
    validator(PAGE_SCHEMA).validate(body)
    for item in body["content"]:
        validator(BOOKING_SCHEMA, format_checker=validator.FORMAT_CHECKER).validate(item)


def test_tc92_a_station_page_matches_the_station_schema(api):
    response = api.get("/api/stations", VIEWER)

    assert response.status_code == 200
    body = response.json()
    validator(PAGE_SCHEMA).validate(body)
    for item in body["content"]:
        validator(STATION_SCHEMA).validate(item)


@pytest.mark.parametrize("path,key,payload,expected", [
    ("/api/bookings", OPERATOR, {"stationCode": "ZZZ", "purpose": "Nowhere",
                                 "startsAt": "2030-01-01T10:00:00Z",
                                 "endsAt": "2030-01-01T10:30:00Z"}, 404),
    ("/api/bookings", VIEWER, None, 403),
    ("/api/stations", OPERATOR, {"code": "AAA", "name": "Denied"}, 403),
])
def test_tc93_every_error_body_matches_the_error_schema(api, path, key, payload, expected):
    response = api.post(path, key, json=payload if payload is not None else booking())

    assert response.status_code == expected
    validator(ERROR_SCHEMA).validate(response.json())


def test_tc94_a_decided_booking_reports_who_decided_it(api):
    created = api.post("/api/bookings", OPERATOR, json=booking()).json()

    decided = api.post(f"/api/bookings/{created['id']}/confirm", SUPERVISOR).json()

    validator(BOOKING_SCHEMA, format_checker=validator.FORMAT_CHECKER).validate(decided)
    assert decided["decidedBy"] == "sam.supervisor"
    assert decided["decidedAt"] is not None


def test_tc95_a_created_station_matches_the_station_schema(api):
    response = api.post("/api/stations", ADMIN,
                        json={"code": "TST", "name": "Test dish", "capacity": 3})

    assert response.status_code == 201
    validator(STATION_SCHEMA).validate(response.json())
    assert response.json()["capacity"] == 3
