using System.ComponentModel;
using System.Diagnostics;
using Folium.Search;

namespace Folium.ViewModels;

/// <summary>Search and preview state with no access to saved books.</summary>
/// <param name="provider">The metadata provider.</param>
public sealed class SearchViewModel(IBookSearch provider) : INotifyPropertyChanged
{
    private CancellationTokenSource? _pending;
    private int _generation;
    private int _page = 1;
    private int _workPage = 1;
    private string _searchedTitle = "";
    private SearchWork? _work;
    private SearchEdition? _edition;
    private bool _worksHaveAnotherPage;
    private bool _editionsHaveAnotherPage;
    private Stage _stage;

    /// <summary>Signals a change to the screen state.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The title entered by the user. Search starts only on request.</summary>
    public string Query { get; set; } = "";

    /// <summary>The current work results.</summary>
    public IReadOnlyList<SearchWork> Works { get; private set; } = [];

    /// <summary>The current edition results.</summary>
    public IReadOnlyList<SearchEdition> Editions { get; private set; } = [];

    /// <summary>The selected work path, or empty text before selection.</summary>
    public string WorkKey => _work?.Key ?? "";

    /// <summary>The selected edition's metadata.</summary>
    public SearchEdition? Preview { get; private set; }

    /// <summary>The selected cover bytes, or null.</summary>
    public ReadOnlyMemory<byte> Cover { get; private set; }

    /// <summary>The current explanation or recoverable error.</summary>
    public string Message { get; private set; } = "Search by title to find a book.";

    /// <summary>True while a request is pending.</summary>
    public bool IsBusy => _pending is not null;

    /// <summary>True when work results are current.</summary>
    public bool ShowWorks => _stage == Stage.Works;

    /// <summary>True when edition results are current.</summary>
    public bool ShowEditions => _stage == Stage.Editions;

    /// <summary>True when the metadata preview is current.</summary>
    public bool ShowPreview => _stage == Stage.Preview;

    /// <summary>The section title.</summary>
    public string Heading => _stage switch
    {
        Stage.Works => "Find a book",
        Stage.Editions => _work?.Title ?? "Editions",
        Stage.Preview => "Edition preview",
        _ => "Find a book",
    };

    /// <summary>True when a preceding result page exists.</summary>
    public bool HasPrevious => !ShowPreview && _page > 1 && !IsBusy;

    /// <summary>True when the current raw provider page was full.</summary>
    public bool HasNext => !ShowPreview && !IsBusy && (ShowWorks ? _worksHaveAnotherPage : _editionsHaveAnotherPage);

    /// <summary>True when a request is available to retry.</summary>
    public bool CanRetry => !IsBusy && _searchedTitle.Length > 0;

    /// <summary>True when the retry button is shown. The offline panel has its own retry button.</summary>
    public bool ShowRetry => SearchFailed && CanRetry && !IsOffline;

    /// <summary>True when the last failure was a lost connection.</summary>
    public bool IsOffline { get; private set; }

    private bool SearchFailed { get; set; }

    /// <summary>The selected edition's provider page.</summary>
    public Uri? Source => Preview is null ? null : new Uri("https://openlibrary.org" + Preview.Key);

