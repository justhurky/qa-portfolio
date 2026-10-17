using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace GroundStation.ApiTests;

public class AuthenticationApiTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    [Fact(DisplayName = "IT-01 health is reachable without a key")]
    public async Task HealthNeedsNoKey()
    {
        using HttpClient client = As(null);

        HttpResponseMessage response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("UP", (await BodyOf(response)).GetProperty("status").GetString());
    }

    [Theory(DisplayName = "IT-02 a protected endpoint rejects an anonymous caller")]
    [InlineData("/api/me")]
    [InlineData("/api/stations")]
    [InlineData("/api/bookings")]
    public async Task ProtectedEndpointsRejectAnonymousCallers(string path)
    {
        using HttpClient client = As(null);

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("AUTHENTICATION_REQUIRED", await ErrorCodeOf(response));
    }

    [Fact(DisplayName = "IT-03 an unknown key is rejected")]
    public async Task UnknownKeyIsRejected()
    {
        using HttpClient client = As("there-is-no-such-key");

        HttpResponseMessage response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("AUTHENTICATION_FAILED", await ErrorCodeOf(response));
    }

    [Theory(DisplayName = "IT-04 each seeded key resolves to its identity")]
    [InlineData(Viewer, "vera.viewer", "VIEWER")]
    [InlineData(Operator, "olga.operator", "OPERATOR")]
    [InlineData(Supervisor, "sam.supervisor", "SUPERVISOR")]
    [InlineData(Admin, "ada.admin", "ADMIN")]
    public async Task SeededKeysResolveToIdentities(string key, string username, string role)
    {
        using HttpClient client = As(key);

        JsonElement body = await BodyOf(await client.GetAsync("/api/me"));

        Assert.Equal(username, body.GetProperty("username").GetString());
        Assert.Equal(role, body.GetProperty("role").GetString());
    }

    [Fact(DisplayName = "IT-05 the identity response does not echo the key back")]
    public async Task IdentityDoesNotLeakTheKey()
    {
        using HttpClient client = As(Operator);

        string body = await (await client.GetAsync("/api/me")).Content.ReadAsStringAsync();

        Assert.DoesNotContain(Operator, body);
    }
}

