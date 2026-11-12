namespace Folium.Updates;

/// <summary>The result of comparing an update APK with the installed package.</summary>
public enum ApkVerificationFailure
{
    /// <summary>The APK can update the installed package.</summary>
    None = 0,
    /// <summary>The APK package name differs from the installed package.</summary>
    PackageNameMismatch = 1,
    /// <summary>The APK version code is not greater than the installed version code.</summary>
    VersionNotNewer = 2,
    /// <summary>The APK does not have exactly one matching signer.</summary>
    SignatureMismatch = 3,
}
