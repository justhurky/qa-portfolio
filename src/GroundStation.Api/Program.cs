using System.Text.Json;
using System.Text.Json.Serialization;
using GroundStation.Api.Contracts;
using GroundStation.Api.Domain;
using GroundStation.Api.Storage;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<BookingStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

WebApplication app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});

IResult Error(int status, string code, string message, IDictionary<string, object?>? details = null) =>
    Results.Json(ErrorBody.Of(code, message, details), statusCode: status);

IResult FromRule(RuleResult rule)
{
    Dictionary<string, object?>? details = rule.Field is null
        ? null
        : new Dictionary<string, object?> { ["field"] = rule.Field };
    return Error(rule.Status, rule.Code!, rule.Message!, details);
}

(ApiUser? user, IResult? failure) Authenticate(HttpContext context, BookingStore store)
{
    string? apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return (null, Error(401, "AUTHENTICATION_REQUIRED", "X-Api-Key header is required"));
    }

    ApiUser? user = store.FindUser(apiKey);
    return user is null
        ? (null, Error(401, "AUTHENTICATION_FAILED", "Unknown API key"))
        : (user, null);
}

(int page, int size, IResult? failure) ReadPaging(HttpRequest request)
{
    string? rawPage = request.Query["page"].FirstOrDefault();
    string? rawSize = request.Query["size"].FirstOrDefault();

    if (rawPage is not null && (!int.TryParse(rawPage, out int parsedPage) || parsedPage < 0))
    {
        return (0, 0, Error(400, "VALIDATION_FAILED", "page must be a non-negative integer",
            new Dictionary<string, object?> { ["field"] = "page" }));
    }

    if (rawSize is not null && (!int.TryParse(rawSize, out int parsedSize) || parsedSize < 1 || parsedSize > 100))
    {
        return (0, 0, Error(400, "VALIDATION_FAILED", "size must be between 1 and 100",
            new Dictionary<string, object?> { ["field"] = "size" }));
    }

    int page = rawPage is null ? 0 : int.Parse(rawPage);
    int size = rawSize is null ? 20 : int.Parse(rawSize);
    return (page, size, null);
}

app.MapGet("/api/health", (TimeProvider clock) =>
    Results.Ok(new { status = "UP", time = clock.GetUtcNow() }));

app.MapPost("/api/test/reset", (BookingStore store) =>
{
    store.Reset();
    return Results.Ok(new { status = "RESET" });
});

app.MapGet("/api/me", (HttpContext context, BookingStore store) =>
{
    (ApiUser? user, IResult? failure) = Authenticate(context, store);
    return failure ?? Results.Ok(new IdentityResponse(user!.Username, user.Role.ToString().ToUpperInvariant()));
});

app.MapGet("/api/stations", (HttpContext context, BookingStore store) =>
{
    (ApiUser? user, IResult? failure) = Authenticate(context, store);
    if (failure is not null)
    {
        return failure;
    }

    (int page, int size, IResult? pagingFailure) = ReadPaging(context.Request);
    if (pagingFailure is not null)
    {
        return pagingFailure;
    }

    bool activeOnly = context.Request.Query["activeOnly"] == "true";
    List<StationResponse> stations = store.ListStations(activeOnly).Select(StationResponse.From).ToList();
    return Results.Ok(PageResponse<StationResponse>.Of(stations, page, size));
});

app.MapGet("/api/stations/{code}", (string code, HttpContext context, BookingStore store) =>
{
    (ApiUser? user, IResult? failure) = Authenticate(context, store);
    if (failure is not null)
    {
        return failure;
    }

    Station? station = store.FindStation(code);
    return station is null
        ? Error(404, "STATION_NOT_FOUND", $"No station with code {code}")
        : Results.Ok(StationResponse.From(station));
});

app.MapPost("/api/stations", (CreateStationRequest request, HttpContext context, BookingStore store) =>
{
    (ApiUser? user, IResult? failure) = Authenticate(context, store);
    if (failure is not null)
    {
        return failure;
    }

    RuleResult permission = RolePolicy.MayManageStations(user!.Role);
    if (!permission.IsAllowed)
    {
        return FromRule(permission);
    }

    if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length is < 2 or > 16)
    {
        return Error(400, "VALIDATION_FAILED", "code must be between 2 and 16 characters",
            new Dictionary<string, object?> { ["field"] = "code" });
    }

    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 64)
    {
        return Error(400, "VALIDATION_FAILED", "name must be between 2 and 64 characters",
            new Dictionary<string, object?> { ["field"] = "name" });
    }

    int capacity = request.Capacity ?? 1;
    if (capacity is < 1 or > 8)
    {
        return Error(400, "VALIDATION_FAILED", "capacity must be an integer between 1 and 8",
            new Dictionary<string, object?> { ["field"] = "capacity" });
    }

    string code = request.Code.Trim().ToUpperInvariant();
    if (store.FindStation(code) is not null)
    {
        return Error(409, "STATION_ALREADY_EXISTS", $"Station {code} already exists");
    }

    Station created = store.AddStation(new Station(code, request.Name.Trim(),
        request.Country ?? "XX", capacity, request.Active ?? true));
    return Results.Created($"/api/stations/{created.Code}", StationResponse.From(created));
});

