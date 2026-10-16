package io.github.justhurky.qa;

import static org.hamcrest.Matchers.equalTo;

import io.github.justhurky.qa.support.ApiTest;
import io.github.justhurky.qa.support.BookingRequest;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

@DisplayName("Booking authorisation decision table (REQ-20, REQ-27, REQ-28)")
class BookingAuthorisationTest extends ApiTest {

    @ParameterizedTest(name = "{0}: {1} requesting {2} at {3} minutes lead expects {4}")
    @CsvSource({
            "R1,viewer-key,ROUTINE,60,403,ACCESS_DENIED",
            "R2,operator-key,ROUTINE,60,201,",
            "R3,operator-key,ROUTINE,5,422,LEAD_TIME_TOO_SHORT",
            "R4,operator-key,PRIORITY,60,201,",
            "R5,operator-key,PRIORITY,5,422,LEAD_TIME_TOO_SHORT",
            "R6,operator-key,EMERGENCY,60,403,PRIORITY_NOT_PERMITTED",
            "R7,supervisor-key,EMERGENCY,60,201,",
            "R8,supervisor-key,EMERGENCY,5,201,",
            "R9,admin-key,EMERGENCY,5,201,"
    })
    void decisionTableIsHonoured(String rule, String apiKey, String priority, long leadMinutes,
                                 int expectedStatus, String expectedCode) {
        var response = as(apiKey)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(leadMinutes),
                                inMinutes(leadMinutes + 30))
                        .with("priority", priority).asMap())
                .when().post("/api/bookings")
                .then().statusCode(expectedStatus);

        if (expectedCode != null && !expectedCode.isBlank()) {
            response.body("error.code", equalTo(expectedCode));
        }
    }

    @Test
    @DisplayName("TC-50 the role check wins over the timing check")
    void roleFailureIsReportedBeforeTimingFailure() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(1), inMinutes(31))
                        .with("priority", "EMERGENCY").asMap())
                .when().post("/api/bookings")
                .then().statusCode(403)
                .body("error.code", equalTo("PRIORITY_NOT_PERMITTED"));
    }

    @Test
    @DisplayName("TC-51 a viewer may read bookings but not create them")
    void viewerIsReadOnly() {
        as(VIEWER).when().get("/api/bookings").then().statusCode(200);

        as(VIEWER)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(403)
                .body("error.code", equalTo("ACCESS_DENIED"));
    }

    @Test
    @DisplayName("TC-52 only an administrator may create a station")
    void onlyAdminCreatesStations() {
        for (String key : new String[]{VIEWER, OPERATOR, SUPERVISOR}) {
            as(key)
                    .body(java.util.Map.of("code", "NEW", "name", "New dish"))
                    .when().post("/api/stations")
                    .then().statusCode(403)
                    .body("error.code", equalTo("ACCESS_DENIED"));
        }

        as(ADMIN)
                .body(java.util.Map.of("code", "NEW", "name", "New dish"))
                .when().post("/api/stations")
                .then().statusCode(201)
                .body("code", equalTo("NEW"))
                .body("capacity", equalTo(1));
    }

    @Test
    @DisplayName("TC-53 the server owns the state and the requester of a booking")
    void serverControlledFieldsCannotBeSetByTheClient() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90))
                        .with("state", "CONFIRMED")
                        .with("requestedBy", "somebody.else")
                        .with("id", "11111111-1111-1111-1111-111111111111").asMap())
                .when().post("/api/bookings")
                .then().statusCode(201)
                .body("state", equalTo("REQUESTED"))
                .body("requestedBy", equalTo("olga.operator"))
                .body("id", org.hamcrest.Matchers.not(equalTo("11111111-1111-1111-1111-111111111111")));
    }
}
