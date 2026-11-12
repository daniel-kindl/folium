using Folium.Updates;

namespace Folium.Tests;

public sealed class UpdatePolicyTests
{
    [Fact]
    public void OffersNewerVersionWhenCheckIsDue()
    {
        ReleaseInfo release = new(new SemVersion(0, 2, 0), new Uri("https://example.test/app.apk"), 10, "notes", new Uri("https://example.test/release"));

        Assert.True(UpdatePolicy.IsCheckDue(new UpdateSettings(), DateTimeOffset.UnixEpoch, force: false));
        Assert.True(UpdatePolicy.ShouldOffer(release, "0.1.0", skippedVersion: null));
    }

    [Theory]
    [InlineData("0.1.0", null)]
    [InlineData("0.0.9", null)]
    [InlineData("0.2.0", "0.2.0")]
    public void DoesNotOfferEqualOlderOrSkippedRelease(string candidate, string? skipped)
    {
        ReleaseInfo release = new(SemVersion.Parse(candidate), new Uri("https://example.test/app.apk"), 10, "notes", new Uri("https://example.test/release"));

        Assert.False(UpdatePolicy.ShouldOffer(release, "0.1.0", skipped));
    }

    [Fact]
    public void DisabledOrNotDueDoesNotCheck()
    {
        DateTimeOffset now = new(year: 2026, month: 11, day: 12, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
        Assert.False(UpdatePolicy.IsCheckDue(new UpdateSettings(ChecksEnabled: false), now, force: false));
        Assert.False(UpdatePolicy.IsCheckDue(new UpdateSettings(LastCheck: now.AddHours(-1)), now, force: false));
        Assert.True(UpdatePolicy.IsCheckDue(new UpdateSettings(ChecksEnabled: false), now, force: true));
    }

    [Theory]
    [InlineData("01.2.3")]
    [InlineData("1.02.3")]
    [InlineData("1.2.03")]
    [InlineData("1.x.3")]
    [InlineData("999999999999999999.1.0")]
    [InlineData("-1.0.0")]
    public void SemVersionRejectsLeadingZeroComponents(string value)
    {
        _ = Assert.Throws<FormatException>(() => SemVersion.Parse(value).ToString());
    }

    [Fact]
    public void ComparesVersionComponentsNumerically()
    {
        Assert.True(SemVersion.Parse("1.10.0") > SemVersion.Parse("1.9.9"));
        Assert.True(SemVersion.Parse("2.0.0") > SemVersion.Parse("1.99.99"));
        Assert.True(SemVersion.Parse("1.2.4") > SemVersion.Parse("1.2.3"));
        Assert.False(SemVersion.Parse("1.2.3") > SemVersion.Parse("1.2.3"));
        Assert.True(SemVersion.Parse("1.2.3") >= SemVersion.Parse("1.2.3"));
    }

    [Fact]
    public void RejectsNullVersionAndUpdatePreferences()
    {
        _ = Assert.Throws<ArgumentNullException>(() => SemVersion.Parse(null!));
        _ = Assert.Throws<ArgumentNullException>(() => UpdatePolicy.IsCheckDue(null!, DateTimeOffset.UnixEpoch));
        _ = Assert.Throws<ArgumentException>(() => UpdatePolicy.ShouldOffer(release: null, currentVersion: " ", skippedVersion: null));
    }

    [Fact]
    public void InvalidVersionTextUsesTheVersionFormatError()
    {
        FormatException missingComponent = Assert.Throws<FormatException>(() => SemVersion.Parse("1.2"));
        FormatException invalidComponent = Assert.Throws<FormatException>(() => SemVersion.Parse("1.2.x"));

        Assert.Equal("The version must use X.Y.Z format.", missingComponent.Message);
        Assert.Equal("The version must use X.Y.Z format.", invalidComponent.Message);
    }

    [Fact]
    public void OffersVersionNewerThanTheSkippedVersion()
    {
        ReleaseInfo release = new(SemVersion.Parse("0.3.0"), new Uri("https://example.test/app.apk"), 10, "notes", new Uri("https://example.test/release"));
        Assert.True(UpdatePolicy.ShouldOffer(release, "0.1.0", skippedVersion: "0.2.0"));
    }

    [Fact]
    public void OffersAfterTwentyFourHours()
    {
        DateTimeOffset now = new(year: 2026, month: 11, day: 12, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
        ReleaseInfo release = new(SemVersion.Parse("0.2.0"), new Uri("https://example.test/app.apk"), 10, "notes", new Uri("https://example.test/release"));

        Assert.True(UpdatePolicy.IsCheckDue(new UpdateSettings(LastCheck: now.AddHours(-24)), now, force: false));
        Assert.True(UpdatePolicy.ShouldOffer(release, "0.1.0", skippedVersion: null));
    }
}
