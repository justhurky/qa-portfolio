package io.github.justhurky.qa;

import static org.hamcrest.Matchers.equalTo;

import io.github.justhurky.qa.support.ApiTest;
import io.github.justhurky.qa.support.BookingRequest;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

@DisplayName("Lead time, capacity and overlap (REQ-24 to REQ-31)")
class BookingSchedulingTest extends ApiTest {

    @ParameterizedTest(name = "TC-33 a booking starting in {0} minutes is accepted")
    @ValueSource(longs = {60, 2880})
    void leadTimesInsideTheWindowAreAccepted(long leadMinutes) {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(leadMinutes),
                        inMinutes(leadMinutes + 30)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(201);
    }

    @ParameterizedTest(name = "TC-34 a booking starting in {0} minutes is too soon")
    @ValueSource(longs = {-60, 0, 14})
    void leadTimesBelowTheMinimumAreRefused(long leadMinutes) {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(leadMinutes),
                        inMinutes(leadMinutes + 30)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(422)
                .body("error.code", equalTo("LEAD_TIME_TOO_SHORT"));
    }

    @Test
    @Tag("known-defect")
    @DisplayName("TC-33b a booking at exactly the 15 minute boundary is accepted (DEF-002)")
    void leadTimeAtTheExactBoundaryIsAccepted() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(15), inMinutes(45)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(201);
    }

    @Test
    @DisplayName("TC-35 a booking exactly 30 days ahead is accepted")
    void bookingAtTheFarBoundaryIsAccepted() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inDays(30), inMinutes(30 * 24 * 60 + 30))
                        .asMap())
                .when().post("/api/bookings")
                .then().statusCode(201);
    }

    @Test
    @DisplayName("TC-36 a booking beyond 30 days is refused")
    void bookingBeyondTheFarBoundaryIsRefused() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(30 * 24 * 60 + 2),
                        inMinutes(30 * 24 * 60 + 32)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(422)
                .body("error.code", equalTo("LEAD_TIME_TOO_LONG"));
    }

    @Test
    @DisplayName("TC-37 an emergency booking may start inside the minimum lead time")
    void emergencyOverridesTheMinimumLeadTime() {
        as(SUPERVISOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(1), inMinutes(31))
                        .with("priority", "EMERGENCY").asMap())
                .when().post("/api/bookings")
                .then().statusCode(201)
                .body("priority", equalTo("EMERGENCY"));
    }

    @Test
    @Tag("known-defect")
    @DisplayName("TC-38 an emergency booking must still not start in the past (DEF-001)")
    void emergencyMayNotBeBackdated() {
        as(SUPERVISOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(-4320), inMinutes(-4290))
                        .with("priority", "EMERGENCY").asMap())
                .when().post("/api/bookings")
                .then().statusCode(422)
                .body("error.code", equalTo("LEAD_TIME_TOO_SHORT"));
    }

    @Test
    @DisplayName("TC-39 a second overlapping booking on a single slot station is refused")
    void overlapOnASingleSlotStationIsRefused() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SINGLE_SLOT, inMinutes(60), inMinutes(90)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(201);

        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SINGLE_SLOT, inMinutes(70), inMinutes(100)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(409)
                .body("error.code", equalTo("STATION_AT_CAPACITY"));
    }

    @Test
    @DisplayName("TC-40 a station with capacity two accepts two overlaps and refuses the third")
    void capacityIsRespectedExactly() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_TWO_SLOTS, inMinutes(60), inMinutes(90)).asMap())
                .when().post("/api/bookings").then().statusCode(201);
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_TWO_SLOTS, inMinutes(65), inMinutes(95)).asMap())
                .when().post("/api/bookings").then().statusCode(201);

        as(OPERATOR)
                .body(BookingRequest.valid(STATION_TWO_SLOTS, inMinutes(70), inMinutes(100)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(409)
                .body("error.details.capacity", equalTo(2));
    }

    @Test
    @DisplayName("TC-41 back to back bookings that only touch are both accepted")
    void touchingBookingsDoNotOverlap() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SINGLE_SLOT, inMinutes(60), inMinutes(90)).asMap())
                .when().post("/api/bookings").then().statusCode(201);

        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SINGLE_SLOT, inMinutes(90), inMinutes(120)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(201);
    }

    @Test
    @DisplayName("TC-42 a cancelled booking frees its slot")
    void cancellingFreesTheSlot() {
        String id = as(OPERATOR)
                .body(BookingRequest.valid(STATION_SINGLE_SLOT, inMinutes(60), inMinutes(90)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(201).extract().path("id");

        as(OPERATOR).when().post("/api/bookings/{id}/cancel", id).then().statusCode(200);

        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SINGLE_SLOT, inMinutes(70), inMinutes(100)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(201);
    }

    @Test
    @DisplayName("TC-43 an inactive station refuses bookings")
    void inactiveStationRefusesBookings() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_INACTIVE, inMinutes(60), inMinutes(90)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(409)
                .body("error.code", equalTo("STATION_INACTIVE"));
    }

    @Test
    @DisplayName("TC-44 an unknown station is reported as not found")
    void unknownStationIsReported() {
        as(OPERATOR)
                .body(BookingRequest.valid("ZZZ", inMinutes(60), inMinutes(90)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(404)
                .body("error.code", equalTo("STATION_NOT_FOUND"));
    }

    @Test
    @DisplayName("TC-45 a station code is matched regardless of case")
    void stationCodeIsCaseInsensitive() {
        as(OPERATOR)
                .body(BookingRequest.valid("per", inMinutes(60), inMinutes(90)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(201)
                .body("stationCode", equalTo("PER"));
    }
}
