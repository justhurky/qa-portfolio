package io.github.justhurky.qa;

import static org.hamcrest.Matchers.equalTo;
import static org.hamcrest.Matchers.hasSize;
import static org.hamcrest.Matchers.matchesRegex;

import io.github.justhurky.qa.support.ApiTest;
import io.github.justhurky.qa.support.BookingRequest;
import io.restassured.http.ContentType;
import java.util.List;
import org.assertj.core.api.Assertions;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;
import org.junit.jupiter.params.provider.ValueSource;

@DisplayName("Queries, pagination and robustness (REQ-50 to REQ-64)")
class QueryAndRobustnessTest extends ApiTest {

    private void seedBookings() {
        as(OPERATOR).body(BookingRequest.valid(STATION_SPARE, inMinutes(300), inMinutes(330)).asMap())
                .when().post("/api/bookings").then().statusCode(201);
        as(OPERATOR).body(BookingRequest.valid(STATION_SINGLE_SLOT, inMinutes(100), inMinutes(130)).asMap())
                .when().post("/api/bookings").then().statusCode(201);
        as(SUPERVISOR).body(BookingRequest.valid(STATION_TWO_SLOTS, inMinutes(200), inMinutes(230)).asMap())
                .when().post("/api/bookings").then().statusCode(201);
    }

    @Test
    @DisplayName("TC-70 bookings come back ordered by start time")
    void bookingsAreOrderedByStartTime() {
        seedBookings();

        List<String> starts = as(VIEWER).when().get("/api/bookings")
                .then().statusCode(200).extract().jsonPath().getList("content.startsAt");

        Assertions.assertThat(starts).isSorted();
    }

    @Test
    @DisplayName("TC-71 the station filter narrows the result")
    void stationFilterNarrowsTheResult() {
        seedBookings();

        as(VIEWER).queryParam("stationCode", STATION_SINGLE_SLOT)
                .when().get("/api/bookings")
                .then().statusCode(200)
                .body("content", hasSize(1))
                .body("content[0].stationCode", equalTo(STATION_SINGLE_SLOT));
    }

    @Test
    @DisplayName("TC-72 the state filter narrows the result")
    void stateFilterNarrowsTheResult() {
        seedBookings();
        String id = as(VIEWER).when().get("/api/bookings").then().extract().path("content[0].id");
        as(SUPERVISOR).when().post("/api/bookings/{id}/confirm", id).then().statusCode(200);

        as(VIEWER).queryParam("state", "CONFIRMED")
                .when().get("/api/bookings")
                .then().statusCode(200)
                .body("content", hasSize(1))
                .body("content[0].state", equalTo("CONFIRMED"));
    }

    @Test
    @DisplayName("TC-73 the requester filter narrows the result")
    void requesterFilterNarrowsTheResult() {
        seedBookings();

        as(VIEWER).queryParam("requestedBy", "sam.supervisor")
                .when().get("/api/bookings")
                .then().statusCode(200)
                .body("content", hasSize(1));
    }

    @Test
    @DisplayName("TC-74 pagination reports the totals and slices the content")
    void paginationSlicesTheContent() {
        seedBookings();

        as(VIEWER).queryParam("page", 0).queryParam("size", 2)
                .when().get("/api/bookings")
                .then().statusCode(200)
                .body("content", hasSize(2))
                .body("totalElements", equalTo(3))
                .body("totalPages", equalTo(2))
                .body("page", equalTo(0));

        as(VIEWER).queryParam("page", 1).queryParam("size", 2)
                .when().get("/api/bookings")
                .then().statusCode(200)
                .body("content", hasSize(1));
    }

    @Test
    @DisplayName("TC-75 a page beyond the end is empty rather than an error")
    void pageBeyondTheEndIsEmpty() {
        seedBookings();

        as(VIEWER).queryParam("page", 99)
                .when().get("/api/bookings")
                .then().statusCode(200)
                .body("content", hasSize(0))
                .body("totalElements", equalTo(3));
    }

    @ParameterizedTest(name = "TC-76 page={0} size={1} is rejected")
    @CsvSource({"-1,20", "0,0", "0,101", "abc,20", "0,abc"})
    void invalidPaginationIsRejected(String page, String size) {
        as(VIEWER).queryParam("page", page).queryParam("size", size)
                .when().get("/api/bookings")
                .then().statusCode(400)
                .body("error.code", equalTo("VALIDATION_FAILED"));
    }

    @Test
    @DisplayName("TC-77 timestamps are returned as ISO-8601 in UTC")
    void timestampsAreIsoUtc() {
        seedBookings();

        as(VIEWER).when().get("/api/bookings")
                .then().statusCode(200)
                .body("content[0].startsAt", matchesRegex("\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}\\.\\d{3}Z"));
    }

    @Test
    @DisplayName("TC-78 every error body carries a code and a message")
    void errorBodiesHaveAStableShape() {
        as(OPERATOR)
                .body(BookingRequest.valid("ZZZ", inMinutes(60), inMinutes(90)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(404)
                .body("error.code", matchesRegex("[A-Z_]+"))
                .body("error.message", matchesRegex(".+"));
    }

    @ParameterizedTest(name = "TC-79 a malformed body is rejected cleanly: {0}")
    @ValueSource(strings = {"{broken", "[]", "\"text\"", "42"})
    void malformedBodiesAreRejectedCleanly(String body) {
        String response = as(OPERATOR).contentType(ContentType.JSON).body(body)
                .when().post("/api/bookings")
                .then().statusCode(400)
                .extract().asString();

        Assertions.assertThat(response).doesNotContain("at Object", "node:internal", "Error:");
    }

    @Test
    @org.junit.jupiter.api.Tag("known-defect")
    @DisplayName("TC-80 an oversized body is answered with 413 (DEF-003)")
    void oversizedBodyIsRefused() {
        as(OPERATOR)
                .body(BookingRequest.valid(STATION_SPARE, inMinutes(60), inMinutes(90))
                        .with("purpose", "x".repeat(70_000)).asMap())
                .when().post("/api/bookings")
                .then().statusCode(413)
                .body("error.code", equalTo("PAYLOAD_TOO_LARGE"));
    }

    @ParameterizedTest(name = "TC-81 path traversal attempt {0} is refused")
    @ValueSource(strings = {
            "/../sut/store.js",
            "/../../package.json",
            "/..%2fserver.js"
    })
    void pathTraversalIsRefused(String path) {
        anonymous().when().get(path)
                .then().statusCode(org.hamcrest.Matchers.anyOf(equalTo(403), equalTo(404)));
    }

    @Test
    @DisplayName("TC-82 the web interface is served for an unknown non-API path")
    void staticInterfaceIsServed() {
        anonymous().when().get("/")
                .then().statusCode(200)
                .body(org.hamcrest.Matchers.containsString("Ground Station Booking"));
    }

    @Test
    @DisplayName("TC-83 an unknown API route is reported as not found")
    void unknownApiRouteIsReported() {
        as(OPERATOR).when().get("/api/nope")
                .then().statusCode(404)
                .body("error.code", equalTo("NOT_FOUND"));
    }

    @Test
    @DisplayName("TC-84 responses declare a nosniff content type policy")
    void responsesDeclareNosniff() {
        as(OPERATOR).when().get("/api/me")
                .then().statusCode(200)
                .header("X-Content-Type-Options", equalTo("nosniff"));
    }
}
