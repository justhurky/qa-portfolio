using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace GroundStation.ApiTests;

public class AuthorisationApiTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    [Theory(DisplayName = "IT-40 the booking decision table holds end to end")]
    [InlineData("R1", Viewer, "ROUTINE", 60, HttpStatusCode.Forbidden, "ACCESS_DENIED")]
    [InlineData("R2", Operator, "ROUTINE", 60, HttpStatusCode.Created, null)]
    [InlineData("R3", Operator, "ROUTINE", 5, HttpStatusCode.UnprocessableEntity, "LEAD_TIME_TOO_SHORT")]
    [InlineData("R4", Operator, "PRIORITY", 60, HttpStatusCode.Created, null)]
    [InlineData("R5", Operator, "PRIORITY", 5, HttpStatusCode.UnprocessableEntity, "LEAD_TIME_TOO_SHORT")]
    [InlineData("R6", Operator, "EMERGENCY", 60, HttpStatusCode.Forbidden, "PRIORITY_NOT_PERMITTED")]
    [InlineData("R7", Supervisor, "EMERGENCY", 60, HttpStatusCode.Created, null)]
    [InlineData("R8", Supervisor, "EMERGENCY", 5, HttpStatusCode.Created, null)]
    [InlineData("R9", Admin, "EMERGENCY", 5, HttpStatusCode.Created, null)]
    public async Task DecisionTableHolds(string rule, string apiKey, string priority, double leadMinutes,
        HttpStatusCode expectedStatus, string? expectedCode)
    {
        using HttpClient client = As(apiKey);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(leadMinutes: leadMinutes, priority: priority), Json);

        Assert.True(expectedStatus == response.StatusCode,
            $"{rule}: expected {expectedStatus} but got {response.StatusCode}");
        if (expectedCode is not null)
        {
            Assert.Equal(expectedCode, await ErrorCodeOf(response));
        }
    }

    [Fact(DisplayName = "IT-41 the role check wins over the timing check")]
    public async Task RoleFailureBeatsTimingFailure()
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload(leadMinutes: 1, priority: "EMERGENCY"), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("PRIORITY_NOT_PERMITTED", await ErrorCodeOf(response));
    }

    [Fact(DisplayName = "IT-42 a viewer may read but not create")]
    public async Task ViewerIsReadOnly()
    {
        using HttpClient client = As(Viewer);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/bookings")).StatusCode);
        HttpResponseMessage create = await client.PostAsJsonAsync("/api/bookings", BookingPayload(), Json);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Theory(DisplayName = "IT-43 only an administrator may create a station")]
    [InlineData(Viewer, HttpStatusCode.Forbidden)]
    [InlineData(Operator, HttpStatusCode.Forbidden)]
    [InlineData(Supervisor, HttpStatusCode.Forbidden)]
    [InlineData(Admin, HttpStatusCode.Created)]
    public async Task StationCreationRequiresAdmin(string apiKey, HttpStatusCode expected)
    {
        using HttpClient client = As(apiKey);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/stations",
            new { code = "NEW", name = "New dish" }, Json);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact(DisplayName = "IT-44 a duplicate station code is refused")]
    public async Task DuplicateStationIsRefused()
    {
        using HttpClient client = As(Admin);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/stations",
            new { code = "KIR", name = "Duplicate" }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("STATION_ALREADY_EXISTS", await ErrorCodeOf(response));
    }

    [Theory(DisplayName = "IT-45 station capacity is validated")]
    [InlineData(0)]
    [InlineData(9)]
    public async Task StationCapacityIsValidated(int capacity)
    {
        using HttpClient client = As(Admin);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/stations",
            new { code = "CAP", name = "Capacity probe", capacity }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class LifecycleApiTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    private async Task<string> BookingIn(string state)
    {
        JsonElement booking = await CreateBooking(Operator, BookingPayload());
        string id = booking.GetProperty("id").GetString()!;

        if (state == "Requested")
        {
            return id;
        }

        using HttpClient supervisor = As(Supervisor);
        if (state == "Cancelled")
        {
            await supervisor.PostAsync($"/api/bookings/{id}/cancel", null);
            return id;
        }

        await supervisor.PostAsync($"/api/bookings/{id}/confirm", null);
        if (state == "Completed")
        {
            await supervisor.PostAsync($"/api/bookings/{id}/complete", null);
        }

        return id;
    }

    [Fact(DisplayName = "IT-50 a new booking starts in REQUESTED")]
    public async Task NewBookingStartsRequested()
    {
        JsonElement booking = await CreateBooking(Operator, BookingPayload());

        Assert.Equal("REQUESTED", booking.GetProperty("state").GetString());
        Assert.False(booking.TryGetProperty("decidedBy", out _));
    }

    [Theory(DisplayName = "IT-51 a permitted transition succeeds")]
    [InlineData("Requested", "confirm", "CONFIRMED")]
    [InlineData("Requested", "cancel", "CANCELLED")]
    [InlineData("Confirmed", "complete", "COMPLETED")]
    [InlineData("Confirmed", "cancel", "CANCELLED")]
    public async Task PermittedTransitionsSucceed(string from, string action, string expected)
    {
        string id = await BookingIn(from);
        using HttpClient client = As(Supervisor);

        HttpResponseMessage response = await client.PostAsync($"/api/bookings/{id}/{action}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await BodyOf(response);
        Assert.Equal(expected, body.GetProperty("state").GetString());
        Assert.Equal("sam.supervisor", body.GetProperty("decidedBy").GetString());
    }

    [Theory(DisplayName = "IT-52 a forbidden transition is refused")]
    [InlineData("Requested", "complete")]
    [InlineData("Confirmed", "confirm")]
    [InlineData("Completed", "confirm")]
    [InlineData("Completed", "cancel")]
    [InlineData("Cancelled", "confirm")]
    [InlineData("Cancelled", "complete")]
    public async Task ForbiddenTransitionsAreRefused(string from, string action)
    {
        string id = await BookingIn(from);
        using HttpClient client = As(Supervisor);

        HttpResponseMessage response = await client.PostAsync($"/api/bookings/{id}/{action}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ILLEGAL_STATE_TRANSITION", await ErrorCodeOf(response));
    }

    [Fact(DisplayName = "IT-53 the full path walks REQUESTED to COMPLETED")]
    public async Task FullPathIsWalked()
    {
        string id = await BookingIn("Completed");
        using HttpClient client = As(Viewer);

        JsonElement body = await BodyOf(await client.GetAsync($"/api/bookings/{id}"));

        Assert.Equal("COMPLETED", body.GetProperty("state").GetString());
    }

    [Fact(DisplayName = "IT-54 an operator may cancel their own booking")]
    public async Task OwnerMayCancel()
    {
        string id = await BookingIn("Requested");
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsync($"/api/bookings/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "IT-55 another operator may not cancel someone else's booking")]
    public async Task OtherOperatorMayNotCancel()
    {
        string id = await BookingIn("Requested");
        using HttpClient client = As(OtherOperator);

        HttpResponseMessage response = await client.PostAsync($"/api/bookings/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("ACCESS_DENIED", await ErrorCodeOf(response));
    }

    [Theory(DisplayName = "IT-56 an operator may not decide a booking")]
    [InlineData("confirm")]
    [InlineData("complete")]
    public async Task OperatorMayNotDecide(string action)
    {
        string id = await BookingIn(action == "complete" ? "Confirmed" : "Requested");
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsync($"/api/bookings/{id}/{action}", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "IT-57 an unknown booking is reported as not found")]
    public async Task UnknownBookingIsReported()
    {
        using HttpClient client = As(Supervisor);

        HttpResponseMessage response = await client.PostAsync(
            $"/api/bookings/{Guid.NewGuid()}/confirm", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("BOOKING_NOT_FOUND", await ErrorCodeOf(response));
    }

    [Fact(DisplayName = "IT-58 an unknown action is reported as not found")]
    public async Task UnknownActionIsReported()
    {
        string id = await BookingIn("Requested");
        using HttpClient client = As(Supervisor);

        HttpResponseMessage response = await client.PostAsync($"/api/bookings/{id}/approve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public class QueryApiTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    private async Task SeedThree()
    {
        await CreateBooking(Operator, BookingPayload(station: SpareStation, leadMinutes: 300));
        await CreateBooking(Operator, BookingPayload(station: SingleSlotStation, leadMinutes: 100));
        await CreateBooking(Supervisor, BookingPayload(station: TwoSlotStation, leadMinutes: 200));
    }

    [Fact(DisplayName = "IT-60 bookings come back ordered by start time")]
    public async Task BookingsAreOrdered()
    {
        await SeedThree();
        using HttpClient client = As(Viewer);

        JsonElement body = await BodyOf(await client.GetAsync("/api/bookings"));

        List<DateTimeOffset> starts = body.GetProperty("content").EnumerateArray()
            .Select(item => item.GetProperty("startsAt").GetDateTimeOffset()).ToList();
        Assert.Equal(starts.OrderBy(moment => moment).ToList(), starts);
    }

    [Fact(DisplayName = "IT-61 the station filter narrows the result")]
    public async Task StationFilterNarrowsTheResult()
    {
        await SeedThree();
        using HttpClient client = As(Viewer);

        JsonElement body = await BodyOf(
            await client.GetAsync($"/api/bookings?stationCode={SingleSlotStation}"));

        Assert.Equal(1, body.GetProperty("totalElements").GetInt32());
    }

    [Fact(DisplayName = "IT-62 the requester filter narrows the result")]
    public async Task RequesterFilterNarrowsTheResult()
    {
        await SeedThree();
        using HttpClient client = As(Viewer);

        JsonElement body = await BodyOf(
            await client.GetAsync("/api/bookings?requestedBy=sam.supervisor"));

        Assert.Equal(1, body.GetProperty("totalElements").GetInt32());
    }

    [Fact(DisplayName = "IT-63 pagination slices the content and reports the totals")]
    public async Task PaginationSlicesTheContent()
    {
        await SeedThree();
        using HttpClient client = As(Viewer);

        JsonElement first = await BodyOf(await client.GetAsync("/api/bookings?page=0&size=2"));
        JsonElement second = await BodyOf(await client.GetAsync("/api/bookings?page=1&size=2"));

        Assert.Equal(2, first.GetProperty("content").GetArrayLength());
        Assert.Equal(3, first.GetProperty("totalElements").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, second.GetProperty("content").GetArrayLength());
    }

    [Fact(DisplayName = "IT-64 a page beyond the end is empty rather than an error")]
    public async Task PageBeyondTheEndIsEmpty()
    {
        await SeedThree();
        using HttpClient client = As(Viewer);

        JsonElement body = await BodyOf(await client.GetAsync("/api/bookings?page=99"));

        Assert.Equal(0, body.GetProperty("content").GetArrayLength());
        Assert.Equal(3, body.GetProperty("totalElements").GetInt32());
    }

    [Theory(DisplayName = "IT-65 invalid pagination is rejected")]
    [InlineData("page=-1")]
    [InlineData("size=0")]
    [InlineData("size=101")]
    [InlineData("page=abc")]
    public async Task InvalidPaginationIsRejected(string query)
    {
        using HttpClient client = As(Viewer);

        HttpResponseMessage response = await client.GetAsync($"/api/bookings?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await ErrorCodeOf(response));
    }

    [Fact(DisplayName = "IT-66 an unknown state filter is rejected")]
    public async Task UnknownStateFilterIsRejected()
    {
        using HttpClient client = As(Viewer);

        HttpResponseMessage response = await client.GetAsync("/api/bookings?state=PENDING");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "IT-67 an unknown API route is reported as not found")]
    public async Task UnknownRouteIsReported()
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.GetAsync("/api/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NOT_FOUND", await ErrorCodeOf(response));
    }

    [Fact(DisplayName = "IT-68 responses declare a nosniff policy")]
    public async Task ResponsesDeclareNosniff()
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.GetAsync("/api/me");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Theory(DisplayName = "IT-68b a malformed body is refused with an error code (DEF-004)")]
    [Trait("Category", "KnownDefect")]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    public async Task MalformedBodiesCarryAnErrorCode(string payload)
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsync("/api/bookings",
            new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MALFORMED_JSON", await ErrorCodeOf(response));
    }

    [Fact(DisplayName = "IT-68c a malformed body never leaks a stack trace (DEF-004)")]
    [Trait("Category", "KnownDefect")]
    public async Task MalformedBodiesNeverLeakAStackTrace()
    {
        using HttpClient client = As(Operator);

        HttpResponseMessage response = await client.PostAsync("/api/bookings",
            new System.Net.Http.StringContent("{broken", System.Text.Encoding.UTF8, "application/json"));
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("System.", body);
        Assert.DoesNotContain("   at ", body);
    }

    [Fact(DisplayName = "IT-69 the web interface is served at the root")]
    public async Task WebInterfaceIsServed()
    {
        using HttpClient client = As(null);

        string body = await (await client.GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Contains("Ground Station Booking", body);
    }
}

public class ConcurrencyApiTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    [Fact(DisplayName = "IT-70 two simultaneous bookings leave exactly one winner on a single slot")]
    public async Task OnlyOneWinnerOnASingleSlot()
    {
        object payload = BookingPayload(station: SingleSlotStation, leadMinutes: 120);
        using HttpClient first = As(Operator);
        using HttpClient second = As(Operator);

        HttpResponseMessage[] responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/bookings", payload, Json),
            second.PostAsJsonAsync("/api/bookings", payload, Json));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact(DisplayName = "IT-71 four racers on two slots leave exactly two winners")]
    public async Task ExactlyTwoWinnersOnTwoSlots()
    {
        object payload = BookingPayload(station: TwoSlotStation, leadMinutes: 180);
        HttpClient[] clients = [As(Operator), As(Operator), As(Operator), As(Operator)];

        HttpResponseMessage[] responses = await Task.WhenAll(
            clients.Select(client => client.PostAsJsonAsync("/api/bookings", payload, Json)));

        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (HttpClient client in clients)
        {
            client.Dispose();
        }
    }

    [Fact(DisplayName = "IT-72 a booking cannot be confirmed twice in parallel")]
    public async Task ConfirmIsNotIdempotentUnderRace()
    {
        JsonElement booking = await CreateBooking(Operator, BookingPayload(leadMinutes: 240));
        string id = booking.GetProperty("id").GetString()!;
        using HttpClient first = As(Supervisor);
        using HttpClient second = As(Supervisor);

        HttpResponseMessage[] responses = await Task.WhenAll(
            first.PostAsync($"/api/bookings/{id}/confirm", null),
            second.PostAsync($"/api/bookings/{id}/confirm", null));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }
}
