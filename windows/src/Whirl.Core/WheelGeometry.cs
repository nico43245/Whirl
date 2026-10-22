namespace Whirl.Core;

public enum HitKind { None, Slice, Hub }

/// <summary>Where the pointer is relative to the wheel.</summary>
public readonly record struct WheelHit(HitKind Kind, int Slice = -1)
{
    public static readonly WheelHit None = new(HitKind.None);
    public static readonly WheelHit Hub = new(HitKind.Hub);
    public static WheelHit OfSlice(int index) => new(HitKind.Slice, index);
}

/// <summary>Pure geometry of the 8-slice wheel, in DIPs, origin top-left. Slice 0 sits at 12 o'clock, then clockwise.</summary>
public static class WheelGeometry
{
    public const double Size = 440;
    public const double InnerRadius = 72;
    public const double OuterRadius = 168;
    public const double HubRadius = 62;
    public const int SliceCount = 8;

    public static WheelHit Hit(double x, double y)
    {
        double dx = x - Size / 2, dy = y - Size / 2, distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance <= HubRadius) return WheelHit.Hub;
        // Generous outer edge so a fast flick past the rim still selects the slice.
        if (distance >= InnerRadius - 6 && distance <= OuterRadius + 28) return WheelHit.OfSlice(SliceIndex(dx, dy));
        return WheelHit.None;
    }

    public static int SliceIndex(double dx, double dy)
    {
        var degrees = (Math.Atan2(dy, dx) * 180 / Math.PI + 90 + 22.5) % 360;
        if (degrees < 0) degrees += 360;
        return (int)(degrees / 45) % SliceCount;
    }

    /// <summary>Angle in degrees (screen orientation, y down) of the middle of a slice.</summary>
    public static double CenterAngle(int index) => -90 + index * 45;

    /// <summary>The middle of a slice, in wheel coordinates.</summary>
    public static (double X, double Y) PointForSlice(int index)
    {
        var rad = CenterAngle(index) * Math.PI / 180;
        const double mid = (InnerRadius + OuterRadius) / 2;
        return (Size / 2 + Math.Cos(rad) * mid, Size / 2 + Math.Sin(rad) * mid);
    }
}
