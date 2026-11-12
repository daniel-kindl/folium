using Folium.Updates;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class UpdateViewModelTests
{
    private static readonly ReleaseInfo Latest = new(
        SemVersion.Parse("0.2.0"),
        new Uri("https://example.test/folium.apk"),
        100,
        "Notes",
        new Uri("https://example.test/release"));

    [Fact]
    public void SettingsCanBeClosedAfterOpening()
    {
        using SettingsPath settings = new();
        using UpdateViewModel viewModel = new(new UpdateCheckService(new Source(Latest), settings.File), new Installer(), "0.1.0");
        int commandUpdates = 0;
        viewModel.CloseSettingsCommand.CanExecuteChanged += (_, _) => commandUpdates++;

        viewModel.ToggleSettingsCommand.Execute(parameter: null);

        Assert.True(viewModel.CloseSettingsCommand.CanExecute(parameter: null));
        Assert.Equal(1, commandUpdates);
        viewModel.CloseSettingsCommand.Execute(parameter: null);
        Assert.False(viewModel.CloseSettingsCommand.CanExecute(parameter: null));
        Assert.Equal(2, commandUpdates);
    }

    [Fact]
    public async Task AutomaticCheckDoesNotClearAnExistingOfferWhenNoCheckIsDue()
    {
        using SettingsPath settings = new();
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        using UpdateViewModel viewModel = new(
            new UpdateCheckService(new Source(Latest), settings.File, () => now),
            new Installer(),
            "0.1.0");
        await viewModel.CheckNowAsync(TestContext.Current.CancellationToken);

        await viewModel.CheckAtStartAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasRelease);
        Assert.True(viewModel.RetryCommand.CanExecute(parameter: null));
    }

    [Fact]
    public async Task CheckNowOffersTheReleaseAndSkipRemembersIt()
    {
        using SettingsPath settings = new();
        UpdateCheckService checks = new(new Source(Latest), settings.File, () => DateTimeOffset.UnixEpoch);
        using UpdateViewModel viewModel = new(checks, new Installer(), "0.1.0");
        viewModel.UpdateCommand.Execute(parameter: null);
        Assert.False(viewModel.IsBusy);

        await viewModel.CheckNowAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.HasRelease);
        Assert.Equal("Folium 0.2.0 is available", viewModel.OfferText);
        viewModel.SkipCommand.Execute(parameter: null);

        Assert.False(viewModel.HasRelease);
        Assert.Equal("0.2.0", settings.File.Read().SkippedVersion);
        await viewModel.CheckAtStartAsync(TestContext.Current.CancellationToken);
        Assert.False(viewModel.HasRelease);
    }

    [Fact]
    public async Task LaterHidesTheOfferUntilAnotherCheckIsDue()
    {
        using SettingsPath settings = new();
        CountingSource source = new(Latest);
        using UpdateViewModel viewModel = new(new UpdateCheckService(source, settings.File), new Installer(), "0.1.0");
        await viewModel.CheckNowAsync(TestContext.Current.CancellationToken);
        viewModel.LaterCommand.Execute(parameter: null);

        await viewModel.CheckAtStartAsync(TestContext.Current.CancellationToken);

        Assert.False(viewModel.HasRelease);
        Assert.Equal(1, source.RequestCount);
    }

    [Fact]
    public async Task CheckFailureDoesNotBreakTheLibrary()
    {
        using SettingsPath settings = new();
        using UpdateViewModel viewModel = new(
            new UpdateCheckService(new FailingSource(), settings.File),
            installer: null,
            currentVersion: "0.1.0");

        await viewModel.CheckAtStartAsync(TestContext.Current.CancellationToken);
        viewModel.UpdateCommand.Execute(parameter: null);

        Assert.False(viewModel.HasRelease);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task InstallWaitsForPermissionAndContinuesAfterSettings()
    {
        using SettingsPath settings = new();
        Installer installer = new() { CanInstall = false };
        using UpdateViewModel viewModel = new(new UpdateCheckService(new Source(Latest), settings.File), installer, "0.1.0");
        await viewModel.CheckNowAsync(TestContext.Current.CancellationToken);
        viewModel.UpdateCommand.Execute(parameter: null);
        Assert.True(viewModel.NeedsInstallPermission);
        Assert.Equal(0, installer.InstallCount);

        installer.CanInstall = true;
        viewModel.OnResume();

        Assert.False(viewModel.NeedsInstallPermission);
        Assert.Equal(1, installer.InstallCount);
        Assert.False(viewModel.IsBusy);
    }

    [Theory]
    [InlineData("download", "Download failed.")]
    [InlineData("signature", "Signature mismatch.")]
    [InlineData("file", "File check failed.")]
    [InlineData("install", "Install failed.")]
    public async Task InstallFailuresShowShortReasons(string failure, string expected)
    {
        using SettingsPath settings = new();
        Installer installer = new()
        {
            Failure = failure switch
            {
                "download" => new HttpRequestException("private request details"),
                "signature" => new InvalidOperationException("certificate mismatch"),
                "file" => new InvalidOperationException("APK invalid"),
                _ => new InvalidOperationException("Android installer rejected update"),
            },
        };
        using UpdateViewModel viewModel = new(new UpdateCheckService(new Source(Latest), settings.File), installer, "0.1.0");
        await viewModel.CheckNowAsync(TestContext.Current.CancellationToken);

        viewModel.RetryCommand.Execute(parameter: null);

        Assert.Equal(expected, viewModel.Error);
        Assert.True(viewModel.HasError);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task CancelStopsTheDownloadAndReleasePageOpens()
    {
        using SettingsPath settings = new();
        Installer installer = new() { WaitForCancellation = true };
        using UpdateViewModel viewModel = new(new UpdateCheckService(new Source(Latest), settings.File), installer, "0.1.0");
        await viewModel.CheckNowAsync(TestContext.Current.CancellationToken);
        viewModel.UpdateCommand.Execute(parameter: null);
        await installer.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => viewModel.CanCancel, TestContext.Current.CancellationToken);
        Assert.True(viewModel.CancelCommand.CanExecute(parameter: null));
        viewModel.CancelCommand.Execute(parameter: null);
        await installer.Finished.Task.WaitAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => viewModel.HasError, TestContext.Current.CancellationToken);

        Assert.Equal("Update download cancelled.", viewModel.Error);
        viewModel.OpenReleasePageCommand.Execute(parameter: null);
        Assert.Equal(Latest.PageUrl, installer.OpenedPage);
    }

    private sealed class Source(ReleaseInfo? release) : IReleaseSource
    {
        public Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(release);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token).ConfigureAwait(false);
        }
    }

    private sealed class FailingSource : IReleaseSource
    {
        public Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
        {
            return Task.FromException<ReleaseInfo?>(new HttpRequestException("offline"));
        }
    }

    private sealed class CountingSource(ReleaseInfo release) : IReleaseSource
    {
        public int RequestCount { get; private set; }

        public Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult<ReleaseInfo?>(release);
        }
    }

    private sealed class Installer : IUpdateInstaller
    {
        public bool CanInstall { get; set; } = true;
        public bool CanRequestInstallPackages => CanInstall;
        public int InstallCount { get; private set; }
        public Exception? Failure { get; init; }
        public bool WaitForCancellation { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Uri? OpenedPage { get; private set; }

        public Task InstallAsync(ReleaseInfo release, IProgress<UpdateProgress> progress, CancellationToken cancellationToken)
        {
            InstallCount++;
            progress.Report(new UpdateProgress(0.5, CanCancel: true));
            Task completed = Failure is null ? Task.CompletedTask : Task.FromException(Failure);
            return WaitForCancellation ? WaitAsync(cancellationToken) : completed;
        }

        public void OpenInstallPermissionSettings() { }

        public void OpenReleasePage(Uri pageUrl)
        {
            OpenedPage = pageUrl;
        }

        private async Task WaitAsync(CancellationToken cancellationToken)
        {
            _ = Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ = Finished.TrySetResult();
            }
        }
    }

    private sealed class SettingsPath : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public UpdateSettingsFile File { get; }

        public SettingsPath()
        {
            Directory.CreateDirectory(_directory);
            File = new UpdateSettingsFile(Path.Combine(_directory, "updates.json"));
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
