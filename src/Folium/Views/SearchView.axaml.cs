using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Folium.Search;
using Folium.ViewModels;

namespace Folium.Views;

/// <summary>Shows provider search results and read-only edition metadata.</summary>
public sealed partial class SearchView : UserControl, IDisposable
{
    private SearchViewModel? _model;
    private Bitmap? _cover;
    private Image? _resultImage;
    private ReadOnlyMemory<byte> _coverBytes;

    /// <summary>Loads the search markup.</summary>
    public SearchView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += ContextChanged;
    }

    /// <summary>Releases the decoded cover and detaches state notifications.</summary>
    public void Dispose()
    {
        _model?.PropertyChanged -= ModelChanged;

        _cover?.Dispose();
        _cover = null;
        _coverBytes = default;
        _model = null;
    }

    /// <summary>Releases view resources when the view leaves the visual tree.</summary>
    /// <param name="e">The visual-tree event.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Attaches state notifications when the view enters the visual tree.</summary>
    /// <param name="e">The visual-tree event.</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ContextChanged(this, EventArgs.Empty);
    }

    private void ContextChanged(object? sender, EventArgs args)
    {
        _model?.PropertyChanged -= ModelChanged;

        _model = DataContext as SearchViewModel;
        _model?.PropertyChanged += ModelChanged;

        UpdateCover();
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        UpdateCover();
    }

    private void UpdateCover()
    {
        if (_coverBytes.Equals(_model?.Cover ?? ReadOnlyMemory<byte>.Empty))
        {
            return;
        }

        _coverBytes = _model?.Cover ?? ReadOnlyMemory<byte>.Empty;
        Image image = this.FindControl<Image>("CoverImage")!;
        image.Source = null;
        _resultImage?.Source = null;
        _cover?.Dispose();
        _cover = null;
        if (!_coverBytes.IsEmpty)
        {
            DecodeCover(image);
        }
    }

    private void DecodeCover(Image image)
    {
        try
        {
            using MemoryStream stream = new(_coverBytes.ToArray());
            _cover = new Bitmap(stream);
            Image target = _model?.ShowPreview == true || _resultImage is null ? image : _resultImage;
            target.Source = _cover;
        }
        catch (ArgumentException exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    private async void SearchClicked(object? sender, RoutedEventArgs args)
    {
        if (_model is not null)
        {
            await _model.SearchAsync().ConfigureAwait(true);
        }
    }

    private async void WorkClicked(object? sender, RoutedEventArgs args)
    {
        if (_model is not null && sender is Button { Tag: SearchWork work })
        {
            await _model.OpenWorkAsync(work).ConfigureAwait(true);
        }
    }

    private async void EditionClicked(object? sender, RoutedEventArgs args)
    {
        if (_model is not null && sender is Button { Tag: SearchEdition edition })
        {
            await _model.OpenEditionAsync(edition).ConfigureAwait(true);
        }
    }

    private void CancelClicked(object? sender, RoutedEventArgs args)
    {
        _model?.Cancel();
    }

    private async void RetryClicked(object? sender, RoutedEventArgs args)
    {
        if (_model is not null)
        {
            await _model.RetryAsync().ConfigureAwait(true);
        }
    }

    private async void PreviousClicked(object? sender, RoutedEventArgs args)
    {
        await _model!.ChangePageAsync(-1).ConfigureAwait(true);
    }

    private async void NextClicked(object? sender, RoutedEventArgs args)
    {
        await _model!.ChangePageAsync(1).ConfigureAwait(true);
    }

    private void BackClicked(object? sender, RoutedEventArgs args)
    {
        if (this.FindAncestorOfType<MainView>()?.DataContext is LibraryViewModel library)
        {
            _ = library.GoBack();
        }
    }

    private async void CoverClicked(object? sender, RoutedEventArgs args)
    {
        args.Handled = true;
        if (_model is not null && sender is Button { Tag: int id, Parent: StackPanel panel })
        {
            _resultImage?.Source = null;
            _resultImage = panel.Children.OfType<Image>().First();
            await _model.PreviewCoverAsync(id).ConfigureAwait(true);
        }
    }

    private async void SourceClicked(object? sender, RoutedEventArgs args)
    {
        if (_model?.Source is Uri source)
        {
            _ = await TopLevel.GetTopLevel(this)!.Launcher.LaunchUriAsync(source).ConfigureAwait(true);
        }
    }
}
