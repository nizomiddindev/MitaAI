using UnityEngine;

namespace MitaAI.Plugin;

internal sealed class VoiceLipSync
{
    private MitaPerson? mita;
    private Audio_BlendShapeVoice? voice;
    private AudioSource? modSource;
    private AudioSource? beforeAudioVoice;
    private bool beforeEnabled;
    private bool enabledByLease;
    private int shapeO = -1;

    internal bool HasBinding { get; private set; }
    internal bool Paused { get; private set; }
    internal string Status { get; private set; } = "idle";

    internal static bool IsCompatible(MitaPerson? target, Audio_BlendShapeVoice? candidate, out string reason)
    {
        if (target == null || !IsLoadedAndActive(target.gameObject))
        {
            reason = "target-unavailable";
            return false;
        }
        if (candidate == null || !IsLoadedAndActive(candidate.gameObject))
        {
            reason = "component-unavailable";
            return false;
        }
        var renderer = candidate.mesh;
        if (renderer == null || !renderer.enabled || !IsLoadedAndActive(renderer.gameObject))
        {
            reason = "mesh-renderer-unavailable";
            return false;
        }
        var sharedMesh = renderer.sharedMesh;
        if (sharedMesh == null)
        {
            reason = "shared-mesh-null";
            return false;
        }
        if (candidate.animationMouth == null)
        {
            reason = "mouth-curve-null";
            return false;
        }
        int shapeCount = sharedMesh.blendShapeCount;
        int index = candidate.shapeO;
        if (index < 0 || index >= shapeCount || string.IsNullOrEmpty(candidate.nameShapeO)
            || !string.Equals(sharedMesh.GetBlendShapeName(index), candidate.nameShapeO, StringComparison.Ordinal))
        {
            reason = "shape-o-unverified";
            return false;
        }
        var suppressed = candidate.shapesOffInSpeak;
        if (suppressed != null && suppressed.Length > 0)
        {
            if (candidate.animationOffInSpeak == null)
            {
                reason = "suppressed-curve-null";
                return false;
            }
            for (int i = 0; i < suppressed.Length; i++)
            {
                var entry = suppressed[i];
                if (entry == null || entry.indexShape >= shapeCount)
                {
                    reason = "suppressed-shape-invalid";
                    return false;
                }
            }
        }
        var currentVoice = candidate.audioVoice;
        if (currentVoice == null || !IsLoaded(currentVoice.gameObject))
        {
            reason = "native-audio-source-unavailable";
            return false;
        }
        if (currentVoice.isPlaying)
        {
            reason = "native-voice-playing";
            return false;
        }
        reason = "ok";
        return true;
    }

