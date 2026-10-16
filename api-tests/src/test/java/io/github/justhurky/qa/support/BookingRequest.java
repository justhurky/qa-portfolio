package io.github.justhurky.qa.support;

import java.util.LinkedHashMap;
import java.util.Map;

public final class BookingRequest {

    private final Map<String, Object> fields = new LinkedHashMap<>();

    private BookingRequest() {
    }

    public static BookingRequest valid(String stationCode, String startsAt, String endsAt) {
        BookingRequest request = new BookingRequest();
        request.fields.put("stationCode", stationCode);
        request.fields.put("purpose", "Scheduled housekeeping pass");
        request.fields.put("startsAt", startsAt);
        request.fields.put("endsAt", endsAt);
        return request;
    }

    public static BookingRequest empty() {
        return new BookingRequest();
    }

    public BookingRequest with(String field, Object value) {
        fields.put(field, value);
        return this;
    }

    public BookingRequest without(String field) {
        fields.remove(field);
        return this;
    }

    public Map<String, Object> asMap() {
        return new LinkedHashMap<>(fields);
    }
}
