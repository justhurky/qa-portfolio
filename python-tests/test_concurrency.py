from concurrent.futures import ThreadPoolExecutor

import pytest

from conftest import OPERATOR, OTHER_OPERATOR, SUPERVISOR, booking


def post_booking(api, payload, key=OPERATOR):
    return api.post("/api/bookings", key, json=payload)


def test_tc96_only_one_of_two_simultaneous_bookings_wins_a_single_slot(api):
    payload = booking(station="KIR", lead=120, duration=30)

    with ThreadPoolExecutor(max_workers=2) as pool:
        responses = list(pool.map(lambda _: post_booking(api, payload), range(2)))

    statuses = sorted(response.status_code for response in responses)
    assert statuses == [201, 409], f"expected one winner and one rejection, got {statuses}"
    assert any(r.json().get("error", {}).get("code") == "STATION_AT_CAPACITY"
               for r in responses if r.status_code == 409)


def test_tc97_a_station_with_two_slots_admits_exactly_two_of_four_racers(api):
    payload = booking(station="SVB", lead=180, duration=30)

    with ThreadPoolExecutor(max_workers=4) as pool:
        responses = list(pool.map(lambda _: post_booking(api, payload), range(4)))

    accepted = [r for r in responses if r.status_code == 201]
    rejected = [r for r in responses if r.status_code == 409]
    assert len(accepted) == 2, f"expected exactly two winners, got {len(accepted)}"
    assert len(rejected) == 2


def test_tc98_a_booking_cannot_be_confirmed_twice_in_parallel(api):
    created = post_booking(api, booking(station="PER", lead=240)).json()
    path = f"/api/bookings/{created['id']}/confirm"

    with ThreadPoolExecutor(max_workers=2) as pool:
        responses = list(pool.map(lambda _: api.post(path, SUPERVISOR), range(2)))

    statuses = sorted(response.status_code for response in responses)
    assert statuses == [200, 409], f"expected one confirmation and one rejection, got {statuses}"


@pytest.mark.parametrize("key,expected", [(OPERATOR, 200), (OTHER_OPERATOR, 403)])
def test_tc99_cancellation_ownership_holds_under_repeated_attempts(api, key, expected):
    created = post_booking(api, booking(station="PER", lead=300)).json()

    for _ in range(3):
        response = api.post(f"/api/bookings/{created['id']}/cancel", key)
        if expected == 200:
            assert response.status_code in (200, 409)
        else:
            assert response.status_code == 403
