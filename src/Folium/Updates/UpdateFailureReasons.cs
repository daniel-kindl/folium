namespace Folium.Updates;

/// <summary>Maps platform install failures to user-facing update reasons.</summary>
public static class UpdateFailureReasons
{
    /// <summary>Maps a platform install failure to a stable update failure reason.</summary>
    /// <param name="failure">The platform failure class.</param>
    /// <returns>The reason that the update view must show.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The failure class is not supported.</exception>
    public static UpdateFailureReason FromPackageInstallFailure(PackageInstallFailureKind failure)
    {
        return failure switch
        {
            PackageInstallFailureKind.InvalidApk => UpdateFailureReason.FileCheck,
            PackageInstallFailureKind.PackageConflict => UpdateFailureReason.SignatureMismatch,
            PackageInstallFailureKind.DeviceIncompatible => UpdateFailureReason.Install,
            PackageInstallFailureKind.Other => UpdateFailureReason.Install,
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "Unknown package install failure."),
        };
    }
}
