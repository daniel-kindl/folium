using System.Text.Json;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;

namespace Folium.Tests;

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
    public async Task SharedTextStyleUpdatesWhenWindowThemeChanges()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            TextBlock text = new() { Text = "Theme" };
            Window window = new()
            {
                Content = text,
                RequestedThemeVariant = ThemeVariant.Light,
                Width = 200,
                Height = 100,
            };
            window.Show();
            window.UpdateLayout();
            AssertBrushColor("#202922", text.Foreground);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.UpdateLayout();
            AssertBrushColor("#F1F3EC", text.Foreground);
            window.Close();
        }, TestContext.Current.CancellationToken);
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

    private static void AssertBrushColor(string expectedColor, IBrush? actual)
    {
        SolidColorBrush brush = Assert.IsType<SolidColorBrush>(actual);
        Assert.Equal(Color.Parse(expectedColor), brush.Color);
    }

    private static string[] Roles()
    {
        return ["background", "surface", "raised", "text", "muted", "primary", "onPrimary", "secondary", "accent", "border", "success", "warning", "error", "info"];
    }

    private static string TokenColor(string theme, string role)
    {
        string path = Path.Combine(RepositoryRoot(), "docs", "design", "tokens.json");
        using JsonDocument tokens = JsonDocument.Parse(File.ReadAllText(path));
        string color = tokens.RootElement.GetProperty("color").GetProperty(theme).GetProperty(role).GetString()!;
        return Color.Parse(color).ToString();
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
