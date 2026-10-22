// Only the native boundary is stubbed. Tests execute the production lease implementation.
namespace UnityEngine
{
    internal enum CursorLockMode { None, Locked, Confined }
    internal static class Cursor
    {
        internal static CursorLockMode lockState;
        internal static bool visible;
    }
    internal static class Time { internal static float timeScale = 1; }
    internal static class Application { internal static bool isFocused = true; }
    internal sealed class Scene
    {
        internal bool isValid = true;
        internal bool isLoaded = true;
        internal bool IsValid() => isValid;
    }
    internal sealed class GameObject { internal Scene scene = new(); internal bool activeInHierarchy = true; }
    internal sealed class AnimationCurve { internal float Evaluate(float time) => 1f; }
    internal sealed class Mesh
    {
        private readonly string[] shapeNames;
        internal Mesh(params string[] names) => shapeNames = names.Length > 0 ? names : new[] { "O", "Smile" };
        internal int blendShapeCount => shapeNames.Length;
        internal string GetBlendShapeName(int index) => shapeNames[index];
    }
    internal sealed class SkinnedMeshRenderer
    {
        private readonly Dictionary<int, float> weights = new();
        internal GameObject gameObject = new();
        internal bool enabled = true;
        internal Mesh? sharedMesh = new("O", "Smile");
        internal float GetBlendShapeWeight(int index) => weights.TryGetValue(index, out float value) ? value : 0f;
        internal void SetBlendShapeWeight(int index, float value) => weights[index] = value;
    }
    internal sealed class AudioSource
    {
        internal GameObject gameObject = new();
        internal bool isPlaying;
    }
}

internal sealed class MitaPerson
{
    internal UnityEngine.GameObject gameObject = new();
    internal UnityEngine.SkinnedMeshRenderer? meshHead;
}

internal sealed class Audio_BlendShapeVoice_Intensity
{
    internal int indexShape;
    internal float intensity = 1f;
    internal string nameBlendshape = "";
}

internal sealed class Audio_BlendShapeVoice
{
    internal UnityEngine.GameObject gameObject = new();
    internal bool enabled = true;
    internal float intensity = 1f;
    internal string nameShapeO = "O";
    internal UnityEngine.AudioSource? audioVoice;
    internal UnityEngine.AnimationCurve? animationMouth = new();
    internal Audio_BlendShapeVoice_Intensity?[]? shapesOffInSpeak = new[]
    {
        new Audio_BlendShapeVoice_Intensity { indexShape = 1, nameBlendshape = "Smile" }
    };
    internal UnityEngine.AnimationCurve? animationOffInSpeak = new();
    internal float intensityOff;
    internal float timeOffInSpeak;
    internal UnityEngine.SkinnedMeshRenderer? mesh;
    internal int shapeO;
    internal float timeAnimationFloat;
    internal float intensotyO;
    internal float randomMaxO = 80f;

    internal void SimulateNativeFrame(float spectrumSample = 0.25f)
    {
        if (!enabled || mesh == null) return;
        float dt = UnityEngine.Time.timeScale > 0f ? 0.016f * UnityEngine.Time.timeScale : 0f;
        if (intensotyO == 0f)
        {
            float w = mesh.GetBlendShapeWeight(shapeO);
            mesh.SetBlendShapeWeight(shapeO, w + (0f - w) * Math.Clamp(dt * 10f, 0f, 1f));
        }
        if (audioVoice!.isPlaying && spectrumSample > 0.01f)
        {
            timeOffInSpeak = 0.4f;
            timeAnimationFloat = (timeAnimationFloat + dt * 5f) % 1f;
            intensotyO = Math.Clamp(intensotyO + dt * 8f, 0f, 1f);
            float current = mesh.GetBlendShapeWeight(shapeO);
            float target = animationMouth!.Evaluate(timeAnimationFloat) * randomMaxO;
            mesh.SetBlendShapeWeight(shapeO, current + (target - current) * Math.Clamp(intensotyO, 0f, 1f));
        }
        else
        {
            timeAnimationFloat = Math.Min(1f, timeAnimationFloat + dt * 5f);
            intensotyO = Math.Max(0f, intensotyO - dt * 8f);
            float current = mesh.GetBlendShapeWeight(shapeO);
            float target = animationMouth!.Evaluate(timeAnimationFloat) * randomMaxO;
            mesh.SetBlendShapeWeight(shapeO, current + (target - current) * Math.Clamp(intensotyO, 0f, 1f));
        }
        if (timeOffInSpeak > 0f)
        {
            timeOffInSpeak = Math.Max(0f, timeOffInSpeak - dt);
            intensityOff = Math.Min(1f, intensityOff + dt * 2f);
        }
        else if (intensityOff > 0f)
        {
            intensityOff = Math.Max(0f, intensityOff - dt * 2f);
        }
    }
}

internal sealed class PlayerMove
{
    internal bool enabled = true;
    internal bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
    internal UnityEngine.GameObject gameObject = new();
    internal bool animationLoopRun;
    internal bool animationFast;
    internal object? cameraOtherTarget;
    internal object? lookTarget;
    internal bool dontMove;
    internal bool stopMouseMove;
    internal int blockInteractive;
}
