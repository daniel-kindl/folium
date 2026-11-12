namespace Folium.Updates;

/// <summary>The platform failure class returned after an install attempt.</summary>
public enum PackageInstallFailureKind
{
    /// <summary>The APK is invalid or cannot be used.</summary>
    InvalidApk = 0,
    /// <summary>The package conflicts with an installed package or signer.</summary>
    PackageConflict = 1,
    /// <summary>The device cannot install this package.</summary>
    DeviceIncompatible = 2,
    /// <summary>The platform returned another install failure.</summary>
    Other = 3,
}
