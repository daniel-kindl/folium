using Folium.Updates;

namespace Folium.Tests;

public sealed class UpdateFailureReasonTests
{
    [Theory]
    [InlineData(PackageInstallFailureKind.InvalidApk, UpdateFailureReason.FileCheck)]
    [InlineData(PackageInstallFailureKind.PackageConflict, UpdateFailureReason.SignatureMismatch)]
    [InlineData(PackageInstallFailureKind.DeviceIncompatible, UpdateFailureReason.Install)]
    [InlineData(PackageInstallFailureKind.Other, UpdateFailureReason.Install)]
    public void MapsPackageInstallerFailureToUserReason(PackageInstallFailureKind failure, UpdateFailureReason expected)
    {
        Assert.Equal(expected, UpdateFailureReasons.FromPackageInstallFailure(failure));
    }
}
