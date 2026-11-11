using Folium.Views;

namespace Folium.Tests;

public sealed class CoverPlaceholderTests
{
    private static readonly Guid SampleId = new(0xaaaaaaaa, 0xbbbb, 0xcccc, 0xdd, 0xdd, 0xee, 0xee, 0xee, 0xee, 0xee, 0xee);
    private static readonly string[] Tones = ["sage", "sand", "clay", "mist", "heather", "stone"];

    [Fact]
    public void ToneStaysWithTheBookId()
    {
        Assert.Equal(CoverPlaceholder.Tone(SampleId), CoverPlaceholder.Tone(SampleId));
        Assert.Contains(CoverPlaceholder.Tone(SampleId), Tones, StringComparer.Ordinal);
    }

    [Fact]
    public void InitialSkipsALeadingTheAndKeepsTheFirstLetter()
    {
        Assert.Equal("M", CoverPlaceholder.Initial("The Middlemarch"));
        Assert.Equal("D", CoverPlaceholder.Initial("Dune"));
        Assert.Equal("", CoverPlaceholder.Initial("  "));
    }

    [Fact]
    public void NarrowCoversShowOnlyTheLetter()
    {
        Assert.True(CoverPlaceholder.ShowsLetterOnly(48));
        Assert.False(CoverPlaceholder.ShowsLetterOnly(117));
    }
}
