using System.Globalization;

namespace MitaAI.Plugin;

internal sealed class ScreenSubtitleState
{
    private string text = "";
    private int[] elements = Array.Empty<int>();
    private float started;
    internal bool Active { get; private set; }
    internal float EndsAt { get; private set; }
    private const float CharactersPerSecond = 24;

    internal void Begin(string line, float now, float hold)
    {
        if (string.IsNullOrWhiteSpace(line) || !float.IsFinite(now) || !float.IsFinite(hold) || hold <= 0)
            throw new ArgumentException("Invalid screen subtitle.");
        text = SubtitleTurnState.DisplayExcerpt(line);
        elements = StringInfo.ParseCombiningCharacters(text);
        started = now;
        EndsAt = now + elements.Length / CharactersPerSecond + hold;
        if (!float.IsFinite(EndsAt)) { Clear(); throw new ArgumentException("Subtitle deadline overflow."); }
        Active = true;
    }

    internal string Visible(float now)
    {
        if (!Active || !float.IsFinite(now)) return "";
        int count = (int)Math.Clamp(Math.Floor(Math.Max(0, now - started) * CharactersPerSecond), 0, elements.Length);
        return count == elements.Length ? text : text[..elements[count]];
    }

    internal bool Finished(float now) => Active && float.IsFinite(now) && now >= EndsAt;

    internal void PauseTick(float delta)
    {
        if (!Active || !float.IsFinite(delta) || delta <= 0) return;
        started += delta;
        EndsAt += delta;
    }

    internal void Clear()
    {
        Active = false;
        text = "";
        elements = Array.Empty<int>();
        started = 0;
        EndsAt = 0;
    }
}
