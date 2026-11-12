using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Folium.Updates;

namespace Folium.Tests;

public sealed class UpdateReleaseSourceTests
{
    [Fact]
    public async Task ReadsLatestReleaseAndUsesGithubHeaders()
    {
        using FixtureHandler handler = new(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "github-release.json"), TestContext.Current.CancellationToken));
        using HttpClient client = new(handler) { Timeout = TimeSpan.FromSeconds(4) };
        GitHubReleaseSource source = new(client);

        ReleaseInfo? release = await source.GetLatestAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(release);
        Assert.Equal("0.2.0", release.Version.ToString());
        Assert.Equal(1234, release.SizeBytes);
        Assert.Equal("Release notes", release.Notes);
        Assert.Contains("application/vnd.github+json", handler.Accept, StringComparison.Ordinal);
        Assert.Contains("Folium-Android", handler.UserAgent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task MissingOrRateLimitedReleasesAreQuiet(HttpStatusCode status)
    {
        using StatusHandler handler = new(status);
        using HttpClient client = new(handler);
        GitHubReleaseSource source = new(client);
        Assert.Null(await source.GetLatestAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HttpFailureAndInvalidJsonAreQuiet()
    {
        using ThrowingHandler failure = new();
        using HttpClient failedClient = new(failure);
        GitHubReleaseSource failedSource = new(failedClient);
        Assert.Null(await failedSource.GetLatestAsync(TestContext.Current.CancellationToken));

        using FixtureHandler invalid = new("{");
        using HttpClient invalidClient = new(invalid);
        GitHubReleaseSource invalidSource = new(invalidClient);
        Assert.Null(await invalidSource.GetLatestAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsAnHttpRedirect()
    {
        using StatusHandler handler = new(HttpStatusCode.OK, useHttpUri: true);
        using HttpClient client = new(handler);
        GitHubReleaseSource source = new(client);
        Assert.Null(await source.GetLatestAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("draft", "true")]
    [InlineData("prerelease", "true")]
    [InlineData("tag_name", "bad")]
    public void RejectsUnstableOrMalformedRelease(string field, string value)
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "github-release.json")));
        Dictionary<string, object?> release = JsonSerializer.Deserialize<Dictionary<string, object?>>(fixture.RootElement.GetRawText())!;
        release[field] = string.Equals(value, "true", StringComparison.Ordinal) ? true : value;
        using JsonDocument modified = JsonDocument.Parse(JsonSerializer.Serialize(release));

        Assert.Null(GitHubReleaseSource.Parse(modified.RootElement));
    }

    [Fact]
    public void RejectsDuplicateMatchingAssetsAndHttpUrls()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "github-release.json"));
        JsonObject duplicate = JsonNode.Parse(json)!.AsObject();
        duplicate["assets"]!.AsArray().Add(JsonNode.Parse("{\"name\":\"folium-0.2.0.apk\",\"browser_download_url\":\"https://example.test/app.apk\"}"));
        using JsonDocument doc = JsonDocument.Parse(duplicate.ToJsonString());
        Assert.Null(GitHubReleaseSource.Parse(doc.RootElement));
        using JsonDocument http = JsonDocument.Parse(json.Replace("https://github.com", "http://github.com", StringComparison.Ordinal));
        Assert.Null(GitHubReleaseSource.Parse(http.RootElement));
    }

    [Fact]
    public void RejectsReleaseWithoutTheNamedApkAsset()
    {
        JsonObject release = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "github-release.json")))!.AsObject();
        release["assets"]!.AsArray().Clear();
        using JsonDocument document = JsonDocument.Parse(release.ToJsonString());
        Assert.Null(GitHubReleaseSource.Parse(document.RootElement));
    }

    [Fact]
    public async Task DisabledChecksDoNotCallReleaseSource()
    {
        using TemporaryDirectory directory = new();
        UpdateSettingsFile settings = new(Path.Combine(directory.Path, "updates.json"));
        settings.Write(new UpdateSettings(ChecksEnabled: false));
        CountingSource source = new();
        UpdateCheckService service = new(source, settings);

        _ = await service.CheckAsync(currentVersion: "0.1.0", force: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, source.RequestCount);
    }

    [Fact]
    public async Task CheckNowIgnoresTheAutomaticInterval()
    {
        using TemporaryDirectory directory = new();
        DateTimeOffset now = new(year: 2026, month: 11, day: 12, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
        UpdateSettingsFile file = new(Path.Combine(directory.Path, "updates.json"));
        file.Write(new UpdateSettings(LastCheck: now.AddHours(-1)));
        ReleaseInfo expected = new(SemVersion.Parse("0.2.0"), new Uri("https://example.test/app.apk"), 10, "notes", new Uri("https://example.test/release"));
        CountingSource source = new(expected);
        UpdateCheckService service = new(source, file, () => now);

        Assert.Null(await service.CheckAsync(currentVersion: "0.1.0", force: false, TestContext.Current.CancellationToken));
        Assert.Equal(0, source.RequestCount);
        Assert.Equal(expected, await service.CheckAsync(currentVersion: "0.1.0", force: true, TestContext.Current.CancellationToken));
        Assert.Equal(1, source.RequestCount);
        Assert.Equal(now, file.Read().LastCheck);
    }

    [Fact]
    public async Task OverlappingAutomaticChecksMakeOneReleaseRequest()
    {
        using TemporaryDirectory directory = new();
        DateTimeOffset now = new(year: 2026, month: 11, day: 12, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
        UpdateSettingsFile file = new(Path.Combine(directory.Path, "updates.json"));
        BlockingSource source = new();
        UpdateCheckService service = new(source, file, () => now);

        Task<ReleaseInfo?> first = service.CheckAsync("0.1.0", force: false, TestContext.Current.CancellationToken);
        await source.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        Task<ReleaseInfo?> second = service.CheckAsync("0.1.0", force: false, TestContext.Current.CancellationToken);
        _ = source.Complete.TrySetResult(null);

        _ = await Task.WhenAll(first, second);

        Assert.Equal(1, source.RequestCount);
        Assert.Equal(now, file.Read().LastCheck);
    }

    private sealed class FixtureHandler(string json) : HttpMessageHandler
    {
        public string Accept { get; private set; } = "";
        public string UserAgent { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Accept = request.Headers.Accept.ToString();
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent(json) });
        }
    }

    private sealed class StatusHandler(HttpStatusCode status, bool useHttpUri = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (useHttpUri)
            {
                request.RequestUri = new Uri("http://example.test/redirect");
            }

            return Task.FromResult(new HttpResponseMessage(status) { RequestMessage = request });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(new HttpRequestException("request failed"));
        }
    }

    private sealed class CountingSource(ReleaseInfo? result = null) : IReleaseSource
    {
        public int RequestCount { get; private set; }

        public Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class BlockingSource : IReleaseSource
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ReleaseInfo?> Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RequestCount { get; private set; }

        public Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
        {
            RequestCount++;
            _ = Started.TrySetResult();
            return Complete.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
