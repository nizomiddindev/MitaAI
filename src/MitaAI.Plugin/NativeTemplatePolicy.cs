namespace MitaAI.Plugin;

internal static class NativeTemplatePolicy
{
    // Known visual/letter-physics components only; the two sanitizable types have explicit cleanup.
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "UnityEngine.Transform", "UnityEngine.RectTransform", "UnityEngine.Canvas",
        "UnityEngine.CanvasRenderer", "UnityEngine.CanvasGroup", "UnityEngine.MeshFilter", "UnityEngine.MeshRenderer",
        "UnityEngine.UI.Text", "UnityEngine.UI.Image", "UnityEngine.UI.Shadow", "UnityEngine.UI.Outline",
        "UnityEngine.UI.CanvasScaler",
        "Coffee.UIEffects.UIGradient", "Coffee.UIEffects.UIShadow",
        "UnityEngine.Rigidbody", "UnityEngine.BoxCollider",
        "Dialogue_3DText", "Dialogue_Symbol", "UI_LookOnCamera"
    };

    internal static bool IsAllowed(string type) => Allowed.Contains(type);
    internal static bool CanSanitize(string type) => type is "Events_Data" or "UnityEngine.AudioSource";
}
