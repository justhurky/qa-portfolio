using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GroundStation.ApiTests;

public sealed class BookingApiFactory : WebApplicationFactory<Program>
{
    public HttpClient ClientFor(string? apiKey)
    {
        HttpClient client = CreateClient();
        if (apiKey is not null)
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        return client;
    }
}

public abstract class ApiTestBase : IClassFixture<BookingApiFactory>, IAsyncLifetime
{
    protected const string Viewer = "viewer-key";
    protected const string Operator = "operator-key";
    protected const string OtherOperator = "operator2-key";
    protected const string Supervisor = "supervisor-key";
    protected const string Admin = "admin-key";

    protected const string SingleSlotStation = "KIR";
    protected const string TwoSlotStation = "SVB";
    protected const string SpareStation = "PER";
    protected const string InactiveStation = "OLD";

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly BookingApiFactory factory;

    protected ApiTestBase(BookingApiFactory factory) => this.factory = factory;

    public async Task InitializeAsync()
    {
        using HttpClient client = factory.ClientFor(null);
        HttpResponseMessage response = await client.PostAsync("/api/test/reset", null);
        response.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected HttpClient As(string? apiKey) => factory.ClientFor(apiKey);

    protected static string InMinutes(double minutes) =>
        DateTimeOffset.UtcNow.AddMinutes(minutes).ToString("O");

    protected static object BookingPayload(string station = SpareStation, double leadMinutes = 60,
        double durationMinutes = 30, string purpose = "Scheduled housekeeping pass",
        string? priority = null)
    {
        DateTimeOffset starts = DateTimeOffset.UtcNow.AddMinutes(leadMinutes);
        Dictionary<string, object?> payload = new()
        {
            ["stationCode"] = station,
            ["purpose"] = purpose,
            ["startsAt"] = starts.ToString("O"),
            ["endsAt"] = starts.AddMinutes(durationMinutes).ToString("O")
        };
        if (priority is not null)
        {
            payload["priority"] = priority;
        }

        return payload;
    }

    protected static async Task<JsonElement> BodyOf(HttpResponseMessage response)
    {
        string text = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(text)
            ? default
            : JsonDocument.Parse(text).RootElement.Clone();
    }

    protected static async Task<string> ErrorCodeOf(HttpResponseMessage response)
    {
        JsonElement body = await BodyOf(response);
        return body.GetProperty("error").GetProperty("code").GetString()!;
    }

    protected async Task<JsonElement> CreateBooking(string apiKey, object payload)
    {
        using HttpClient client = As(apiKey);
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/bookings", payload, Json);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await BodyOf(response);
    }
}
