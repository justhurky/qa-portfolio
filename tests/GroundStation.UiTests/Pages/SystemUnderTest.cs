using System.Diagnostics;
using System.Net.Sockets;

namespace GroundStation.UiTests.Pages;

public sealed class SystemUnderTest : IDisposable
{
    private readonly Process? process;

    public string BaseUrl { get; }

    private SystemUnderTest(string baseUrl, Process? process)
    {
        BaseUrl = baseUrl;
        this.process = process;
    }

    public static SystemUnderTest Start()
    {
        string? configured = Environment.GetEnvironmentVariable("SUT_BASE_URL");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            WaitUntilHealthy(configured);
            return new SystemUnderTest(configured, null);
        }

        int port = FreePort();
        string baseUrl = $"http://127.0.0.1:{port}";
        string projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "GroundStation.Api"));

        ProcessStartInfo startInfo = new("dotnet")
        {
            ArgumentList = { "run", "--project", projectPath, "--urls", baseUrl },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        Process started = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the system under test");
        WaitUntilHealthy(baseUrl);
        return new SystemUnderTest(baseUrl, started);
    }

    public async Task ResetAsync()
    {
        using HttpClient client = new() { BaseAddress = new Uri(BaseUrl) };
        HttpResponseMessage response = await client.PostAsync("/api/test/reset", null);
        response.EnsureSuccessStatusCode();
    }

    private static int FreePort()
    {
        TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void WaitUntilHealthy(string baseUrl)
    {
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(2) };
        DateTime deadline = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                HttpResponseMessage response = client.GetAsync($"{baseUrl}/api/health").GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                Thread.Sleep(500);
            }
        }

        throw new TimeoutException($"The system under test at {baseUrl} did not become healthy");
    }

    public void Dispose()
    {
        if (process is null || process.HasExited)
        {
            return;
        }

        process.Kill(entireProcessTree: true);
        process.WaitForExit(10_000);
        process.Dispose();
    }
}
