using Android.Content;
using Folium.Updates;

namespace Folium.Android;

/// <summary>Downloads and installs release APKs through Android's package installer.</summary>
public sealed class UpdateInstaller : IUpdateInstaller, IDisposable
{
    private const string PACKAGE_NAME = "dev.danielkindl.folium";
    private readonly Context _context;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _cacheDirectory;

    /// <summary>Creates the Android installer and removes stale cached APKs.</summary>
    /// <param name="context">The Android application context.</param>
    /// <param name="http">The optional HTTP client used by verification builds.</param>
    public UpdateInstaller(Context context, HttpClient? http = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cacheDirectory = context.CacheDir?.AbsolutePath ?? throw new InvalidOperationException("The app cache directory is not available.");
        _ownsHttp = http is null;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        foreach (string apk in Directory.EnumerateFiles(_cacheDirectory, "folium-update-*.apk"))
        {
            TryDelete(apk);
        }
    }

    /// <inheritdoc />
    public bool CanRequestInstallPackages => _context.PackageManager?.CanRequestPackageInstalls() ?? false;

    /// <inheritdoc />
    public void OpenInstallPermissionSettings()
    {
        using Intent intent = new(global::Android.Provider.Settings.ActionManageUnknownAppSources, global::Android.Net.Uri.Parse($"package:{PACKAGE_NAME}"));
        _ = intent.AddFlags(ActivityFlags.NewTask);
        _context.StartActivity(intent);
    }

    /// <inheritdoc />
    public void OpenReleasePage(Uri pageUrl)
    {
        ArgumentNullException.ThrowIfNull(pageUrl);
        using Intent intent = new(Intent.ActionView, global::Android.Net.Uri.Parse(pageUrl.AbsoluteUri));
        _ = intent.AddFlags(ActivityFlags.NewTask);
        _context.StartActivity(intent);
    }

    /// <inheritdoc />
    public async Task InstallAsync(ReleaseInfo release, IProgress<UpdateProgress> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(progress);
        if (!CanRequestInstallPackages)
        {
            throw new InvalidOperationException("Allow Folium to install unknown apps in Android settings, then try again.");
        }

        string path = Path.Combine(_cacheDirectory, $"folium-update-{release.Version}.apk");
        try
        {
            await DownloadAsync(release, path, progress, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new UpdateProgress(1, CanCancel: false));
            ApkVerifier.Verify(_context, path);
            await PackageSession.CommitAsync(_context, path, cancellationToken).ConfigureAwait(false);
        }
        catch (Java.Lang.Exception exception)
        {
            TryDelete(path);
            throw new InvalidOperationException("Android could not install the update.", exception);
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private async Task DownloadAsync(ReleaseInfo release, string path, IProgress<UpdateProgress> progress, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(release.ApkUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        if (!string.Equals(response.RequestMessage?.RequestUri?.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            throw new HttpRequestException("The APK download redirected to a non-HTTPS URL.");
        }

        long? size = response.Content.Headers.ContentLength ?? (release.SizeBytes > 0 ? release.SizeBytes : null);
        Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            FileStream target = new(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await using (target.ConfigureAwait(false))
            {
                byte[] buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    total += read;
                    if (size is > 0)
                    {
                        progress.Report(new UpdateProgress(Math.Min(1, (double)total / size.Value), CanCancel: true));
                    }
                }

                await target.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (size is > 0 && total != size.Value)
                {
                    throw new IOException("The downloaded APK size does not match the response.");
                }

                progress.Report(new UpdateProgress(1, CanCancel: true));
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
