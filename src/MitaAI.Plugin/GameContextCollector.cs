using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MitaAI.Plugin;

// Observational only: reads the active scene and Location34_Communication.roomPlayer, never writes game state.
// Construct and call Capture on the Unity main thread, on explicit events only (for example an E interaction).
internal sealed class GameContextCollector
{
    private readonly ContextCaptureGate gate;
    private GameContextSnapshot? last;

    internal GameContextCollector(float minimumIntervalSeconds = 0.5f) => gate = new ContextCaptureGate(minimumIntervalSeconds);

    internal GameContextSnapshot Capture()
    {
        // Checked before any Unity call: Unity APIs are not safe off the main thread.
        if (!gate.OnOwnerThread) return GameContextSnapshot.Unavailable("not-main-thread");
        float now = float.NaN;
        try
        {
            now = Time.unscaledTime;
            var active = SceneManager.GetActiveScene();
            if (!gate.ShouldRefresh(now, active.handle) && last != null) return last;
            bool loaded = active.IsValid() && active.isLoaded;
            var scene = GameContextRules.Scene(loaded, loaded ? active.name : null);
            if (!loaded)
            {
                last = new GameContextSnapshot(scene, ContextValue.Unknown("scene-not-loaded"), 0, now);
                return last;
            }
            var observations = new List<RoomObservation>();
            foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Location34_Communication>()))
            {
                var communication = item.TryCast<Location34_Communication>();
                if (communication == null) continue;
                var owner = communication.gameObject.scene;
                // Prefabs and inactive or other-scene copies are not the player's current room source.
                bool eligible = owner.IsValid() && owner.isLoaded && owner.handle == active.handle && communication.isActiveAndEnabled;
                if (!eligible) { observations.Add(new RoomObservation(null, false)); continue; }
                // An interop field read (NativeFieldInfoPtr_roomPlayer); no game method is invoked.
                try { observations.Add(new RoomObservation(communication.roomPlayer, true)); }
                catch { observations.Add(new RoomObservation(null, true, ReadFailed: true)); }
            }
            var room = GameContextRules.Room(observations, out int sources);
            last = new GameContextSnapshot(scene, room, sources, now);
        }
        catch (Exception error)
        {
            last = GameContextSnapshot.Unavailable("capture-failed-" + error.GetType().Name, now);
        }
        return last;
    }
}
