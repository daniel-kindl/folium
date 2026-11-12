using System.Security.Cryptography;
using Android.Content;
using Android.Content.PM;

namespace Folium.Android;

/// <summary>Checks an APK's package, version and signing certificate.</summary>
internal static class ApkVerifier
{
    private const string PACKAGE_ID = "dev.danielkindl.folium";

    public static void Verify(Context context, string path)
    {
        ArgumentNullException.ThrowIfNull(context);
        PackageManager manager = context.PackageManager ?? throw new InvalidOperationException("Android package manager is not available.");
        const PackageInfoFlags SIGNING_FLAGS = PackageInfoFlags.SigningCertificates;
        PackageInfo archive = manager.GetPackageArchiveInfo(path, SIGNING_FLAGS) ?? throw new InvalidOperationException("The downloaded file is not a valid APK.");
        PackageInfo installed = manager.GetPackageInfo(PACKAGE_ID, SIGNING_FLAGS) ?? throw new InvalidOperationException("The installed Folium package is not available.");
        if (!string.Equals(archive.PackageName, PACKAGE_ID, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The APK package name does not match Folium.");
        }

        if (archive.LongVersionCode <= installed.LongVersionCode)
        {
            throw new InvalidOperationException("The APK version is not newer than the installed version.");
        }

        byte[]? archiveSignature = GetSignature(archive);
        byte[]? installedSignature = GetSignature(installed);
        if (archiveSignature is null || installedSignature is null
            || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(archiveSignature), SHA256.HashData(installedSignature)))
        {
            throw new InvalidOperationException("The APK signing certificate does not match this installation.");
        }
    }

    private static byte[]? GetSignature(PackageInfo info)
    {
        Signature[]? signatures = info.SigningInfo?.GetApkContentsSigners();
        return signatures is { Length: 1 } ? signatures[0]?.ToByteArray() : null;
    }
}
