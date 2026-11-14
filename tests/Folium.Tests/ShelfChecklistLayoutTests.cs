using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class ShelfChecklistLayoutTests
{
    [Fact]
    public async Task LongChecklistScrollsAndKeepsActionsVisible()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            MutableStore store = new();
            PersonalLibrary library = new(store);
            for (int index = 1; index <= 20; index++)
            {
                _ = library.CreateShelf("Shelf " + index.ToString(CultureInfo.InvariantCulture));
            }

            Book book = library.Add("Dune", "Frank Herbert");
            LibraryViewModel viewModel = LibraryViewModel.Open(store);
            viewModel.OpenCommand.Execute(book.Id);
            viewModel.OpenShelfChecklistCommand.Execute(parameter: null);
            MainView view = new() { DataContext = viewModel };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            window.UpdateLayout();

            Button save = view.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Save shelves"));
            Button cancel = view.GetVisualDescendants().OfType<Button>().Single(button =>
                Equals(button.Content, "Cancel") && button.IsEffectivelyVisible);
            AssertControlFits(view, save);
            AssertControlFits(view, cancel);

            ScrollViewer checklist = view.GetVisualDescendants().OfType<ScrollViewer>().Single(scroll =>
                scroll.GetVisualDescendants().OfType<CheckBox>().Take(21).Count() == 20);
            double scrollableHeight = checklist.Extent.Height - checklist.Viewport.Height;
            Assert.True(scrollableHeight > 0);
            checklist.Offset = new Vector(0, scrollableHeight);
            window.UpdateLayout();

            CheckBox lastShelf = view.GetVisualDescendants().OfType<CheckBox>().Single(checkBox =>
                string.Equals(Avalonia.Automation.AutomationProperties.GetName(checkBox), "Shelf 20", StringComparison.Ordinal));
            Assert.True(lastShelf.IsEffectivelyVisible);
            AssertControlFits(view, lastShelf);
            AssertControlFits(view, save);
            AssertControlFits(view, cancel);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private static void AssertControlFits(MainView view, Control control)
    {
        Rect bounds = new(control.TranslatePoint(default, view)!.Value, control.Bounds.Size);
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= view.Bounds.Width && bounds.Bottom <= view.Bounds.Height,
            "The control must fit inside the app window.");
    }

    private sealed class MutableStore : IBookStore
    {
        private LibrarySnapshot _snapshot = new([], []);

        public LibrarySnapshot Read()
        {
            return _snapshot;
        }

        public void Replace(LibrarySnapshot snapshot)
        {
            _snapshot = snapshot;
        }
    }
}
