using Folium.ViewModels;

namespace Folium.Tests;

public class MainViewModelTests
{
    [Fact]
    public void ShowsApplicationName()
    {
        var viewModel = new MainViewModel();

        Assert.Equal("Folium", viewModel.Title);
    }
}
