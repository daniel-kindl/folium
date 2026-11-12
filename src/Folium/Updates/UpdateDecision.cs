namespace Folium.Updates;

/// <summary>The result of applying update-check rules.</summary>
public enum UpdateDecision
{
    /// <summary>Checks are disabled.</summary>
    Disabled = 0,
    /// <summary>The previous check is less than one day old.</summary>
    NotDue = 1,
    /// <summary>No newer release is available.</summary>
    None = 2,
    /// <summary>A newer release is available.</summary>
    Offer = 3,
}
