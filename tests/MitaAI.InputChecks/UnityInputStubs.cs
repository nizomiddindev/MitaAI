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
    internal sealed class Scene { internal bool isLoaded = true; }
    internal sealed class GameObject { internal Scene scene = new(); internal bool activeInHierarchy = true; }
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
