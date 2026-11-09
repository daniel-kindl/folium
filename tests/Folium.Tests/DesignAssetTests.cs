using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class DesignAssetTests
{
    private static readonly string[] FontFaces = ["Regular", "Medium", "SemiBold"];
    private static readonly string[] Icons = ["FoliumLibraryIcon", "FoliumBookIcon", "FoliumAddIcon", "FoliumEditIcon", "FoliumDeleteIcon", "FoliumSearchIcon", "FoliumToReadIcon", "FoliumReadingIcon", "FoliumReadIcon", "FoliumTimeIcon", "FoliumErrorIcon", "FoliumBackIcon", "FoliumOverflowIcon", "FoliumEmptyIcon"];

    [Fact]
    public async Task AppEmbedsNotoSansFacesAndLucideGeometry()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            foreach (string face in FontFaces)
            {
                using Stream font = AssetLoader.Open(new Uri($"avares://Folium/Assets/Fonts/NotoSans-{face}.ttf"));
                Assert.True(font.Length > 0);
            }

            foreach (string icon in Icons)
            {
                Assert.True(Application.Current!.Resources.TryGetResource(icon, ThemeVariant.Light, out object? value), icon);
                _ = Assert.IsType<GeometryGroup>(value);
            }

            TextBlock text = new() { Text = "Folium" };
            Window window = new() { Content = text, Width = 160, Height = 64 };
            window.Show();
            window.UpdateLayout();
            Assert.Contains("Noto Sans", text.FontFamily!.ToString(), StringComparison.Ordinal);
            AssertNotoSansCzechGlyphs(text.FontFamily!);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void DesignAssetHashesMatchTheirSourceRecord()
    {
        string root = RepositoryRoot();
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "design", "assets.json")));
        foreach (JsonElement group in record.RootElement.EnumerateObject().Select(property => property.Value))
        {
            string license = group.GetProperty("licenseFile").GetString()!;
            Assert.True(File.Exists(Path.Combine(root, license)), license);
            foreach (JsonElement file in group.GetProperty("files").EnumerateArray())
            {
                string path = file.GetProperty("path").GetString()!;
                string expected = file.GetProperty("sha256").GetString()!.ToUpperInvariant();
                byte[] bytes = File.ReadAllBytes(Path.Combine(root, path));
                string actual = Convert.ToHexString(SHA256.HashData(bytes));
                Assert.Equal(expected, actual);
            }
        }
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Folium.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The repository root was not found from the test output directory.");
    }

    private static void AssertNotoSansCzechGlyphs(FontFamily family)
    {
        foreach (FontWeight weight in new[] { FontWeight.Normal, FontWeight.Medium, FontWeight.SemiBold })
        {
            Typeface typeface = new(family, FontStyle.Normal, weight, FontStretch.Normal);
            bool found = FontManager.Current.TryGetGlyphTypeface(typeface, out GlyphTypeface? glyphTypeface);
            Assert.True(found);
            GlyphTypeface resolved = Assert.IsType<GlyphTypeface>(glyphTypeface);
            foreach (int codepoint in "Příliš žluťoučký kůň úpěl ďábelské ódy Ωμέγα Книга")
            {
                Assert.True(resolved.CharacterToGlyphMap.TryGetGlyph(codepoint, out _), $"U+{codepoint:X4} at weight {weight}");
            }
        }
    }
}
