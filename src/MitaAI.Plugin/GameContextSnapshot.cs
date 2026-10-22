using System.Text;

namespace MitaAI.Plugin;

// A raw game value, or unknown with a diagnostic reason. Known values are kept exactly as the game reported them.
internal readonly record struct ContextValue
{
    private ContextValue(string? raw, string reason) { Raw = raw; Reason = reason; }
    internal string? Raw { get; }
    internal string Reason { get; }
    internal bool IsKnown => Raw != null;
    internal static ContextValue Known(string raw) => new(raw, "");
    internal static ContextValue Unknown(string reason) => new(null, reason);
}

// One Location34_Communication instance seen by the collector. Eligible means enabled, active and in the active scene.
internal readonly record struct RoomObservation(string? Raw, bool Eligible, bool ReadFailed = false);

// Scene (chapter) identity and the player's room are separate facts; neither is derived from the other.
internal sealed record GameContextSnapshot(ContextValue Scene, ContextValue PlayerRoom, int RoomSources, float CapturedAt)
{
    internal const string RoomSource = "Location34_Communication.roomPlayer";

    internal static GameContextSnapshot Unavailable(string reason, float capturedAt = float.NaN) =>
        new(ContextValue.Unknown(reason), ContextValue.Unknown(reason), 0, capturedAt);

    internal string ToLogLine() =>
        $"MITAAI_CONTEXT {Field("scene", Scene)} {Field("room", PlayerRoom)} roomSource={RoomSource} roomSources={RoomSources}";

    private static string Field(string name, ContextValue value) =>
        value.IsKnown ? $"{name}=\"{Escape(value.Raw!)}\"" : $"{name}=unknown {name}Reason={value.Reason}";

    // Log-safe quoting only; the snapshot itself keeps the raw value.
    private static string Escape(string raw)
    {
        var builder = new StringBuilder(raw.Length);
        foreach (char character in raw)
        {
            if (character is '"' or '\\') builder.Append('\\').Append(character);
            else if (char.IsControl(character)) builder.Append("\\u").Append(((int)character).ToString("x4"));
            else builder.Append(character);
        }
        return builder.ToString();
    }
}

internal static class GameContextRules
{
    internal const int MaxRawLength = 128;

    internal static ContextValue Scene(bool validAndLoaded, string? name)
    {
        if (!validAndLoaded) return ContextValue.Unknown("scene-not-loaded");
        return Raw(name);
    }

    // Exactly one eligible source is required. Several are ambiguous even when they agree.
    internal static ContextValue Room(IReadOnlyCollection<RoomObservation> observations, out int eligibleSources)
    {
        var eligible = observations.Where(observation => observation.Eligible).ToList();
        eligibleSources = eligible.Count;
        if (eligible.Count == 0) return ContextValue.Unknown("no-source");
        if (eligible.Count > 1) return ContextValue.Unknown("ambiguous-sources");
        return eligible[0].ReadFailed ? ContextValue.Unknown("read-failed") : Raw(eligible[0].Raw);
    }

    internal static ContextValue Raw(string? raw)
    {
        if (raw == null) return ContextValue.Unknown("missing");
        if (string.IsNullOrWhiteSpace(raw)) return ContextValue.Unknown("empty");
        if (raw.Length > MaxRawLength || raw.Any(char.IsControl)) return ContextValue.Unknown("invalid");
        return ContextValue.Known(raw);
    }
}

// Restricts capture to the thread that created the collector and spaces out repeated scene searches.
internal sealed class ContextCaptureGate
{
    private readonly int ownerThread = Environment.CurrentManagedThreadId;
    private readonly float minimumInterval;
    private float lastRefresh = float.NaN;
    private int? lastSceneHandle;

    internal ContextCaptureGate(float minimumIntervalSeconds) => minimumInterval = minimumIntervalSeconds;

    internal bool OnOwnerThread => Environment.CurrentManagedThreadId == ownerThread;

    internal bool ShouldRefresh(float now, int sceneHandle = 0)
    {
        if (!float.IsFinite(now)) return true;
        // A clock that moved backwards cannot prove the cached value is recent.
        if (lastSceneHandle == sceneHandle && float.IsFinite(lastRefresh)
            && now >= lastRefresh && now - lastRefresh < minimumInterval) return false;
        lastRefresh = now;
        lastSceneHandle = sceneHandle;
        return true;
    }
}
