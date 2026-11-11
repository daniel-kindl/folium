using System.Xml.Linq;
using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class MainViewTests : IDisposable
{
    private readonly List<string> _directories = [];

    public void Dispose()
    {
        foreach (string directory in _directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FailedStatusChangeShowsTheErrorOnTheBookScreen()
    {
        string path = NewPath();
        LibraryViewModel viewModel = LibraryViewModel.Open(new FailOnSecondWriteStore(new FileBookStore(path)));
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);

        viewModel.SetStatusCommand.Execute("finished");

        Assert.True(viewModel.ShowDetails);
        Assert.Equal("Folium could not save this status.", viewModel.SaveError);
        AssertScreenShowsSaveError("ShowDetails");
    }

    [Fact]
    public void MissingBookShowsTheErrorOnTheLibraryScreen()
    {
        string path = NewPath();
        LibraryViewModel viewModel = LibraryViewModel.Open(new MissingOnSecondWriteStore(new FileBookStore(path)));
        Add(viewModel, "Dune", "Frank Herbert");
        viewModel.OpenCommand.Execute(viewModel.Books[0].Id);

        viewModel.SetStatusCommand.Execute("reading");

        Assert.True(viewModel.ShowLibrary);
        Assert.Equal("Folium could not find that book.", viewModel.SaveError);
        AssertScreenShowsSaveError("ShowLibrary");
    }

    [Fact]
    public void LoadErrorScreenAnnouncesSaveError()
    {
        AssertScreenShowsSaveError("ShowLoadError");
    }

    private static void AssertScreenShowsSaveError(string screen)
    {
        XElement view = XElement.Load(ViewPath());
        XElement panel = view.Descendants().First(element =>
            string.Equals(Binding(element, "IsVisible"), screen, StringComparison.Ordinal));
        XElement error = panel.Descendants().FirstOrDefault(IsSaveError) ?? throw new InvalidOperationException(
            $"The {screen} screen does not show SaveError.");
        Assert.Equal("Assertive", error.Attribute("AutomationProperties.LiveSetting")?.Value);
    }

    private static bool IsSaveError(XElement element)
    {
        return string.Equals(element.Name.LocalName, "TextBlock", StringComparison.Ordinal)
            && string.Equals(Binding(element, "Text"), "SaveError", StringComparison.Ordinal)
            && string.Equals(Binding(element, "AutomationProperties.Name"), "SaveError", StringComparison.Ordinal)
            && string.Equals(Binding(element, "IsVisible"), "SaveError", StringComparison.Ordinal);
    }

    private static string? Binding(XElement element, string attributeName)
    {
        string? value = element.Attribute(attributeName)?.Value;
        if (value?.StartsWith("{Binding ", StringComparison.Ordinal) != true || !value.EndsWith('}'))
        {
            return null;
        }

        string body = value["{Binding ".Length..^1];
        int comma = body.IndexOf(',', StringComparison.Ordinal);
        return (comma < 0 ? body : body[..comma]).Trim();
    }

    private static string ViewPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "Folium", "Views", "MainView.axaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("MainView.axaml was not found from the test output directory.");
    }

    private string NewPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "folium-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        _directories.Add(directory);
        return Path.Combine(directory, "library.json");
    }

    private static void Add(LibraryViewModel viewModel, string title, string author)
    {
        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.TitleInput = title;
        viewModel.AuthorInput = author;
        viewModel.SaveCommand.Execute(parameter: null);
    }

    private sealed class MissingOnSecondWriteStore(IBookStore store) : IBookStore
    {
        private int _writes;

        public IReadOnlyList<Book> Read()
        {
            return store.Read();
        }

        public void Replace(IReadOnlyList<Book> books)
        {
            _writes++;
            if (_writes > 1)
            {
                throw new MissingBookException(books[0].Id);
            }

            store.Replace(books);
        }
    }

    private sealed class FailOnSecondWriteStore(IBookStore store) : IBookStore
    {
        private int _writes;

        public IReadOnlyList<Book> Read()
        {
            return store.Read();
        }

        public void Replace(IReadOnlyList<Book> books)
        {
            _writes++;
            if (_writes > 1)
            {
                throw new LibraryStoreException("Folium could not write the library file.");
            }

            store.Replace(books);
        }
    }
}
