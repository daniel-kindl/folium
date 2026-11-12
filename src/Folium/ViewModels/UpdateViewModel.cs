using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Folium.Updates;

namespace Folium.ViewModels;

/// <summary>Controls update checks, offers and APK installation.</summary>
public sealed class UpdateViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly UpdateCheckService _checks;
    private readonly IUpdateInstaller? _installer;
    private CancellationTokenSource? _installCancellation;

    /// <summary>Creates the update controls.</summary>
    /// <param name="checks">The update check service.</param>
    /// <param name="installer">The platform installer.</param>
    /// <param name="currentVersion">The installed version.</param>
    public UpdateViewModel(UpdateCheckService checks, IUpdateInstaller? installer, string currentVersion)
    {
        _checks = checks ?? throw new ArgumentNullException(nameof(checks));
        _installer = installer;
        CurrentVersion = currentVersion;
        CheckNowCommand = new Command(_ => RunCheckNow(), _ => !IsBusy);
        UpdateCommand = new Command(_ => InstallAsync(), _ => Release is not null && !IsBusy && _installer is not null);
        SkipCommand = new Command(_ => Skip(), _ => Release is not null);
        LaterCommand = new Command(_ => Later(), _ => Release is not null);
        ToggleSettingsCommand = new Command(_ => SetShowSettings(!ShowSettings), _ => true);
        CancelCommand = new Command(_ => _installCancellation?.Cancel(throwOnFirstException: false), _ => IsBusy && CanCancel);
        PermissionCommand = new Command(_ => _installer?.OpenInstallPermissionSettings(), _ => NeedsInstallPermission);
        RetryCommand = new Command(_ => InstallAsync(), _ => Release is not null && !IsBusy && _installer is not null);
        OpenReleasePageCommand = new Command(_ => OpenReleasePage(), _ => Release is not null && _installer is not null);
        ToggleNotesCommand = new Command(_ => SetShowNotes(!ShowNotes), _ => Release is not null);
        CloseSettingsCommand = new Command(_ => SetShowSettings(show: false), _ => ShowSettings);
    }

    /// <summary>Notifies bound controls when an update value changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public void Dispose()
    {
        _installCancellation?.Cancel(throwOnFirstException: false);
        _installCancellation?.Dispose();
        _installCancellation = null;
    }

    /// <summary>Starts a due check when the app opens or resumes.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    public Task CheckAtStartAsync(CancellationToken cancellationToken = default)
    {
        return CheckAsync(force: false, cancellationToken);
    }

    /// <summary>Starts a check that ignores the automatic check interval.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    public Task CheckNowAsync(CancellationToken cancellationToken = default)
    {
        return CheckAsync(force: true, cancellationToken);
    }

    /// <summary>Continues permission handling or checks a pending install when the app resumes.</summary>
    /// <param name="installPromptWasDismissed">Whether Android paused this activity for the install prompt.</param>
    public void OnResume(bool installPromptWasDismissed = false)
    {
        if (NeedsInstallPermission && _installer?.CanRequestInstallPackages == true)
        {
            SetNeedsInstallPermission(needed: false);
            InstallAsync();
        }
        else if (IsBusy && !CanCancel && !installPromptWasDismissed)
        {
            SetError(ErrorSummary(UpdateFailureReason.Install));
            _installCancellation?.Cancel(throwOnFirstException: false);
        }
    }

    /// <summary>The available release.</summary>
    public ReleaseInfo? Release { get; private set; }

    /// <summary>Whether an update offer is visible.</summary>
    public bool HasRelease => Release is not null;

    /// <summary>Whether the plain-text release notes are visible.</summary>
    public bool ShowNotes { get; private set; }

    /// <summary>The banner text for the available version.</summary>
    public string OfferText => Release is null ? string.Empty : $"Folium {Release.Version} is available";

    /// <summary>Whether the settings panel is visible.</summary>
    public bool ShowSettings { get; private set; }

    /// <summary>Whether an update operation is active.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>Whether download progress is visible.</summary>
    public bool ShowProgress => IsBusy;

    /// <summary>Whether the current update phase can be cancelled.</summary>
    public bool CanCancel { get; private set; }

    /// <summary>The current update phase for screen readers.</summary>
    public string ProgressPhaseText => CanCancel ? "Downloading update" : "Installing update";

    /// <summary>The release notes button label for screen readers.</summary>
    public string ReleaseNotesButtonText => ShowNotes ? "Hide release notes" : "Show release notes";

    /// <summary>Download progress from zero to one.</summary>
    public double Progress { get; private set; }

    /// <summary>Download progress as a percentage.</summary>
    public string ProgressText => Progress.ToString("P0", CultureInfo.CurrentCulture);

    /// <summary>Whether an install error is visible.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>Whether Folium must allow package installation in Android settings.</summary>
    public bool NeedsInstallPermission { get; private set; }

    /// <summary>The current install error.</summary>
    public string Error { get; private set; } = string.Empty;

    /// <summary>The installed app version.</summary>
    public string CurrentVersion { get; }

    /// <summary>The settings line that shows the installed version.</summary>
    public string VersionText => $"Folium {CurrentVersion}";

    /// <summary>Whether automatic checks are enabled.</summary>
    public bool ChecksEnabled
    {
        get => _checks.Settings.ChecksEnabled;
        set
        {
            _checks.SetEnabled(value);
            Raise(nameof(ChecksEnabled));
        }
    }

    /// <summary>Checks for updates now.</summary>
    public ICommand CheckNowCommand { get; }
    /// <summary>Downloads and installs the offered release.</summary>
    public ICommand UpdateCommand { get; }
    /// <summary>Skips the offered release.</summary>
    public ICommand SkipCommand { get; }
    /// <summary>Hides the offer until a later check.</summary>
    public ICommand LaterCommand { get; }
    /// <summary>Opens or closes update settings.</summary>
    public ICommand ToggleSettingsCommand { get; }
    /// <summary>Cancels the APK download.</summary>
    public ICommand CancelCommand { get; }
    /// <summary>Opens the Android install permission setting.</summary>
    public ICommand PermissionCommand { get; }
    /// <summary>Retries the APK download and install.</summary>
    public ICommand RetryCommand { get; }
    /// <summary>Opens the GitHub release page.</summary>
    public ICommand OpenReleasePageCommand { get; }
    /// <summary>Shows or hides the release notes.</summary>
    public ICommand ToggleNotesCommand { get; }
    /// <summary>Closes update settings.</summary>
    public ICommand CloseSettingsCommand { get; }

    private async Task CheckAsync(bool force, CancellationToken cancellationToken)
    {
        try
        {
            ReleaseInfo? release = await _checks.CheckAsync(CurrentVersion, force, cancellationToken).ConfigureAwait(true);
            if (release is not null)
            {
                SetRelease(release);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException)
        {
        }
    }

    private async void RunCheckNow()
    {
        await CheckNowAsync(cancellationToken: CancellationToken.None).ConfigureAwait(true);
    }

    private async void InstallAsync()
    {
        if (!TryPrepareInstall(out ReleaseInfo release, out IUpdateInstaller installer))
        {
            return;
        }

        SetBusy(busy: true);
        SetError(string.Empty);
        _installCancellation?.Dispose();
        _installCancellation = new CancellationTokenSource();
        CancellationTokenSource cancellation = _installCancellation;
        Progress<UpdateProgress> progress = new(SetProgress);
        try
        {
            await InstallReleaseAsync(installer, release, progress, cancellation.Token).ConfigureAwait(true);
        }
        finally
        {
            cancellation.Dispose();
            _installCancellation = null;
            SetBusy(busy: false);
        }
    }

    private async Task InstallReleaseAsync(IUpdateInstaller installer, ReleaseInfo release,
        IProgress<UpdateProgress> progress, CancellationToken cancellationToken)
    {
        try
        {
            await installer.InstallAsync(release, progress, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            if (!HasError)
            {
                SetError("Update download cancelled.");
            }
        }
        catch (Exception exception) when (IsExpectedInstallFailure(exception))
        {
            SetError(ErrorSummary(exception));
        }
    }

    private static bool IsExpectedInstallFailure(Exception exception)
    {
        return exception is UpdateFailureException or HttpRequestException or IOException or UnauthorizedAccessException or InvalidOperationException;
    }

    private bool TryPrepareInstall(out ReleaseInfo release, out IUpdateInstaller installer)
    {
        release = Release!;
        installer = _installer!;
        if (release is null || installer is null)
        {
            return false;
        }

        if (installer.CanRequestInstallPackages)
        {
            return true;
        }

        SetNeedsInstallPermission(needed: true);
        return false;
    }

    private void Skip()
    {
        if (Release is not null)
        {
            _checks.Skip(Release.Version);
        }
        SetRelease(release: null);
    }

    private void Later()
    {
        SetRelease(release: null);
    }

    private void Raise(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void SetRelease(ReleaseInfo? release)
    {
        Release = release;
        Raise(nameof(Release));
        Raise(nameof(HasRelease));
        Raise(nameof(OfferText));
        if (release is null)
        {
            SetShowNotes(show: false);
        }

        RaiseCommands();
        ((Command)ToggleNotesCommand).RaiseCanExecuteChanged();
    }

    private void SetShowSettings(bool show)
    {
        ShowSettings = show;
        Raise(nameof(ShowSettings));
        ((Command)CloseSettingsCommand).RaiseCanExecuteChanged();
    }

    private void SetShowNotes(bool show)
    {
        ShowNotes = show;
        Raise(nameof(ShowNotes));
        Raise(nameof(ReleaseNotesButtonText));
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        CanCancel = busy;
        Raise(nameof(CanCancel));
        Raise(nameof(ProgressPhaseText));
        Raise(nameof(IsBusy));
        Raise(nameof(ShowProgress));
        RaiseCommands();
        ((Command)CancelCommand).RaiseCanExecuteChanged();
    }

    private void SetProgress(UpdateProgress progress)
    {
        if (!IsBusy)
        {
            return;
        }

        Progress = progress.Percent;
        CanCancel = progress.CanCancel;
        Raise(nameof(Progress));
        Raise(nameof(ProgressText));
        Raise(nameof(CanCancel));
        Raise(nameof(ProgressPhaseText));
        ((Command)CancelCommand).RaiseCanExecuteChanged();
    }

    private void SetError(string error)
    {
        Error = error;
        Raise(nameof(Error));
        Raise(nameof(HasError));
    }

    private void SetNeedsInstallPermission(bool needed)
    {
        NeedsInstallPermission = needed;
        Raise(nameof(NeedsInstallPermission));
        ((Command)PermissionCommand).RaiseCanExecuteChanged();
    }

    private void OpenReleasePage()
    {
        if (Release is not null)
        {
            _installer?.OpenReleasePage(Release.PageUrl);
        }
    }

    private static string ErrorSummary(UpdateFailureReason reason)
    {
        return reason switch
        {
            UpdateFailureReason.Download => "Download failed.",
            UpdateFailureReason.FileCheck => "File check failed.",
            UpdateFailureReason.SignatureMismatch => "Signature mismatch.",
            UpdateFailureReason.Install => "Install failed.",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown update failure reason."),
        };
    }

    private static string ErrorSummary(Exception exception)
    {
        UpdateFailureReason reason = exception switch
        {
            UpdateFailureException failure => failure.Reason,
            HttpRequestException or IOException or UnauthorizedAccessException => UpdateFailureReason.Download,
            _ => UpdateFailureReason.Install,
        };
        return ErrorSummary(reason);
    }

    private void RaiseCommands()
    {
        ((Command)CheckNowCommand).RaiseCanExecuteChanged();
        ((Command)UpdateCommand).RaiseCanExecuteChanged();
        ((Command)RetryCommand).RaiseCanExecuteChanged();
    }

    private sealed class Command(Action<object?> execute, Func<object?, bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter)
        {
            return canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            execute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
