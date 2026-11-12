namespace Folium.Updates;

/// <summary>Checks GitHub releases and applies the update offer policy.</summary>
/// <param name="source">The release metadata source.</param>
/// <param name="settingsFile">The private update settings file.</param>
/// <param name="clock">The clock used to apply the 24-hour policy.</param>
public sealed class UpdateCheckService(IReleaseSource source, UpdateSettingsFile settingsFile, Func<DateTimeOffset>? clock = null)
{
    private static readonly SemaphoreSlim CheckGate = new(1, 1);
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);

    /// <summary>Returns an eligible release, or null when checks are disabled, not due, or unavailable.</summary>
    /// <param name="currentVersion">The installed app version.</param>
    /// <param name="force">Whether a user started the check manually.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<ReleaseInfo?> CheckAsync(string currentVersion, bool force, CancellationToken cancellationToken)
    {
        await CheckGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            UpdateSettings settings = settingsFile.Read();
            DateTimeOffset checkedAt = _clock();
            if (!UpdatePolicy.IsCheckDue(settings, checkedAt, force))
            {
                return null;
            }

            ReleaseInfo? release = await source.GetLatestAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                settingsFile.Write(settings with { LastCheck = checkedAt });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return null;
            }

            return UpdatePolicy.ShouldOffer(release, currentVersion, settings.SkippedVersion) ? release : null;
        }
        finally
        {
            _ = CheckGate.Release();
        }
    }

    /// <summary>Stores the version the user chose to skip.</summary>
    /// <param name="version">The release version.</param>
    public void Skip(SemVersion version)
    {
        UpdateSettings settings = settingsFile.Read();
        settingsFile.Write(settings with { SkippedVersion = version.ToString() });
    }

    /// <summary>Changes automatic update checks.</summary>
    /// <param name="enabled">Whether automatic checks must run.</param>
    public void SetEnabled(bool enabled)
    {
        UpdateSettings settings = settingsFile.Read();
        settingsFile.Write(settings with { ChecksEnabled = enabled });
    }

    /// <summary>Returns the current preferences.</summary>
    public UpdateSettings Settings => settingsFile.Read();
}