public class BookingCreationApiTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    [Theory(DisplayName = "IT-10 a duration inside the range is accepted")]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(120)]
    public async Task DurationsInsideTheRangeAreAccepted(double minutes)
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(durationMinutes: minutes), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory(DisplayName = "IT-11 a duration outside the range is refused")]
    [InlineData(4)]
    [InlineData(121)]
    public async Task DurationsOutsideTheRangeAreRefused(double minutes)
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(durationMinutes: minutes), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("DURATION_OUT_OF_RANGE", await ErrorCodeOf(response));
    }

    [Theory(DisplayName = "IT-12 a purpose outside the length range is refused")]
    [InlineData(2)]
    [InlineData(201)]
    public async Task PurposeOutsideTheRangeIsRefused(int length)
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(purpose: new string('x', length)), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await BodyOf(response);
        Assert.Equal("purpose", body.GetProperty("error").GetProperty("details")
            .GetProperty("field").GetString());
    }

    [Fact(DisplayName = "IT-13 the purpose is stored trimmed")]
    public async Task PurposeIsStoredTrimmed()
    {
        JsonElement booking = await CreateBooking(Operator,
            BookingPayload(purpose: "   Trimmed purpose   "));

        Assert.Equal("Trimmed purpose", booking.GetProperty("purpose").GetString());
    }

    [Fact(DisplayName = "IT-14 a reversed window is refused")]
    public async Task ReversedWindowIsRefused()
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(durationMinutes: -30), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory(DisplayName = "IT-15 a malformed timestamp is refused")]
    [InlineData("not-a-date")]
    [InlineData("2026-13-45T99:99:99Z")]
    public async Task MalformedTimestampsAreRefused(string value)
    {
        using HttpClient client = As(Operator);
        string payload = $$"""
            {"stationCode":"PER","purpose":"Bad timestamp","startsAt":"{{value}}",
             "endsAt":"{{InMinutes(90)}}"}
            """;

        HttpResponseMessage response = await client.PostAsync("/api/bookings",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory(DisplayName = "IT-16 an unknown priority is refused")]
    [InlineData("routine")]
    [InlineData("URGENT")]
    public async Task UnknownPriorityIsRefused(string priority)
    {
        using HttpClient client = As(Supervisor);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(priority: priority), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "IT-17 priority defaults to ROUTINE")]
    public async Task PriorityDefaultsToRoutine()
    {
        JsonElement booking = await CreateBooking(Operator, BookingPayload());

        Assert.Equal("ROUTINE", booking.GetProperty("priority").GetString());
    }

    [Fact(DisplayName = "IT-18 a station code is matched regardless of case")]
    public async Task StationCodeIsCaseInsensitive()
    {
        JsonElement booking = await CreateBooking(Operator, BookingPayload(station: "per"));

        Assert.Equal("PER", booking.GetProperty("stationCode").GetString());
    }

    [Fact(DisplayName = "IT-19 an unknown station is reported as not found")]
    public async Task UnknownStationIsReported()
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(station: "ZZZ"), Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("STATION_NOT_FOUND", await ErrorCodeOf(response));
    }

    [Fact(DisplayName = "IT-20 an inactive station refuses bookings")]
    public async Task InactiveStationRefusesBookings()
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(station: InactiveStation), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("STATION_INACTIVE", await ErrorCodeOf(response));
    }

    [Fact(DisplayName = "IT-21 the server owns the state and the requester")]
    public async Task ServerOwnedFieldsCannotBeSetByTheClient()
    {
        using HttpClient client = As(Operator);
        string payload = $$"""
            {"stationCode":"PER","purpose":"Mass assignment probe",
             "startsAt":"{{InMinutes(60)}}","endsAt":"{{InMinutes(90)}}",
             "state":"CONFIRMED","requestedBy":"somebody.else"}
            """;

        HttpResponseMessage response = await client.PostAsync("/api/bookings",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        JsonElement body = await BodyOf(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("REQUESTED", body.GetProperty("state").GetString());
        Assert.Equal("olga.operator", body.GetProperty("requestedBy").GetString());
    }
}

public class SchedulingApiTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    [Theory(DisplayName = "IT-30 a lead time inside the window is accepted")]
    [InlineData(60)]
    [InlineData(2880)]
    public async Task LeadTimesInsideTheWindowAreAccepted(double leadMinutes)
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(leadMinutes: leadMinutes), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory(DisplayName = "IT-31 a lead time below the minimum is refused")]
    [InlineData(14)]
    [InlineData(0)]
    [InlineData(-60)]
    public async Task LeadTimesBelowTheMinimumAreRefused(double leadMinutes)
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(leadMinutes: leadMinutes), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("LEAD_TIME_TOO_SHORT", await ErrorCodeOf(response));
    }

    [Fact(DisplayName = "IT-32 the exact 15 minute boundary is accepted (DEF-002)")]
    [Trait("Category", "KnownDefect")]
    public async Task ExactLeadBoundaryIsAccepted()
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(leadMinutes: 15), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "IT-33 a booking beyond the far horizon is refused")]
    public async Task BeyondTheHorizonIsRefused()
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(leadMinutes: 30 * 24 * 60 + 5), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("LEAD_TIME_TOO_LONG", await ErrorCodeOf(response));
    }

    [Fact(DisplayName = "IT-34 an emergency may start inside the lead window")]
    public async Task EmergencyOverridesTheLeadWindow()
    {
        using HttpClient client = As(Supervisor);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(leadMinutes: 1, priority: "EMERGENCY"), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "IT-35 an emergency may not be backdated (DEF-001)")]
    [Trait("Category", "KnownDefect")]
    public async Task EmergencyMayNotBeBackdated()
    {
        using HttpClient client = As(Supervisor);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(leadMinutes: -4320, priority: "EMERGENCY"), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact(DisplayName = "IT-36 a single slot station refuses a second overlap")]
    public async Task SingleSlotStationRefusesAnOverlap()
    {
        await CreateBooking(Operator, BookingPayload(station: SingleSlotStation, leadMinutes: 60));
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(station: SingleSlotStation, leadMinutes: 70), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("STATION_AT_CAPACITY", await ErrorCodeOf(response));
    }

    [Fact(DisplayName = "IT-37 a two slot station admits two overlaps and refuses the third")]
    public async Task CapacityIsRespectedExactly()
    {
        await CreateBooking(Operator, BookingPayload(station: TwoSlotStation, leadMinutes: 60));
        await CreateBooking(Operator, BookingPayload(station: TwoSlotStation, leadMinutes: 65));
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(station: TwoSlotStation, leadMinutes: 70), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName = "IT-38 back to back bookings that only touch are both accepted")]
    public async Task TouchingBookingsAreAccepted()
    {
        await CreateBooking(Operator, BookingPayload(station: SingleSlotStation, leadMinutes: 60));

        await CreateBooking(Operator, BookingPayload(station: SingleSlotStation, leadMinutes: 90));
    }

    [Fact(DisplayName = "IT-39 cancelling frees the slot")]
    public async Task CancellingFreesTheSlot()
    {
        JsonElement booking = await CreateBooking(Operator,
            BookingPayload(station: SingleSlotStation, leadMinutes: 60));
        using HttpClient client = As(Operator);
        await client.PostAsync($"/api/bookings/{booking.GetProperty("id").GetString()}/cancel", null);

        await CreateBooking(Operator, BookingPayload(station: SingleSlotStation, leadMinutes: 70));
    }
}
