using System;

namespace Aion2Meter.Services;

internal static class MeterWindowHeightPolicy
{
    public const int MaximumVisibleRows = 10;

    public static double MinimumHeight(bool compact, int rowCount)
    {
        int visibleRows = Math.Clamp(rowCount, 1, MaximumVisibleRows);
        return compact
            ? Math.Max(180, 174 + visibleRows * 40)
            : Math.Max(430, 220 + visibleRows * 51);
    }

    public static double DesiredHeight(bool compact, int rowCount, double preferredHeight, double maximumHeight) =>
        Math.Min(maximumHeight, Math.Max(preferredHeight, MinimumHeight(compact, rowCount)));
}
