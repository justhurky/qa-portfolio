package io.github.justhurky.qa;

import static org.hamcrest.Matchers.equalTo;
import static org.hamcrest.Matchers.notNullValue;

import io.github.justhurky.qa.support.ApiTest;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;
import org.junit.jupiter.params.provider.ValueSource;

@DisplayName("Authentication and identity (REQ-01 to REQ-04)")
class AuthenticationTest extends ApiTest {

    @Test
    @DisplayName("TC-01 health is reachable without a key")
    void healthNeedsNoKey() {
        anonymous()
                .when().get("/api/health")
                .then().statusCode(200)
                .body("status", equalTo("UP"))
                .body("time", notNullValue());
    }

    @ParameterizedTest(name = "TC-02 {0} rejects an anonymous caller")
    @ValueSource(strings = {"/api/me", "/api/stations", "/api/bookings"})
    void protectedEndpointsRejectAnonymousCallers(String path) {
        anonymous()
                .when().get(path)
                .then().statusCode(401)
                .body("error.code", equalTo("AUTHENTICATION_REQUIRED"));
    }

    @Test
    @DisplayName("TC-03 an unknown key is rejected as a failed authentication")
    void unknownKeyIsRejected() {
        as("there-is-no-such-key")
                .when().get("/api/me")
                .then().statusCode(401)
                .body("error.code", equalTo("AUTHENTICATION_FAILED"));
    }

    @Test
    @DisplayName("TC-04 an empty key is treated as a missing key")
    void emptyKeyIsTreatedAsMissing() {
        as("")
                .when().get("/api/me")
                .then().statusCode(401)
                .body("error.code", equalTo("AUTHENTICATION_REQUIRED"));
    }

    @ParameterizedTest(name = "TC-05 {0} resolves to {1} with role {2}")
    @CsvSource({
            "viewer-key,vera.viewer,VIEWER",
            "operator-key,olga.operator,OPERATOR",
            "supervisor-key,sam.supervisor,SUPERVISOR",
            "admin-key,ada.admin,ADMIN"
    })
    void everySeededKeyResolvesToItsIdentity(String key, String username, String role) {
        as(key)
                .when().get("/api/me")
                .then().statusCode(200)
                .body("username", equalTo(username))
                .body("role", equalTo(role));
    }

    @Test
    @DisplayName("TC-06 the identity response does not echo the API key back")
    void identityResponseDoesNotLeakTheKey() {
        String body = as(OPERATOR).when().get("/api/me").then().statusCode(200)
                .extract().asString();

        org.assertj.core.api.Assertions.assertThat(body).doesNotContain(OPERATOR);
    }
}
