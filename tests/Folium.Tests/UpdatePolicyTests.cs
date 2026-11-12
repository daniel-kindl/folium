using Folium.Updates;

namespace Folium.Tests;

public sealed class UpdatePolicyTests
{
    [Fact]
    public void OffersNewerVersionWhenCheckIsDue()
    {
        UpdateDecision decision = UpdatePolicy.Decide(
            settings: new UpdateSettings(), currentVersion: "0.1.0", now: new DateTimeOffset(year: 2026, month: 11, day: 12, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero), lastCheck: null, skippedVersion: null,
            new ReleaseInfo(new SemVersion(0, 2, 0), new Uri("https://example.test/app.apk"), 10, "notes", new Uri("https://example.test/release")));

        Assert.Equal(UpdateDecision.Offer, decision);
    }

    [Theory]
    [InlineData("0.1.0", null)]
    [InlineData("0.0.9", null)]
    [InlineData("0.2.0", "0.2.0")]
    public void DoesNotOfferEqualOlderOrSkippedRelease(string candidate, string? skipped)
    {
        UpdateDecision decision = UpdatePolicy.Decide(
            settings: new UpdateSettings(), currentVersion: "0.1.0", now: new DateTimeOffset(year: 2026, month: 11, day: 12, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero), lastCheck: null, skippedVersion: skipped,
            new ReleaseInfo(SemVersion.Parse(candidate), new Uri("https://example.test/app.apk"), 10, "notes", new Uri("https://example.test/release")));

        Assert.Equal(UpdateDecision.None, decision);
    }

    [Fact]
    public void DisabledOrNotDueDoesNotCheck()
    {
        DateTimeOffset now = new(year: 2026, month: 11, day: 12, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
        Assert.Equal(UpdateDecision.Disabled, UpdatePolicy.Decide(settings: new UpdateSettings(ChecksEnabled: false), currentVersion: "0.1.0", now: now, lastCheck: null, skippedVersion: null, release: null));
        Assert.Equal(UpdateDecision.NotDue, UpdatePolicy.Decide(settings: new UpdateSettings(), currentVersion: "0.1.0", now: now, lastCheck: now.AddHours(-1), skippedVersion: null, release: null));
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
    }

    [Fact]
    public void OffersVersionNewerThanTheSkippedVersion()
    {
        ReleaseInfo release = new(SemVersion.Parse("0.3.0"), new Uri("https://example.test/app.apk"), 10, "notes", new Uri("https://example.test/release"));
        UpdateDecision decision = UpdatePolicy.Decide(settings: new UpdateSettings(), currentVersion: "0.1.0", now: DateTimeOffset.UtcNow,
            lastCheck: null, skippedVersion: "0.2.0", release: release);
        Assert.Equal(UpdateDecision.Offer, decision);
    }

    [Fact]
    public void OffersAfterTwentyFourHours()
    {
        DateTimeOffset now = new(year: 2026, month: 11, day: 12, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
        ReleaseInfo release = new(SemVersion.Parse("0.2.0"), new Uri("https://example.test/app.apk"), 10, "notes", new Uri("https://example.test/release"));

        UpdateDecision decision = UpdatePolicy.Decide(settings: new UpdateSettings(), currentVersion: "0.1.0",
            now: now, lastCheck: now.AddHours(-24), skippedVersion: null, release: release);

        Assert.Equal(UpdateDecision.Offer, decision);
    }
}
