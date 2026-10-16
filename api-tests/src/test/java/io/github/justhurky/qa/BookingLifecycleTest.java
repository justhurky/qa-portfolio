package io.github.justhurky.qa;

import static org.hamcrest.Matchers.equalTo;
import static org.hamcrest.Matchers.notNullValue;

import io.github.justhurky.qa.support.ApiTest;
import io.github.justhurky.qa.support.BookingRequest;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

@DisplayName("Booking lifecycle state machine (REQ-40 to REQ-45)")
class BookingLifecycleTest extends ApiTest {

    private String requestBooking() {
        return as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(201)
                .extract().path("id");
    }

    private String bookingInState(String state) {
        String id = requestBooking();
        if (state.equals("REQUESTED")) {
            return id;
        }
        if (state.equals("CANCELLED")) {
            as(OPERATOR).when().post("/api/bookings/{id}/cancel", id).then().statusCode(200);
            return id;
        }
        as(SUPERVISOR).when().post("/api/bookings/{id}/confirm", id).then().statusCode(200);
        if (state.equals("COMPLETED")) {
            as(SUPERVISOR).when().post("/api/bookings/{id}/complete", id).then().statusCode(200);
        }
        return id;
    }

    @Test
    @DisplayName("TC-60 a new booking starts in REQUESTED")
    void newBookingStartsRequested() {
        as(OPERATOR).when().get("/api/bookings/{id}", requestBooking())
                .then().statusCode(200)
                .body("state", equalTo("REQUESTED"))
                .body("decidedBy", org.hamcrest.Matchers.nullValue());
    }

    @ParameterizedTest(name = "TC-61 {0} may be {1}ed")
    @CsvSource({
            "REQUESTED,confirm,CONFIRMED",
            "REQUESTED,cancel,CANCELLED",
            "CONFIRMED,complete,COMPLETED",
            "CONFIRMED,cancel,CANCELLED"
    })
    void permittedTransitionsSucceed(String from, String action, String expected) {
        String id = bookingInState(from);

        as(SUPERVISOR)
                .when().post("/api/bookings/{id}/{action}", id, action)
                .then().statusCode(200)
                .body("state", equalTo(expected))
                .body("decidedBy", equalTo("sam.supervisor"))
                .body("decidedAt", notNullValue());
    }

    @ParameterizedTest(name = "TC-62 {0} may not be {1}ed")
    @CsvSource({
            "REQUESTED,complete",
            "CONFIRMED,confirm",
            "COMPLETED,confirm",
            "COMPLETED,cancel",
            "COMPLETED,complete",
            "CANCELLED,confirm",
            "CANCELLED,cancel",
            "CANCELLED,complete"
    })
    void forbiddenTransitionsAreRefused(String from, String action) {
        String id = bookingInState(from);

        as(SUPERVISOR)
                .when().post("/api/bookings/{id}/{action}", id, action)
                .then().statusCode(409)
                .body("error.code", equalTo("ILLEGAL_STATE_TRANSITION"))
                .body("error.details.from", equalTo(from));
    }

    @Test
    @DisplayName("TC-63 the full happy path walks REQUESTED to COMPLETED")
    void happyPathWalksTheWholeLifecycle() {
        String id = requestBooking();

        as(SUPERVISOR).when().post("/api/bookings/{id}/confirm", id)
                .then().statusCode(200).body("state", equalTo("CONFIRMED"));
        as(SUPERVISOR).when().post("/api/bookings/{id}/complete", id)
                .then().statusCode(200).body("state", equalTo("COMPLETED"));
        as(VIEWER).when().get("/api/bookings/{id}", id)
                .then().statusCode(200).body("state", equalTo("COMPLETED"));
    }

    @Test
    @DisplayName("TC-64 an operator may cancel their own booking")
    void ownerMayCancel() {
        as(OPERATOR).when().post("/api/bookings/{id}/cancel", requestBooking())
                .then().statusCode(200)
                .body("state", equalTo("CANCELLED"));
    }

    @Test
    @DisplayName("TC-65 another operator may not cancel someone else's booking")
    void otherOperatorMayNotCancel() {
        as(OTHER_OPERATOR).when().post("/api/bookings/{id}/cancel", requestBooking())
                .then().statusCode(403)
                .body("error.code", equalTo("ACCESS_DENIED"));
    }

    @Test
    @DisplayName("TC-66 a supervisor may cancel a booking they did not request")
    void supervisorMayCancelAnyBooking() {
        as(SUPERVISOR).when().post("/api/bookings/{id}/cancel", requestBooking())
                .then().statusCode(200)
                .body("state", equalTo("CANCELLED"));
    }

    @ParameterizedTest(name = "TC-67 an operator may not {0} a booking")
    @CsvSource({"confirm", "complete"})
    void operatorMayNotDecide(String action) {
        String id = action.equals("complete") ? bookingInState("CONFIRMED") : requestBooking();

        as(OPERATOR)
                .when().post("/api/bookings/{id}/{action}", id, action)
                .then().statusCode(403)
                .body("error.code", equalTo("ACCESS_DENIED"));
    }

    @Test
    @DisplayName("TC-68 an unknown booking id is reported as not found")
    void unknownBookingIsReported() {
        as(SUPERVISOR)
                .when().post("/api/bookings/{id}/confirm", "00000000-0000-0000-0000-000000000000")
                .then().statusCode(404)
                .body("error.code", equalTo("BOOKING_NOT_FOUND"));
    }

    @Test
    @DisplayName("TC-69 an unknown action is reported as not found")
    void unknownActionIsReported() {
        as(SUPERVISOR)
                .when().post("/api/bookings/{id}/approve", requestBooking())
                .then().statusCode(404)
                .body("error.code", equalTo("NOT_FOUND"));
    }
}
