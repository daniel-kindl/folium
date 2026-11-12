namespace Folium.Updates;

/// <summary>Reports the download percentage and whether the current phase can be cancelled.</summary>
/// <param name="Percent">The download fraction from zero through one.</param>
/// <param name="CanCancel">Whether cancellation can still stop the operation.</param>
public sealed record UpdateProgress(double Percent, bool CanCancel);