    /// <summary>Starts a new title search.</summary>
    public Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            Cancel();
            Message = "Enter a book title.";
            Changed();
            return Task.CompletedTask;
        }

        _searchedTitle = Query.Trim();
        _page = 1;
        _stage = Stage.Works;
        Works = [];
        _worksHaveAnotherPage = false;
        return LoadPageAsync();
    }

    /// <summary>Opens editions for a selected work.</summary>
    /// <param name="work">The selected work.</param>
    public Task OpenWorkAsync(SearchWork work)
    {
        _workPage = _page;
        _work = work;
        _page = 1;
        _stage = Stage.Editions;
        Editions = [];
        _editionsHaveAnotherPage = false;
        return LoadPageAsync();
    }

    /// <summary>Opens one edition and its optional cover.</summary>
    /// <param name="edition">The selected edition.</param>
    public Task OpenEditionAsync(SearchEdition edition)
    {
        _edition = edition;
        _stage = Stage.Preview;
        Preview = null;
        Cover = default;
        return RunAsync(async (token, generation) =>
        {
            SearchEdition result = await provider.PreviewAsync(edition.Key, token).ConfigureAwait(true);
            if (generation != _generation)
            {
                return;
            }

            Preview = result;
            Message = "Metadata from Open Library. Nothing has been saved.";
            Changed();
            await LoadCoverAsync(result.CoverId, generation, token).ConfigureAwait(true);
        });
    }

    /// <summary>Loads a result cover only when the user requests it.</summary>
    /// <param name="coverId">The provider cover identifier.</param>
    public Task PreviewCoverAsync(int coverId)
    {
        Cover = default;
        return RunAsync(async (token, generation) =>
        {
            await LoadCoverAsync(coverId, generation, token).ConfigureAwait(true);
            if (generation == _generation && !Cover.IsEmpty)
            {
                Message = "Cover from Open Library.";
            }
        });
    }

    /// <summary>Loads the adjacent result page.</summary>
    /// <param name="direction">Minus one for previous, one for next.</param>
    /// <exception cref="ArgumentOutOfRangeException">The direction is not minus one or one.</exception>
    public Task ChangePageAsync(int direction)
    {
        return direction switch
        {
            -1 when HasPrevious => LoadPageAsync(_page - 1),
            1 when HasNext => LoadPageAsync(_page + 1),
            -1 or 1 => Task.CompletedTask,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), "The page direction must be minus one or one."),
        };
    }

    /// <summary>Retries the current page or edition.</summary>
    public Task RetryAsync()
    {
        return CanRetry ? RetryCurrentAsync() : Task.CompletedTask;
    }

    /// <summary>Cancels a request and discards any late response.</summary>
    public void Cancel()
    {
        _generation++;
        _pending?.Cancel();
        _pending = null;
        Message = "Search canceled.";
        Changed();
    }

    /// <summary>Returns to editions, then work results. False means the shell can leave search.</summary>
    public bool GoBack()
    {
        Cancel();
        Cover = default;
        Preview = null;
        if (ShowPreview)
        {
            _stage = Stage.Editions;
        }
        else if (ShowEditions)
        {
            _stage = Stage.Works;
            _page = _workPage;
        }
        else
        {
            return false;
        }

        Message = "";
        Changed();
        return true;
    }

    private Task RetryCurrentAsync()
    {
        return ShowPreview && _edition is not null ? OpenEditionAsync(_edition) : LoadPageAsync();
    }

    private Task LoadPageAsync(int? requestedPage = null)
    {
        int page = requestedPage ?? _page;
        return RunAsync(async (token, generation) =>
        {
            if (ShowWorks)
            {
                SearchPage<SearchWork> results = await provider.SearchAsync(_searchedTitle, page, token).ConfigureAwait(true);
                if (generation == _generation)
                {
                    _page = page;
                    Works = results.Items;
                    _worksHaveAnotherPage = results.HasAnotherPage;
                    Message = results.Items.Count == 0 ? "No books found. Try another title." : "Choose a book to see its editions.";
                }
            }
            else
            {
                SearchPage<SearchEdition> results = await provider.EditionsAsync(_work!.Key, page, token).ConfigureAwait(true);
                if (generation == _generation)
                {
                    _page = page;
                    Editions = results.Items;
                    _editionsHaveAnotherPage = results.HasAnotherPage;
                    Message = results.Items.Count == 0 ? "No editions on this page. Return to the previous page or try another book." : "Choose an edition to preview its metadata.";
                }
            }
        });
    }

    private async Task RunAsync(Func<CancellationToken, int, Task> operation)
    {
        Cancel();
        int generation = _generation;
        using CancellationTokenSource pending = new();
        _pending = pending;
        Message = "Searching Open Library…";
        IsOffline = false;
        SearchFailed = false;
        Changed();
        try
        {
            await operation(pending.Token, generation).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (pending.IsCancellationRequested)
        {
            // Cancellation keeps the state set by Cancel or the next request.
        }
        catch (SearchException exception)
        {
            Trace.TraceError(exception.ToString());
            if (generation == _generation)
            {
                Message = exception.Message;
                IsOffline = exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase);
                SearchFailed = !IsOffline;
            }
        }
        finally
        {
            if (generation == _generation)
            {
                _pending = null;
                Changed();
            }
        }
    }

    private async Task LoadCoverAsync(int? coverId, int generation, CancellationToken token)
    {
        if (coverId is not int id)
        {
            return;
        }

        try
        {
            byte[]? bytes = await provider.CoverAsync(id, token).ConfigureAwait(true);
            if (generation == _generation)
            {
                Cover = bytes ?? ReadOnlyMemory<byte>.Empty;
                if (bytes is null)
                {
                    Message = "No cover is available. You can still inspect the edition metadata.";
                }
            }
        }
        catch (SearchException exception)
        {
            Trace.TraceError(exception.ToString());
            if (generation == _generation)
            {
                Message = "The cover could not load. The edition metadata is available. Try again to reload the cover.";
                SearchFailed = true;
                Changed();
            }
        }
    }

    private void Changed()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName: null));
    }

    private enum Stage
    {
        Works = 0,
        Editions = 1,
        Preview = 2,
    }
}
