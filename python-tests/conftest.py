import os
from datetime import datetime, timedelta, timezone

import pytest
import requests

VIEWER = "viewer-key"
OPERATOR = "operator-key"
OTHER_OPERATOR = "operator2-key"
SUPERVISOR = "supervisor-key"
ADMIN = "admin-key"


def pytest_addoption(parser):
    parser.addoption("--base-url", action="store", default=None,
                     help="Base URL of the system under test")


@pytest.fixture(scope="session")
def base_url(pytestconfig):
    return (pytestconfig.getoption("--base-url")
            or os.environ.get("SUT_BASE_URL")
            or "http://localhost:3000")


@pytest.fixture
def api(base_url):
    class Api:
        def __init__(self):
            self.session = requests.Session()

        def request(self, method, path, key=None, **kwargs):
            headers = kwargs.pop("headers", {})
            if key is not None:
                headers["X-Api-Key"] = key
            return self.session.request(method, f"{base_url}{path}", headers=headers,
                                        timeout=10, **kwargs)

        def get(self, path, key=None, **kwargs):
            return self.request("GET", path, key, **kwargs)

        def post(self, path, key=None, **kwargs):
            return self.request("POST", path, key, **kwargs)

    client = Api()
    client.post("/api/test/reset").raise_for_status()
    return client


def in_minutes(minutes):
    moment = datetime.now(timezone.utc) + timedelta(minutes=minutes)
    return moment.replace(microsecond=0).isoformat().replace("+00:00", "Z")


def booking(station="PER", lead=60, duration=30, **overrides):
    payload = {
        "stationCode": station,
        "purpose": "Scheduled housekeeping pass",
        "startsAt": in_minutes(lead),
        "endsAt": in_minutes(lead + duration),
    }
    payload.update(overrides)
    return payload
