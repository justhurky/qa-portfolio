package io.github.justhurky.qa.support;

import static io.restassured.RestAssured.given;

import io.restassured.RestAssured;
import io.restassured.builder.RequestSpecBuilder;
import io.restassured.filter.log.LogDetail;
import io.restassured.http.ContentType;
import io.restassured.specification.RequestSpecification;
import java.time.Duration;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;

public abstract class ApiTest {

    protected static final String VIEWER = "viewer-key";
    protected static final String OPERATOR = "operator-key";
    protected static final String OTHER_OPERATOR = "operator2-key";
    protected static final String SUPERVISOR = "supervisor-key";
    protected static final String ADMIN = "admin-key";

    protected static final String STATION_SINGLE_SLOT = "KIR";
    protected static final String STATION_TWO_SLOTS = "SVB";
    protected static final String STATION_SPARE = "PER";
    protected static final String STATION_INACTIVE = "OLD";

    private static RequestSpecification baseSpec;

    @BeforeAll
    static void configureRestAssured() {
        RestAssured.baseURI = baseUrl();
        baseSpec = new RequestSpecBuilder()
                .setContentType(ContentType.JSON)
                .setAccept(ContentType.JSON)
                .log(LogDetail.URI)
                .build();
        RestAssured.enableLoggingOfRequestAndResponseIfValidationFails();
    }

    @BeforeEach
    void resetSystemUnderTest() {
        given().when().post("/api/test/reset").then().statusCode(200);
    }

    protected static String baseUrl() {
        String configured = System.getProperty("sut.baseUrl");
        if (configured == null || configured.isBlank() || configured.equals("${sut.baseUrl}")) {
            configured = System.getenv("SUT_BASE_URL");
        }
        return configured == null || configured.isBlank() ? "http://localhost:3000" : configured;
    }

    protected RequestSpecification as(String apiKey) {
        return given().spec(baseSpec).header("X-Api-Key", apiKey);
    }

    protected RequestSpecification anonymous() {
        return given().spec(baseSpec);
    }

    protected static String inMinutes(long minutes) {
        return Instant.now().plus(Duration.ofMinutes(minutes)).truncatedTo(ChronoUnit.SECONDS)
                .toString();
    }

    protected static String inDays(long days) {
        return inMinutes(days * 24 * 60);
    }
}
