namespace Folium.Updates;

/// <summary>Installs a verified release APK on the current platform.</summary>
public interface IUpdateInstaller
{
    /// <summary>Whether the platform allows this app to request package installation.</summary>
    bool CanRequestInstallPackages { get; }

    /// <summary>Opens the platform setting for allowing package installation.</summary>
    void OpenInstallPermissionSettings();

    /// <summary>Opens the release page in the platform browser.</summary>
    /// <param name="pageUrl">The GitHub release page.</param>
    void OpenReleasePage(Uri pageUrl);

    /// <summary>Downloads and starts installing the release APK.</summary>
    /// <param name="release">The selected release.</param>
    /// <param name="progress">Download progress from zero to one.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    Task InstallAsync(ReleaseInfo release, IProgress<UpdateProgress> progress, CancellationToken cancellationToken);
}
