using System.Globalization;

namespace MitaAI.Plugin;

internal sealed class SubtitleTurnState
{
    private string? line;
    private float duration;
    private float due;
    internal bool Pending => line != null;

    internal void Begin(string reply, float showDuration, float now, float delay)
    {
        if (!float.IsFinite(now) || !float.IsFinite(delay) || delay < 0
            || !float.IsFinite(showDuration) || showDuration <= 0 || string.IsNullOrWhiteSpace(reply))
            throw new ArgumentException("Invalid subtitle turn.");
        line = reply;
        duration = showDuration;
        due = now + delay;
    }

    internal bool TryTake(float now, bool bindingReady, out string reply, out float showDuration)
    {
        reply = "";
        showDuration = 0;
        if (!bindingReady) { Cancel(); return false; }
        if (!Pending || !float.IsFinite(now) || now < due) return false;
        reply = line!;
        showDuration = duration;
        Cancel();
        return true;
    }

    internal void PauseTick(float delta)
    {
        if (!Pending || !float.IsFinite(delta) || delta <= 0) return;
        due += delta;
    }

    internal void Cancel() { line = null; duration = 0; due = 0; }

    internal static string DisplayExcerpt(string message)
    {
        var boundaries = StringInfo.ParseCombiningCharacters(message);
        return boundaries.Length <= 240 ? message : message[..boundaries[237]] + "...";
    }
}
