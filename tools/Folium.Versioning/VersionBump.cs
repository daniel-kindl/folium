namespace Folium.Versioning;

/// <summary>The version bump to apply for a release.</summary>
public enum VersionBump
{
    /// <summary>Work out the bump from Conventional Commit titles.</summary>
    Auto = 0,

    /// <summary>Raise the patch component.</summary>
    Patch = 1,

    /// <summary>Raise the minor component and set the patch to 0.</summary>
    Minor = 2,

    /// <summary>Raise the major component and set the minor and patch to 0.</summary>
    Major = 3,
}
