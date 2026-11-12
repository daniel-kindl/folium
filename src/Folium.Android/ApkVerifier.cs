using Android.Content;
using Android.Content.PM;
using Folium.Updates;

namespace Folium.Android;

/// <summary>Checks an APK's package, version and signing certificate.</summary>
internal static class ApkVerifier
{
    public static void Verify(Context context, string path)
    {
        ArgumentNullException.ThrowIfNull(context);
        string packageName = context.PackageName ?? throw new UpdateFailureException(UpdateFailureReason.Install, "The installed Folium package is not available.");
        PackageManager manager = context.PackageManager ?? throw new UpdateFailureException(UpdateFailureReason.Install, "Android package manager is not available.");
        const PackageInfoFlags SIGNING_FLAGS = PackageInfoFlags.SigningCertificates;
        PackageInfo archive = manager.GetPackageArchiveInfo(path, SIGNING_FLAGS)
            ?? throw new UpdateFailureException(UpdateFailureReason.FileCheck, "The downloaded file is not a valid APK.");
        PackageInfo installed = manager.GetPackageInfo(packageName, SIGNING_FLAGS)
            ?? throw new UpdateFailureException(UpdateFailureReason.Install, "The installed Folium package is not available.");
        ApkMetadata archiveMetadata = new(archive.PackageName, archive.LongVersionCode, GetSigners(archive));
        ApkMetadata installedMetadata = new(installed.PackageName, installed.LongVersionCode, GetSigners(installed));
        switch (ApkUpdateVerifier.Compare(archiveMetadata, installedMetadata))
        {
            case ApkVerificationFailure.None:
                return;
            case ApkVerificationFailure.PackageNameMismatch:
                throw new UpdateFailureException(UpdateFailureReason.FileCheck, "The APK package name does not match Folium.");
            case ApkVerificationFailure.VersionNotNewer:
                throw new UpdateFailureException(UpdateFailureReason.FileCheck, "The APK version is not newer than the installed version.");
            case ApkVerificationFailure.SignatureMismatch:
                throw new UpdateFailureException(UpdateFailureReason.SignatureMismatch, "The APK signing certificate does not match this installation.");
            default:
                throw new InvalidOperationException("APK verification returned an unknown result.");
        }
    }

    private static byte[]?[] GetSigners(PackageInfo info)
    {
        Signature[]? signatures = info.SigningInfo?.GetApkContentsSigners();
        return signatures is null ? [] : [.. signatures.Select(signature => signature?.ToByteArray())];
    }
}
