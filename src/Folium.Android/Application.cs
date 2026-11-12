using Android.Runtime;
using Avalonia.Android;
#if !FOLIUM_VERIFICATION || FOLIUM_UPDATE_VERIFICATION
using Folium.Updates;
#endif

namespace Folium.Android;

/// <summary>The Android application host for Folium.</summary>
/// <param name="javaReference">The Java object handle from the Android runtime.</param>
/// <param name="transfer">How the runtime transfers ownership of the handle.</param>
[Application]
public class Application(nint javaReference, JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<App>(javaReference, transfer)
{
#if FOLIUM_VERIFICATION
    private static readonly HttpClient VerificationHttp = new(new VerificationHttpHandler());
#endif
#if FOLIUM_UPDATE_VERIFICATION
    private static readonly HttpClient VerificationReleaseHttp = new(new VerificationHttpHandler());
    private static readonly HttpClient VerificationInstallerHttp = new(new VerificationHttpHandler());
#elif !FOLIUM_VERIFICATION
    private static readonly HttpClient ReleaseHttp = new() { Timeout = TimeSpan.FromSeconds(15) };
#endif

    /// <summary>Sets the library file path after the Android application starts.</summary>
    /// <exception cref="InvalidOperationException">The application data directory is not available.</exception>
    public override void OnCreate()
    {
        base.OnCreate();
        string? files = FilesDir?.AbsolutePath;
        if (string.IsNullOrWhiteSpace(files))
        {
            throw new InvalidOperationException("The app data directory is not available.");
        }

        string? version = PackageManager?.GetPackageInfo(PackageName!, global::Android.Content.PM.PackageInfoFlags.SigningCertificates)?.VersionName;
        App.CurrentVersion = string.IsNullOrWhiteSpace(version) ? "0.1.0" : version;
#if FOLIUM_UPDATE_VERIFICATION
        App.UpdateChecks = new UpdateCheckService(
            new VerificationReleaseSource(VerificationReleaseHttp),
            new UpdateSettingsFile(Path.Combine(files, "verification-updates.json")));
        App.UpdateInstaller = new UpdateInstaller(this, VerificationInstallerHttp);
#elif !FOLIUM_VERIFICATION
        App.UpdateChecks = new UpdateCheckService(
            new GitHubReleaseSource(ReleaseHttp),
            new UpdateSettingsFile(Path.Combine(files, "updates.json")));
        App.UpdateInstaller = new UpdateInstaller(this);
#endif

#if FOLIUM_VERIFICATION
        App.LibraryFilePath = Path.Combine(files, "verification-library.json");
        App.VerificationSearch = new Search.OpenLibrary(VerificationHttp);
#else
        App.LibraryFilePath = Path.Combine(files, "library.json");
#endif
    }
}
