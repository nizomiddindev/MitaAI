namespace MitaAI.Plugin;

internal static class RoundedMask
{
    internal const int Size = 96;
    internal const int Radius = 32;

    internal static float Alpha(int x, int y)
        => Math.Clamp(0.5f - Distance(x, y), 0, 1);

    internal static float BorderAlpha(int x, int y)
        => Math.Max(0, Alpha(x, y) - Math.Clamp(0.5f - (Distance(x, y) + 4), 0, 1));

    private static float Distance(int x, int y)
    {
        float qx = Math.Abs(x + 0.5f - Size / 2f) - (Size / 2f - Radius);
        float qy = Math.Abs(y + 0.5f - Size / 2f) - (Size / 2f - Radius);
        float dx = Math.Max(qx, 0), dy = Math.Max(qy, 0);
        return MathF.Sqrt(dx * dx + dy * dy) + Math.Min(Math.Max(qx, qy), 0) - Radius;
    }
}
