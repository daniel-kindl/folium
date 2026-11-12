using Folium.Updates;

namespace Folium.Tests;

public sealed class ApkUpdateVerifierTests
{
    private static readonly byte[] Certificate = [1, 2, 3, 4];

    [Fact]
    public void RequiresArchiveAndInstalledMetadata()
    {
        ApkMetadata metadata = Metadata("dev.danielkindl.folium", 100, Certificate);

        _ = Assert.Throws<ArgumentNullException>(() => ApkUpdateVerifier.Compare(null!, metadata));
        _ = Assert.Throws<ArgumentNullException>(() => ApkUpdateVerifier.Compare(metadata, null!));
    }

    [Fact]
    public void AcceptsMatchingPackageWithNewerVersionAndSigner()
    {
        ApkMetadata installed = Metadata("dev.danielkindl.folium", 100, Certificate);
        ApkMetadata archive = Metadata("dev.danielkindl.folium", 101, Certificate);

        Assert.Equal(ApkVerificationFailure.None, ApkUpdateVerifier.Compare(archive, installed));
    }

    [Fact]
    public void RejectsDifferentPackageName()
    {
        ApkMetadata installed = Metadata("dev.danielkindl.folium", 100, Certificate);
        ApkMetadata archive = Metadata("dev.other.app", 101, Certificate);

        Assert.Equal(ApkVerificationFailure.PackageNameMismatch, ApkUpdateVerifier.Compare(archive, installed));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(100)]
    public void RejectsAnArchiveThatIsNotNewer(long archiveVersionCode)
    {
        ApkMetadata installed = Metadata("dev.danielkindl.folium", 100, Certificate);
        ApkMetadata archive = Metadata("dev.danielkindl.folium", archiveVersionCode, Certificate);

        Assert.Equal(ApkVerificationFailure.VersionNotNewer, ApkUpdateVerifier.Compare(archive, installed));
    }

    [Fact]
    public void RejectsDifferentSigningCertificate()
    {
        ApkMetadata installed = Metadata("dev.danielkindl.folium", 100, [1, 2, 3]);
        ApkMetadata archive = Metadata("dev.danielkindl.folium", 101, [1, 2, 5]);

        Assert.Equal(ApkVerificationFailure.SignatureMismatch, ApkUpdateVerifier.Compare(archive, installed));
    }

    [Theory]
    [MemberData(nameof(InvalidSignerSets))]
    public void RejectsZeroOrMultipleSigners(IReadOnlyList<byte[]> archiveSigners, IReadOnlyList<byte[]> installedSigners)
    {
        ApkMetadata installed = new("dev.danielkindl.folium", 100, installedSigners);
        ApkMetadata archive = new("dev.danielkindl.folium", 101, archiveSigners);

        Assert.Equal(ApkVerificationFailure.SignatureMismatch, ApkUpdateVerifier.Compare(archive, installed));
    }

    public static TheoryData<IReadOnlyList<byte[]?>, IReadOnlyList<byte[]?>> InvalidSignerSets => new()
    {
        { Array.Empty<byte[]?>(), new byte[]?[] { Certificate } },
        { new byte[]?[] { Certificate }, Array.Empty<byte[]?>() },
        { new byte[]?[] { Certificate, Certificate }, new byte[]?[] { Certificate } },
        { new byte[]?[] { Certificate }, new byte[]?[] { Certificate, Certificate } },
    };

    private static ApkMetadata Metadata(string packageName, long versionCode, params byte[][] signers)
    {
        return new ApkMetadata(packageName, versionCode, signers);
    }
}
