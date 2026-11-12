namespace Folium.Updates;

/// <summary>Checks whether an APK can update an installed package.</summary>
public static class ApkUpdateVerifier
{
    /// <summary>Compares the archive package with the installed package.</summary>
    /// <param name="archive">The APK archive metadata.</param>
    /// <param name="installed">The installed package metadata.</param>
    /// <returns>The first verification failure, or <see cref="ApkVerificationFailure.None"/>.</returns>
    public static ApkVerificationFailure Compare(ApkMetadata archive, ApkMetadata installed)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(installed);
        bool packageMatches = string.Equals(archive.PackageName, installed.PackageName, StringComparison.Ordinal);
        bool versionIsNewer = archive.VersionCode > installed.VersionCode;
        bool signerMatches = HasMatchingSigner(archive, installed);
        return (packageMatches, versionIsNewer, signerMatches) switch
        {
            (false, _, _) => ApkVerificationFailure.PackageNameMismatch,
            (true, false, _) => ApkVerificationFailure.VersionNotNewer,
            (true, true, false) => ApkVerificationFailure.SignatureMismatch,
            _ => ApkVerificationFailure.None,
        };
    }

    private static bool HasMatchingSigner(ApkMetadata archive, ApkMetadata installed)
    {
        return archive.Signers.Count == 1 && installed.Signers.Count == 1
            && archive.Signers[0] is byte[] archiveSigner
            && installed.Signers[0] is byte[] installedSigner
            && archiveSigner.AsSpan().SequenceEqual(installedSigner);
    }
}