app.MapGet("/api/bookings", (HttpContext context, BookingStore store) =>
{
    (ApiUser? user, IResult? failure) = Authenticate(context, store);
    if (failure is not null)
    {
        return failure;
    }

    (int page, int size, IResult? pagingFailure) = ReadPaging(context.Request);
    if (pagingFailure is not null)
    {
        return pagingFailure;
    }

    BookingState? state = null;
    string? rawState = context.Request.Query["state"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(rawState))
    {
        if (!Enum.TryParse(rawState, ignoreCase: true, out BookingState parsed))
        {
            return Error(400, "VALIDATION_FAILED", $"state {rawState} is not a known booking state",
                new Dictionary<string, object?> { ["field"] = "state" });
        }

        state = parsed;
    }

    List<BookingResponse> bookings = store
        .ListBookings(context.Request.Query["stationCode"].FirstOrDefault(), state,
            context.Request.Query["requestedBy"].FirstOrDefault())
        .Select(BookingResponse.From)
        .ToList();
    return Results.Ok(PageResponse<BookingResponse>.Of(bookings, page, size));
});

app.MapGet("/api/bookings/{id:guid}", (Guid id, HttpContext context, BookingStore store) =>
{
    (ApiUser? user, IResult? failure) = Authenticate(context, store);
    if (failure is not null)
    {
        return failure;
    }

    Booking? booking = store.FindBooking(id);
    return booking is null
        ? Error(404, "BOOKING_NOT_FOUND", $"No booking with id {id}")
        : Results.Ok(BookingResponse.From(booking));
});

app.MapPost("/api/bookings", (CreateBookingRequest request, HttpContext context, BookingStore store,
    TimeProvider clock) =>
{
    (ApiUser? user, IResult? failure) = Authenticate(context, store);
    if (failure is not null)
    {
        return failure;
    }

    RuleResult mayCreate = RolePolicy.MayCreateBooking(user!.Role);
    if (!mayCreate.IsAllowed)
    {
        return FromRule(mayCreate);
    }

    if (string.IsNullOrWhiteSpace(request.StationCode))
    {
        return Error(400, "VALIDATION_FAILED", "stationCode is required",
            new Dictionary<string, object?> { ["field"] = "stationCode" });
    }

    RuleResult purposeRule = BookingRules.CheckPurpose(request.Purpose);
    if (!purposeRule.IsAllowed)
    {
        return FromRule(purposeRule);
    }

    if (request.StartsAt is null)
    {
        return Error(400, "VALIDATION_FAILED", "startsAt must be an ISO-8601 timestamp",
            new Dictionary<string, object?> { ["field"] = "startsAt" });
    }

    if (request.EndsAt is null)
    {
        return Error(400, "VALIDATION_FAILED", "endsAt must be an ISO-8601 timestamp",
            new Dictionary<string, object?> { ["field"] = "endsAt" });
    }

    if (!PriorityWire.TryParse(request.Priority, out Priority priority))
    {
        return Error(400, "VALIDATION_FAILED",
            $"priority must be one of {string.Join(", ", PriorityWire.All)}",
            new Dictionary<string, object?> { ["field"] = "priority" });
    }

    RuleResult priorityRule = RolePolicy.MayUsePriority(user.Role, priority);
    if (!priorityRule.IsAllowed)
    {
        return FromRule(priorityRule);
    }

    Station? station = store.FindStation(request.StationCode);
    if (station is null)
    {
        return Error(404, "STATION_NOT_FOUND", $"No station with code {request.StationCode}");
    }

    if (!station.Active)
    {
        return Error(409, "STATION_INACTIVE", $"Station {station.Code} is not accepting bookings");
    }

    TimeWindow window = new(request.StartsAt.Value.ToUniversalTime(),
        request.EndsAt.Value.ToUniversalTime());

    RuleResult orderRule = BookingRules.CheckWindowOrder(window);
    if (!orderRule.IsAllowed)
    {
        return FromRule(orderRule);
    }

    RuleResult durationRule = BookingRules.CheckDuration(window);
    if (!durationRule.IsAllowed)
    {
        return FromRule(durationRule);
    }

    DateTimeOffset now = clock.GetUtcNow();
    RuleResult leadRule = BookingRules.CheckLeadTime(window, priority, now);
    if (!leadRule.IsAllowed)
    {
        return FromRule(leadRule);
    }

    Booking candidate = new()
    {
        Id = Guid.NewGuid(),
        StationCode = station.Code,
        Purpose = request.Purpose!.Trim(),
        Priority = priority,
        Window = window,
        RequestedBy = user.Username,
        RequestedAt = now
    };

    Booking? created = store.AddBookingIfCapacityAllows(candidate, station.Capacity, out RuleResult capacity);
    return created is null
        ? Error(capacity.Status, capacity.Code!, capacity.Message!,
            new Dictionary<string, object?> { ["capacity"] = station.Capacity })
        : Results.Created($"/api/bookings/{created.Id}", BookingResponse.From(created));
});

app.MapPost("/api/bookings/{id:guid}/{action}", (Guid id, string action, HttpContext context,
    BookingStore store, TimeProvider clock) =>
{
    (ApiUser? user, IResult? failure) = Authenticate(context, store);
    if (failure is not null)
    {
        return failure;
    }

    if (!Enum.TryParse(action, ignoreCase: true, out BookingAction parsed) ||
        !Enum.IsDefined(parsed))
    {
        return Error(404, "NOT_FOUND", "No such resource");
    }

    RuleResult outcome = store.TryTransition(id, parsed, user!.Username, user.Role,
        clock.GetUtcNow(), out Booking? booking);
    return outcome.IsAllowed
        ? Results.Ok(BookingResponse.From(booking!))
        : FromRule(outcome);
});

app.MapFallback((HttpContext context) =>
    context.Request.Path.StartsWithSegments("/api")
        ? Error(404, "NOT_FOUND", "No such resource")
        : Results.File(Path.Combine(app.Environment.WebRootPath, "index.html"), "text/html"));

app.Run();

public partial class Program;
