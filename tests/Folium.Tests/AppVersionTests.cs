using Folium.Versioning;

namespace Folium.Tests;

public sealed class AppVersionTests
{
    [Theory]
    [InlineData("0.1.0", 0, 1, 0, 100)]
    [InlineData("1.2.3", 1, 2, 3, 10203)]
    [InlineData("0.0.1", 0, 0, 1, 1)]
    [InlineData("0.99.99", 0, 99, 99, 9999)]
    [InlineData("1.0.0", 1, 0, 0, 10000)]
    [InlineData("1.0.1", 1, 0, 1, 10001)]
    [InlineData("2.0.0", 2, 0, 0, 20000)]
    [InlineData("10.20.30", 10, 20, 30, 102030)]
    [InlineData("209999.99.99", 209999, 99, 99, 2099999999)]
    public void ParseAcceptsAStrictVersionAndComputesTheCode(
        string text,
        int major,
        int minor,
        int patch,
        int versionCode)
    {
        AppVersion version = AppVersion.Parse(text);

        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(text, version.DisplayVersion);
        Assert.Equal(versionCode, version.VersionCode);
    }

    [Fact]
    public void VersionCodeIncreasesAcrossDocumentedBoundaries()
    {
        int[] codes =
        [
            AppVersion.Parse("0.0.1").VersionCode,
            AppVersion.Parse("0.0.2").VersionCode,
            AppVersion.Parse("0.1.0").VersionCode,
            AppVersion.Parse("0.99.99").VersionCode,
            AppVersion.Parse("1.0.0").VersionCode,
            AppVersion.Parse("1.0.1").VersionCode,
            AppVersion.Parse("1.99.99").VersionCode,
            AppVersion.Parse("2.0.0").VersionCode,
        ];

        Assert.Equal([1, 2, 100, 9999, 10000, 10001, 19999, 20000], codes);
        Assert.Equal(codes.Order(), codes);
        Assert.True(AppVersion.Parse("0.99.99").VersionCode < AppVersion.Parse("1.0.0").VersionCode);
    }

    [Fact]
    public void DisplayVersionEqualsTheParsedSemVer()
    {
        AppVersion version = AppVersion.Parse("1.2.3");

        Assert.Equal("1.2.3", version.DisplayVersion);
        Assert.Equal("1.2.3", version.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t1.2.3")]
    [InlineData("1.2.3\n")]
    [InlineData("1.2")]
    [InlineData("1")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.3-beta")]
    [InlineData("1.2.3-0")]
    [InlineData("1.2.3+build")]
    [InlineData("1.2.3-beta.1+build")]
    [InlineData("v1.2.3")]
    [InlineData("V1.2.3")]
    [InlineData("01.2.3")]
    [InlineData("1.02.3")]
    [InlineData("1.2.03")]
    [InlineData("1.2.3 ")]
    [InlineData("1..2")]
    [InlineData("-1.2.3")]
    [InlineData("1.-2.3")]
    [InlineData("1.2.-3")]
    [InlineData("1.2.3a")]
    [InlineData("one.two.three")]
    [InlineData("1.2.3.0")]
    public void ParseRejectsTextThatIsNotStrictSemVer(string? text)
    {
        if (text is null)
        {
            _ = Assert.Throws<ArgumentNullException>(() => AppVersion.Parse(text!));
            return;
        }

        FormatException exception = Assert.Throws<FormatException>(() => AppVersion.Parse(text));
        Assert.Contains("X.Y.Z", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1.100.0", "minor")]
    [InlineData("1.0.100", "patch")]
    [InlineData("0.100.0", "minor")]
    [InlineData("0.0.100", "patch")]
    [InlineData("1.101.0", "minor")]
    [InlineData("1.0.101", "patch")]
    [InlineData("210000.0.0", "major")]
    [InlineData("210001.0.0", "major")]
    public void ParseRejectsComponentsOutsideTheFormulaLimits(string text, string parameter)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => AppVersion.Parse(text));

        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void ConstructorRejectsAMinorComponentOf100()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new AppVersion(1, 100, 0));

        Assert.Equal("minor", exception.ParamName);
    }

    [Fact]
    public void ConstructorRejectsAPatchComponentOf100()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new AppVersion(1, 0, 100));

        Assert.Equal("patch", exception.ParamName);
    }

    [Fact]
    public void ConstructorRejectsAMajorComponentAboveTheAndroidLimit()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new AppVersion(210000, 0, 0));

        Assert.Equal("major", exception.ParamName);
    }

    [Fact]
    public void ConstructorRejectsANegativeComponent()
    {
        Assert.Equal("major", Assert.Throws<ArgumentOutOfRangeException>(() => new AppVersion(-1, 0, 0)).ParamName);
        Assert.Equal("minor", Assert.Throws<ArgumentOutOfRangeException>(() => new AppVersion(0, -1, 0)).ParamName);
        Assert.Equal("patch", Assert.Throws<ArgumentOutOfRangeException>(() => new AppVersion(0, 0, -1)).ParamName);
    }
}
