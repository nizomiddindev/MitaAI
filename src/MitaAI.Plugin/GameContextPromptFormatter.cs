using System.Globalization;
using System.Text;

namespace MitaAI.Plugin;

// Pure offline formatter that revalidates a GameContextSnapshot and serializes it as bounded,
// data-only JSON without translating raw strings or inferring unobserved game state.
internal static class GameContextPromptFormatter
{
    internal const string Schema = "mitaai.game_context.v1";
    internal const int MaxReasonLength = 64;
    internal const int MaxReportedSources = 999;
    internal const int MaxOutputLength = 832;

    internal static readonly IReadOnlyList<string> UnobservedFields = new[]
    {
        "mitaIdentity",
        "mitaRoom",
        "mitaActivity",
        "questProgress",
        "surroundings"
    };

    internal static GameContextSnapshot Revalidate(GameContextSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new GameContextSnapshot(
                ContextValue.Unknown("missing"),
                ContextValue.Unknown("missing"),
                0,
                float.NaN);
        }

        var scene = RevalidateValue(snapshot.Scene, fallbackReason: "missing");
        if (!scene.IsKnown && scene.Reason == "scene-not-loaded")
        {
            return new GameContextSnapshot(
                scene,
                ContextValue.Unknown("scene-not-loaded"),
                0,
                snapshot.CapturedAt);
        }

        if (snapshot.RoomSources < 0)
        {
            return new GameContextSnapshot(
                scene,
                ContextValue.Unknown("invalid-sources"),
                0,
                snapshot.CapturedAt);
        }

        int sources = Math.Min(snapshot.RoomSources, MaxReportedSources);
        ContextValue room;
        if (sources == 0)
        {
            string reason = snapshot.PlayerRoom.IsKnown
                ? "no-source"
                : NormalizeReason(snapshot.PlayerRoom.Reason, fallback: "no-source");
            room = ContextValue.Unknown(reason);
        }
        else if (sources > 1)
        {
            room = ContextValue.Unknown("ambiguous-sources");
        }
        else
        {
            room = RevalidateValue(snapshot.PlayerRoom, fallbackReason: "missing");
        }

        return new GameContextSnapshot(scene, room, sources, snapshot.CapturedAt);
    }

    internal static string Format(GameContextSnapshot? snapshot)
    {
        var validated = Revalidate(snapshot);
        var builder = new StringBuilder(384);
        builder.Append("{\"schema\":\"");
        builder.Append(Schema);
        builder.Append("\",\"dataOnly\":true,\"chapterScene\":");
        AppendContextValue(builder, validated.Scene, includeSourceMetadata: false, roomSources: 0);
        builder.Append(",\"playerRoom\":");
        AppendContextValue(builder, validated.PlayerRoom, includeSourceMetadata: true, roomSources: validated.RoomSources);
        builder.Append(",\"unobserved\":[");
        for (int i = 0; i < UnobservedFields.Count; i++)
        {
            if (i > 0) builder.Append(',');
            builder.Append('"');
            builder.Append(UnobservedFields[i]);
            builder.Append('"');
        }
        builder.Append("]}");

        if (builder.Length > MaxOutputLength)
        {
            throw new InvalidOperationException($"Formatted game context exceeded MaxOutputLength ({builder.Length} > {MaxOutputLength}).");
        }

        return builder.ToString();
    }

    private static void AppendContextValue(StringBuilder builder, ContextValue value, bool includeSourceMetadata, int roomSources)
    {
        builder.Append('{');
        if (value.IsKnown)
        {
            builder.Append("\"status\":\"known\",\"raw\":\"");
            AppendEscapedJsonString(builder, value.Raw!);
            builder.Append("\",\"reason\":null");
        }
        else
        {
            builder.Append("\"status\":\"unknown\",\"raw\":null,\"reason\":\"");
            builder.Append(NormalizeReason(value.Reason, fallback: "missing"));
            builder.Append('"');
        }

        if (includeSourceMetadata)
        {
            builder.Append(",\"source\":\"");
            builder.Append(GameContextSnapshot.RoomSource);
            builder.Append("\",\"eligibleSources\":");
            builder.Append(roomSources.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append('}');
    }

    private static ContextValue RevalidateValue(ContextValue value, string fallbackReason)
    {
        if (!value.IsKnown)
        {
            return ContextValue.Unknown(NormalizeReason(value.Reason, fallbackReason));
        }

        string? raw = value.Raw;
        if (raw is null) return ContextValue.Unknown("missing");
        if (string.IsNullOrWhiteSpace(raw)) return ContextValue.Unknown("empty");
        if (raw.Length > GameContextRules.MaxRawLength || HasInvalidCharacters(raw))
        {
            return ContextValue.Unknown("invalid");
        }

        return ContextValue.Known(raw);
    }

    internal static string NormalizeReason(string? reason, string fallback = "missing")
    {
        if (string.IsNullOrWhiteSpace(reason)) return fallback;
        if (reason.Length > MaxReasonLength) return "invalid-reason";
        foreach (char character in reason)
        {
            bool valid = (character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z')
                || (character >= '0' && character <= '9')
                || character is '-' or '_' or '.';
            if (!valid) return "invalid-reason";
        }
        return reason;
    }

    private static bool HasInvalidCharacters(string raw)
    {
        for (int i = 0; i < raw.Length; i++)
        {
            char character = raw[i];
            if (char.IsControl(character) || character is '\u2028' or '\u2029') return true;
            if (char.IsHighSurrogate(character))
            {
                if (i + 1 >= raw.Length || !char.IsLowSurrogate(raw[i + 1])) return true;
                i++;
            }
            else if (char.IsLowSurrogate(character))
            {
                return true;
            }
        }
        return false;
    }

    private static void AppendEscapedJsonString(StringBuilder builder, string raw)
    {
        foreach (char character in raw)
        {
            if (character is '"' or '\\')
            {
                builder.Append('\\').Append(character);
            }
            else
            {
                builder.Append(character);
            }
        }
    }
}