    internal bool TryBind(MitaPerson? target, AudioSource? source, bool storyBusy,
        IReadOnlyList<Audio_BlendShapeVoice?>? candidates, out string reason)
    {
        Release("rebind");
        if (storyBusy)
        {
            Status = reason = "story-busy";
            return false;
        }
        if (source == null || !IsLoadedAndActive(source.gameObject))
        {
            Status = reason = "mod-source-unavailable";
            return false;
        }
        if (target == null || !IsLoadedAndActive(target.gameObject))
        {
            Status = reason = "target-unavailable";
            return false;
        }
        if (candidates == null || candidates.Count == 0)
        {
            Status = reason = "component-missing";
            return false;
        }

        Audio_BlendShapeVoice? chosen = null;
        string firstFailure = "component-missing";
        var headMesh = target.meshHead;
        if (headMesh != null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || candidate.mesh != headMesh) continue;
                if (IsCompatible(target, candidate, out string headReason))
                {
                    chosen = candidate;
                    break;
                }
                if (firstFailure == "component-missing") firstFailure = headReason;
            }
        }
        if (chosen == null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (IsCompatible(target, candidate, out string candidateReason))
                {
                    chosen = candidate;
                    break;
                }
                if (firstFailure == "component-missing") firstFailure = candidateReason;
            }
        }
        if (chosen == null)
        {
            Status = reason = firstFailure;
            return false;
        }

        mita = target;
        voice = chosen;
        modSource = source;
        beforeAudioVoice = chosen.audioVoice;
        beforeEnabled = chosen.enabled;
        enabledByLease = !beforeEnabled;
        shapeO = chosen.shapeO;
        Paused = Time.timeScale <= 0f;
        HasBinding = true;

        CloseMouth(chosen, shapeO);
        chosen.audioVoice = source;
        if (!chosen.enabled) chosen.enabled = true;
        Status = reason = "bound";
        return true;
    }

    internal void Tick(bool shouldPause, bool storyBusy = false)
    {
        if (!HasBinding) return;
        if (storyBusy)
        {
            Release("story-busy");
            return;
        }
        if (!IsBindingValid(out string invalidReason))
        {
            Release(invalidReason);
            return;
        }
        if (shouldPause || Time.timeScale <= 0f)
        {
            Paused = true;
            // Audio_BlendShapeVoice decays intensotyO and closes shapeO via Time.deltaTime.
            // Explicitly zero the mouth envelope and blendshape weight so the mouth closes
            // immediately even when Time.timeScale is 0, without touching Animator state.
            CloseMouth(voice!, shapeO);
        }
        else if (Paused)
        {
            Paused = false;
        }
    }

    internal void Release(string reason = "stopped")
    {
        if (!HasBinding) return;
        HasBinding = false;
        Paused = false;

        var currentVoice = voice;
        var currentModSource = modSource;
        var previousAudioVoice = beforeAudioVoice;
        bool previousEnabled = beforeEnabled;
        bool wasEnabledByLease = enabledByLease;
        int index = shapeO;

        mita = null;
        voice = null;
        modSource = null;
        beforeAudioVoice = null;
        beforeEnabled = false;
        enabledByLease = false;
        shapeO = -1;
        Status = reason;

        if (currentVoice == null || !IsLoaded(currentVoice.gameObject)) return;

        bool stillOwnsAudioVoice = currentModSource != null && currentVoice.audioVoice == currentModSource;
        if (!stillOwnsAudioVoice)
        {
            Status = "released-external-owner";
            return;
        }

        bool nativeNowPlaying = previousAudioVoice != null
            && IsLoaded(previousAudioVoice.gameObject)
            && previousAudioVoice.isPlaying;
        if (!nativeNowPlaying)
        {
            CloseMouth(currentVoice, index);
        }

        if (previousAudioVoice != null && IsLoaded(previousAudioVoice.gameObject))
        {
            currentVoice.audioVoice = previousAudioVoice;
            if (wasEnabledByLease && currentVoice.enabled && !nativeNowPlaying)
            {
                currentVoice.enabled = previousEnabled;
            }
        }
        else
        {
            // Audio_BlendShapeVoice.LateUpdate dereferences audioVoice without a null check when enabled.
            currentVoice.enabled = false;
            currentVoice.audioVoice = null;
        }
    }

    private bool IsBindingValid(out string reason)
    {
        if (mita == null || !IsLoadedAndActive(mita.gameObject))
        {
            reason = "target-lost";
            return false;
        }
        if (voice == null || !IsLoadedAndActive(voice.gameObject) || !voice.enabled)
        {
            reason = "component-lost";
            return false;
        }
        if (modSource == null || !IsLoadedAndActive(modSource.gameObject))
        {
            reason = "mod-source-lost";
            return false;
        }
        if (voice.audioVoice != modSource)
        {
            reason = "released-external-owner";
            return false;
        }
        if (beforeAudioVoice != null && beforeAudioVoice != modSource
            && IsLoaded(beforeAudioVoice.gameObject) && beforeAudioVoice.isPlaying)
        {
            reason = "native-voice-started";
            return false;
        }
        var renderer = voice.mesh;
        if (renderer == null || !renderer.enabled || !IsLoadedAndActive(renderer.gameObject))
        {
            reason = "mesh-renderer-lost";
            return false;
        }
        var sharedMesh = renderer.sharedMesh;
        if (sharedMesh == null || shapeO < 0 || shapeO >= sharedMesh.blendShapeCount
            || !string.Equals(sharedMesh.GetBlendShapeName(shapeO), voice.nameShapeO, StringComparison.Ordinal))
        {
            reason = "mesh-changed";
            return false;
        }
        reason = "ok";
        return true;
    }

    private static void CloseMouth(Audio_BlendShapeVoice targetVoice, int index)
    {
        targetVoice.intensotyO = 0f;
        targetVoice.timeAnimationFloat = 0f;
        targetVoice.timeOffInSpeak = 0f;
        targetVoice.intensityOff = 0f;
        var renderer = targetVoice.mesh;
        if (renderer == null || !IsLoaded(renderer.gameObject)) return;
        var sharedMesh = renderer.sharedMesh;
        if (sharedMesh == null || index < 0 || index >= sharedMesh.blendShapeCount
            || !string.Equals(sharedMesh.GetBlendShapeName(index), targetVoice.nameShapeO, StringComparison.Ordinal)) return;
        renderer.SetBlendShapeWeight(index, 0f);
    }

    private static bool IsLoaded(GameObject? obj) => obj != null && obj.scene.IsValid() && obj.scene.isLoaded;
    private static bool IsLoadedAndActive(GameObject? obj) => IsLoaded(obj) && obj!.activeInHierarchy;
}
