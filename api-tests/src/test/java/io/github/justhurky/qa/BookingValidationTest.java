package io.github.justhurky.qa;

import static org.hamcrest.Matchers.equalTo;

import io.github.justhurky.qa.support.ApiTest;
import io.github.justhurky.qa.support.BookingRequest;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;
import org.junit.jupiter.params.provider.ValueSource;

@DisplayName("Booking field validation and boundaries (REQ-21 to REQ-23)")
class BookingValidationTest extends ApiTest {

    @ParameterizedTest(name = "TC-20 a {0} minute booking is accepted")
    @ValueSource(longs = {5, 30, 120})
    void durationsInsideTheAllowedRangeAreAccepted(long minutes) {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(60 + minutes)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(201)
                .body("state", equalTo("REQUESTED"));
    }

    @ParameterizedTest(name = "TC-21 a {0} minute booking is refused")
    @ValueSource(longs = {4, 121, 240})
    void durationsOutsideTheAllowedRangeAreRefused(long minutes) {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(60 + minutes)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(422)
                .body("error.code", equalTo("DURATION_OUT_OF_RANGE"));
    }

    @Test
    @DisplayName("TC-22 a zero length booking fails on ordering, not on duration")
    void zeroLengthBookingFailsOnOrdering() {
        String moment = inMinutes(60);

        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, moment, moment).asMap())
                .when().post("/api/bookings")
                .then().statusCode(400)
                .body("error.code", equalTo("VALIDATION_FAILED"))
                .body("error.details.field", equalTo("endsAt"));
    }

    @Test
    @DisplayName("TC-23 a booking that ends before it starts is refused")
    void reversedWindowIsRefused() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(90), inMinutes(60)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(400)
                .body("error.details.field", equalTo("endsAt"));
    }

    @ParameterizedTest(name = "TC-24 a purpose of {0} characters is accepted")
    @ValueSource(ints = {3, 50, 200})
    void purposeLengthsInsideTheRangeAreAccepted(int length) {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90))
                        .with("purpose", "x".repeat(length)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(201);
    }

    @ParameterizedTest(name = "TC-25 a purpose of {0} characters is refused")
    @ValueSource(ints = {1, 2, 201, 500})
    void purposeLengthsOutsideTheRangeAreRefused(int length) {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90))
                        .with("purpose", "x".repeat(length)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(400)
                .body("error.code", equalTo("VALIDATION_FAILED"))
                .body("error.details.field", equalTo("purpose"));
    }

    @Test
    @DisplayName("TC-26 a purpose of only whitespace is refused")
    void whitespaceOnlyPurposeIsRefused() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90))
                        .with("purpose", "     ").asMap())
                .when().post("/api/bookings")
                .then().statusCode(400)
                .body("error.details.field", equalTo("purpose"));
    }

    @ParameterizedTest(name = "TC-27 a missing {0} is refused")
    @ValueSource(strings = {"stationCode", "purpose", "startsAt", "endsAt"})
    void mandatoryFieldsAreEnforced(String field) {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90))
                        .without(field).asMap())
                .when().post("/api/bookings")
                .then().statusCode(400)
                .body("error.code", equalTo("VALIDATION_FAILED"))
                .body("error.details.field", equalTo(field));
    }

    @ParameterizedTest(name = "TC-28 {1} is not accepted as a timestamp")
    @CsvSource({
            "startsAt,not-a-date",
            "startsAt,''",
            "endsAt,2026-13-45T99:99:99Z",
            "endsAt,1762000000"
    })
    void malformedTimestampsAreRefused(String field, String value) {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90))
                        .with(field, value).asMap())
                .when().post("/api/bookings")
                .then().statusCode(400)
                .body("error.code", equalTo("VALIDATION_FAILED"));
    }

    @Test
    @DisplayName("TC-29 a non-string purpose is refused rather than coerced")
    void numericPurposeIsRefused() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90))
                        .with("purpose", 12345).asMap())
                .when().post("/api/bookings")
                .then().statusCode(400)
                .body("error.details.field", equalTo("purpose"));
    }

    @ParameterizedTest(name = "TC-30 priority {0} is refused")
    @ValueSource(strings = {"routine", "Routine", "URGENT", "1"})
    void unknownPriorityValuesAreRefused(String priority) {
        as(SUPERVISOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90))
                        .with("priority", priority).asMap())
                .when().post("/api/bookings")
                .then().statusCode(400)
                .body("error.details.field", equalTo("priority"));
    }

    @Test
    @DisplayName("TC-31 priority defaults to ROUTINE when it is omitted")
    void priorityDefaultsToRoutine() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(201)
                .body("priority", equalTo("ROUTINE"));
    }

    @Test
    @DisplayName("TC-32 the purpose is stored trimmed")
    void purposeIsTrimmed() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90))
                        .with("purpose", "   Trimmed purpose   ").asMap())
                .when().post("/api/bookings")
                .then().statusCode(201)
                .body("purpose", equalTo("Trimmed purpose"));
    }
}
