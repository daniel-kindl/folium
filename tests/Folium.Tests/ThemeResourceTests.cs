using System.Text.Json;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Folium.Views;

namespace Folium.Tests;

[CollectionDefinition("Avalonia UI", DisableParallelization = true)]
[Collection("Avalonia UI")]
public sealed class ThemeResourceTests
{
    [Fact]
    public async Task SemanticBrushesMatchCanonicalTokensInBothThemes()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            foreach ((ThemeVariant variant, string theme) in new[]
            {
                (ThemeVariant.Light, "light"),
                (ThemeVariant.Dark, "dark"),
            })
            {
                foreach (string role in Roles())
                {
                    string key = "Folium" + char.ToUpperInvariant(role[0]) + role[1..] + "Brush";
                    Assert.True(Application.Current!.Resources.TryGetResource(key, variant, out object? value), key);
                    SolidColorBrush brush = Assert.IsType<SolidColorBrush>(value);
                    Assert.Equal(TokenColor(theme, role), brush.Color.ToString());
                }
            }
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RealViewsUseThemeBackgroundAndTextInBothThemes()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            foreach (UserControl view in new UserControl[] { new MainView(), new SearchView() })
            {
                Window window = new()
                {
                    Content = view,
                    RequestedThemeVariant = ThemeVariant.Light,
                    Width = 400,
                    Height = 850,
                };
                window.Show();
                window.UpdateLayout();
                TextBlock text = view.GetVisualDescendants().OfType<TextBlock>().First();
                AssertBrushColor(TokenColor("light", "background"), view.Background);
                AssertBrushColor(TokenColor("light", "text"), view.Foreground);
                AssertBrushColor(TokenColor("light", "text"), text.Foreground);

                window.RequestedThemeVariant = ThemeVariant.Dark;
                window.UpdateLayout();
                AssertBrushColor(TokenColor("dark", "background"), view.Background);
                AssertBrushColor(TokenColor("dark", "text"), view.Foreground);
                AssertBrushColor(TokenColor("dark", "text"), text.Foreground);
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SharedStylesApplyTypographyAndControlStates()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            TextBlock title = new() { Text = "Library", Classes = { "folium-title" } };
            Button primary = new() { Content = "Add", Classes = { "folium-primary" } };
            Button filter = new() { Content = "Finished", Classes = { "folium-filter" } };
            Button disabled = new() { Content = "Unavailable", IsEnabled = false };
            TextBox field = new() { Text = "Dune" };
            CheckBox consent = new() { Content = "Keep cover" };
            UserControl view = new()
            {
                Content = new StackPanel { Children = { title, primary, filter, disabled, field, consent } },
            };
            Window window = new()
            {
                Content = view,
                RequestedThemeVariant = ThemeVariant.Light,
                Width = 400,
                Height = 400,
            };
            window.Show();
            window.UpdateLayout();

            Assert.Equal(28, title.FontSize);
            Assert.Equal(36, title.LineHeight);
            Assert.Equal(FontWeight.SemiBold, title.FontWeight);
            Assert.Equal(16, filter.FontSize);
            AssertBrushColor("#315C43", primary.Background);
            AssertBrushColor("#FFFFFF", primary.Foreground);
            AssertBrushColor("#747D75", disabled.Foreground);
            Assert.Equal(1, disabled.Opacity);
            Assert.True(primary.Bounds.Height >= 48);
            Assert.True(consent.Bounds.Height >= 48);
            Assert.Equal(new CornerRadius(8), primary.CornerRadius);
            Assert.True(field.Focus());
            Assert.Equal(new Thickness(2), field.BorderThickness);

            App app = Assert.IsType<App>(Application.Current);
            AssertTypographyAtScale(app, title, filter, field, 1.5);
            AssertTypographyAtScale(app, title, filter, field, 2);
            AssertTypographyAtScale(app, title, filter, field, 1);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.UpdateLayout();
            AssertBrushColor("#AED4B3", primary.Background);
            AssertBrushColor("#173823", primary.Foreground);
            AssertBrushColor("#8C978E", disabled.Foreground);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private static void AssertTypographyAtScale(App app, TextBlock title, Button filter, TextBox field, double scale)
    {
        app.ApplySystemFontScale(scale);
        Assert.Equal(28 * scale, title.FontSize);
        Assert.Equal(36 * scale, title.LineHeight);
        Assert.Equal(16 * scale, filter.FontSize);
        Assert.Equal(16 * scale, field.FontSize);
    }

    [Fact]
    public void MainAndSearchViewsUseSharedColorResources()
    {
        string views = Path.Combine(RepositoryRoot(), "src", "Folium", "Views");
        foreach (string name in new[] { "MainView.axaml", "SearchView.axaml" })
        {
            XDocument view = XDocument.Load(Path.Combine(views, name));
            Assert.DoesNotContain(view.Descendants().Attributes(), attribute => attribute.Value.StartsWith('#'));
        }

        string stylePath = Path.Combine(RepositoryRoot(), "src", "Folium", "Styles", "ThemeStyles.axaml");
        XDocument styles = XDocument.Load(stylePath);
        foreach (string resource in new[] { "FoliumBackgroundBrush", "FoliumTextBrush" })
        {
            Assert.Contains(styles.Descendants().Attributes(), attribute =>
                attribute.Value.Contains("{DynamicResource " + resource + "}", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SharedDimensionResourcesMatchCanonicalTokens()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            foreach ((string key, string[] tokenPath) in DimensionResources())
            {
                Assert.True(Application.Current!.Resources.TryGetResource(key, ThemeVariant.Light, out object? value), key);
                Assert.Equal(TokenNumber(tokenPath), Assert.IsType<double>(value));
            }
        }, TestContext.Current.CancellationToken);
    }

    private static void AssertBrushColor(string expectedColor, IBrush? actual)
    {
        SolidColorBrush brush = Assert.IsType<SolidColorBrush>(actual);
        Assert.Equal(Color.Parse(expectedColor), brush.Color);
    }

    private static string[] Roles()
    {
        return ["background", "surface", "raised", "text", "muted", "primary", "onPrimary", "secondary", "accent", "border", "success", "warning", "error", "info", "statusToRead", "statusReading", "statusFinished", "selectedContainer", "focus", "divider", "disabledText", "inverseSurface", "onInverse", "inversePrimary", "scrim"];
    }

    private static string TokenColor(string theme, string role)
    {
        string path = Path.Combine(RepositoryRoot(), "docs", "design", "tokens.json");
        using JsonDocument tokens = JsonDocument.Parse(File.ReadAllText(path));
        string color = tokens.RootElement.GetProperty("color").GetProperty(theme).GetProperty(role).GetString()!;
        return Color.Parse(color).ToString();
    }

    private static double TokenNumber(string[] path)
    {
        string tokenPath = Path.Combine(RepositoryRoot(), "docs", "design", "tokens.json");
        using JsonDocument tokens = JsonDocument.Parse(File.ReadAllText(tokenPath));
        JsonElement value = tokens.RootElement;
        foreach (string segment in path)
        {
            value = value.GetProperty(segment);
        }

        return value.GetDouble();
    }

    private static (string Key, string[] TokenPath)[] DimensionResources()
    {
        return [
            ("FoliumCaptionFontSize", ["typography", "styles", "caption", "fontSize"]),
            ("FoliumCaptionLineHeight", ["typography", "styles", "caption", "lineHeight"]),
            ("FoliumBodyFontSize", ["typography", "styles", "body", "fontSize"]),
            ("FoliumBodyLineHeight", ["typography", "styles", "body", "lineHeight"]),
            ("FoliumLabelFontSize", ["typography", "styles", "label", "fontSize"]),
            ("FoliumLabelLineHeight", ["typography", "styles", "label", "lineHeight"]),
            ("FoliumSectionFontSize", ["typography", "styles", "section", "fontSize"]),
            ("FoliumSectionLineHeight", ["typography", "styles", "section", "lineHeight"]),
            ("FoliumTitleFontSize", ["typography", "styles", "title", "fontSize"]),
            ("FoliumTitleLineHeight", ["typography", "styles", "title", "lineHeight"]),
            ("FoliumLabelSmallFontSize", ["typography", "styles", "labelSmall", "fontSize"]),
            ("FoliumLabelSmallLineHeight", ["typography", "styles", "labelSmall", "lineHeight"]),
            ("FoliumSpacingXs", ["spacing", "xs"]),
            ("FoliumSpacingSm", ["spacing", "sm"]),
            ("FoliumSpacingMd", ["spacing", "md"]),
            ("FoliumSpacingLg", ["spacing", "lg"]),
            ("FoliumSpacingXl", ["spacing", "xl"]),
            ("FoliumSpacingXxl", ["spacing", "xxl"]),
            ("FoliumGutter", ["layout", "gutter"]),
            ("FoliumGutterWide", ["layout", "gutterWide"]),
            ("FoliumTouchTargetMin", ["layout", "touchTargetMin"]),
            ("FoliumInteractiveGapMin", ["layout", "interactiveGapMin"]),
            ("FoliumControlRadius", ["radius", "control"]),
            ("FoliumGroupRadius", ["radius", "group"]),
            ("FoliumCoverRadius", ["radius", "cover"]),
            ("FoliumFocusWidth", ["border", "focusWidth"]),
            ("FoliumFocusGap", ["border", "focusGap"]),
            ("FoliumCoverLibraryRowWidth", ["cover", "libraryRow", "width"]),
            ("FoliumCoverLibraryRowHeight", ["cover", "libraryRow", "height"]),
            ("FoliumCoverLibraryGridWidth", ["cover", "libraryGrid", "width"]),
            ("FoliumCoverLibraryGridHeight", ["cover", "libraryGrid", "height"]),
            ("FoliumCoverBookDetailsWidth", ["cover", "bookDetails", "width"]),
            ("FoliumCoverBookDetailsHeight", ["cover", "bookDetails", "height"]),
            ("FoliumCoverEditionPreviewWidth", ["cover", "editionPreview", "width"]),
            ("FoliumCoverEditionPreviewHeight", ["cover", "editionPreview", "height"]),
            ("FoliumCoverImportDraftWidth", ["cover", "importDraft", "width"]),
            ("FoliumCoverImportDraftHeight", ["cover", "importDraft", "height"]),
            ("FoliumCoverRestoreRowWidth", ["cover", "restoreRow", "width"]),
            ("FoliumCoverRestoreRowHeight", ["cover", "restoreRow", "height"]),
            ("FoliumInlineIconSize", ["icon", "size", "inline"]),
            ("FoliumActionIconSize", ["icon", "size", "action"]),
            ("FoliumEmptyIconSize", ["icon", "size", "empty"]),
        ];
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
}
