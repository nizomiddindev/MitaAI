namespace MitaAI.Plugin;

internal readonly record struct ProjectedPoint(float X, float Y, float Depth);
internal readonly record struct SpatialPoint(float X, float Y, float Z);

internal static class TargetVisibility
{
    internal static bool IntersectsViewport(IEnumerable<ProjectedPoint> points)
        => Overlaps(points, 0, 0, 1, 1);

    internal static bool ContainsAimPoint(IEnumerable<ProjectedPoint> points)
        => Overlaps(points, 0.5f, 0.5f, 0.5f, 0.5f);

    internal static bool WithinInteractionRange(float distance, float maximum)
        => float.IsFinite(distance) && float.IsFinite(maximum) && distance >= 0
            && maximum > 0 && distance <= maximum;

    internal static bool TryAimDistanceNearHead(SpatialPoint origin, SpatialPoint direction,
        SpatialPoint min, SpatialPoint max, SpatialPoint? head, out float distance)
    {
        distance = 0;
        if (!Finite(min) || !Finite(max) || min.X > max.X || min.Y > max.Y || min.Z > max.Z) return false;
        if (head is SpatialPoint point)
        {
            if (!Finite(point)) return false;
            // Skinned-mesh culling bounds can omit the animated face during a close-up.
            const float radius = 0.22f;
            min = new SpatialPoint(Math.Min(min.X, point.X - radius), Math.Min(min.Y, point.Y - radius), Math.Min(min.Z, point.Z - radius));
            max = new SpatialPoint(Math.Max(max.X, point.X + radius), Math.Max(max.Y, point.Y + radius), Math.Max(max.Z, point.Z + radius));
        }
        return TryAimDistance(origin, direction, min, max, out distance);
    }

    internal static bool TryAimDistance(SpatialPoint origin, SpatialPoint direction, SpatialPoint min, SpatialPoint max, out float distance)
    {
        distance = 0;
        if (!Finite(origin) || !Finite(direction) || !Finite(min) || !Finite(max)
            || min.X > max.X || min.Y > max.Y || min.Z > max.Z
            || direction == new SpatialPoint(0, 0, 0)) return false;
        float entry = 0, exit = float.PositiveInfinity;
        if (!ClipAxis(origin.X, direction.X, min.X, max.X, ref entry, ref exit)
            || !ClipAxis(origin.Y, direction.Y, min.Y, max.Y, ref entry, ref exit)
            || !ClipAxis(origin.Z, direction.Z, min.Z, max.Z, ref entry, ref exit)) return false;
        distance = entry;
        return true;
    }

    private static bool Finite(SpatialPoint point) => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);

    private static bool ClipAxis(float origin, float direction, float min, float max, ref float entry, ref float exit)
    {
        if (Math.Abs(direction) < 0.000001f) return origin >= min && origin <= max;
        float first = (min - origin) / direction, last = (max - origin) / direction;
        if (first > last) (first, last) = (last, first);
        entry = Math.Max(entry, first);
        exit = Math.Min(exit, last);
        return entry <= exit;
    }

    private static bool Overlaps(IEnumerable<ProjectedPoint> points, float left, float bottom, float right, float top)
    {
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        bool inFront = false;
        foreach (var point in points)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Depth)
                || point.Depth <= 0) continue;
            inFront = true;
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }
        return inFront && maxX >= left && minX <= right && maxY >= bottom && minY <= top;
    }
}
