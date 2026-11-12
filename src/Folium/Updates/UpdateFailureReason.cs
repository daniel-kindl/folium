namespace Folium.Updates;

/// <summary>The reason an update operation failed.</summary>
public enum UpdateFailureReason
{
    /// <summary>The release or APK download failed.</summary>
    Download = 0,
    /// <summary>The downloaded APK failed a package or version check.</summary>
    FileCheck = 1,
    /// <summary>The downloaded APK signer differs from the installed signer.</summary>
    SignatureMismatch = 2,
    /// <summary>Android could not install the verified APK.</summary>
    Install = 3,
}
