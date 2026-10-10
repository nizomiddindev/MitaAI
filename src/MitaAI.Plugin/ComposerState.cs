using System.Globalization;

namespace MitaAI.Plugin;

internal sealed class ComposerState
{
    internal const int MaxMessageLength = 1000;
    internal bool IsOpen { get; private set; }
    private string draft = "";
    internal int Caret { get; private set; }
    internal string Draft
    {
        get => draft;
        set { draft = value; Caret = value.Length; }
    }

    internal void InsertText(string text)
    {
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (element == "\b") { Backspace(); continue; }
            if (element.Any(char.IsControl) || draft.Length + element.Length > MaxMessageLength) continue;
            draft = draft.Insert(Caret, element);
            Caret += element.Length;
        }
    }

    internal void Backspace()
    {
        if (Caret == 0) return;
        int previous = StringInfo.ParseCombiningCharacters(draft).Last(index => index < Caret);
        draft = draft.Remove(previous, Caret - previous);
        Caret = previous;
    }

    internal void DeleteForward()
    {
        if (Caret >= draft.Length) return;
        int next = StringInfo.ParseCombiningCharacters(draft).FirstOrDefault(index => index > Caret, draft.Length);
        draft = draft.Remove(Caret, next - Caret);
    }

    internal void MoveCaret(int direction)
    {
        var boundaries = StringInfo.ParseCombiningCharacters(draft);
        Caret = direction < 0 ? boundaries.LastOrDefault(index => index < Caret, 0)
            : boundaries.FirstOrDefault(index => index > Caret, draft.Length);
    }

    internal void MoveToEdge(bool end) => Caret = end ? draft.Length : 0;

    internal string DisplayWindow(int maxElements = 160)
    {
        maxElements = Math.Max(1, maxElements);
        var boundaries = StringInfo.ParseCombiningCharacters(draft);
        int caretElement = Array.BinarySearch(boundaries, Caret);
        if (caretElement < 0) caretElement = ~caretElement;
        int startElement = Math.Max(0, caretElement - maxElements + 1);
        int start = startElement < boundaries.Length ? boundaries[startElement] : draft.Length;
        int endElement = Math.Min(boundaries.Length, startElement + maxElements);
        int end = endElement < boundaries.Length ? boundaries[endElement] : draft.Length;
        return (start > 0 ? "..." : "") + draft[start..Caret] + "|" + draft[Caret..end] + (end < draft.Length ? "..." : "");
    }

    internal void Open()
    {
        IsOpen = true;
        Caret = draft.Length;
    }

    internal bool TrySubmit(out string message)
    {
        message = "";
        if (!IsOpen) return false;
        var trimmed = Draft.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxMessageLength) return false;
        message = trimmed;
        Clear();
        Close();
        return true;
    }

    internal void Close()
    {
        IsOpen = false;
    }

    internal void Clear()
    {
        draft = "";
        Caret = 0;
    }
}

