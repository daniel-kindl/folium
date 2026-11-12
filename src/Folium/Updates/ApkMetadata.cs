namespace Folium.Updates;

/// <summary>Describes the package identity and signers of an APK.</summary>
/// <param name="PackageName">The Android package name, if available.</param>
/// <param name="VersionCode">The Android version code.</param>
/// <param name="Signers">The APK content signers.</param>
public sealed record ApkMetadata(string? PackageName, long VersionCode, IReadOnlyList<byte[]?> Signers);
