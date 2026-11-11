using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class FocusedFieldTests
{
    [Fact]
    public async Task DarkControlsKeepAVisibleFieldAndCheckBox()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            TextBox field = new() { Text = "Dune" };
            CheckBox consent = new() { Content = "Keep cover" };
            UserControl view = new() { Content = new StackPanel { Children = { field, consent } } };
            Window window = new()
            {
                Content = view,
                RequestedThemeVariant = ThemeVariant.Dark,
                Width = 400,
                Height = 400,
            };
            window.Show();
            Assert.True(field.Focus());
            window.UpdateLayout();

            Border fieldBorder = field.GetVisualDescendants().OfType<Border>().Single(border =>
                string.Equals(border.Name, "PART_BorderElement", StringComparison.Ordinal));
            AssertColor("#202922", fieldBorder.Background);
            AssertColor("#F1F3EC", field.Foreground);
            Border box = consent.GetVisualDescendants().OfType<Border>().Single(border =>
                string.Equals(border.Name, "NormalRectangle", StringComparison.Ordinal));
            AssertColor("#F1F3EC", box.BorderBrush);
            Assert.True(box.Bounds.Width >= 20);
            Assert.True(box.Bounds.Height >= 20);
            consent.IsChecked = true;
            window.UpdateLayout();
            AssertColor("#AED4B3", box.Background);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private static void AssertColor(string expectedColor, IBrush? actual)
    {
        SolidColorBrush brush = Assert.IsType<SolidColorBrush>(actual);
        Assert.Equal(Color.Parse(expectedColor), brush.Color);
    }
}
