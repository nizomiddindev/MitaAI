using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MitaAI.Plugin;

internal sealed class VoicePlayback
{
    private readonly VoiceLipSync lipSync = new();
    private GameObject? root;
    private AudioSource? source;
    private AudioClip? clip;
    private bool paused;
    internal bool Active => clip != null;
    internal string Stage { get; private set; } = "idle";
    internal string LipSyncStatus => lipSync.Status;

    internal void VerifyUpload()
    {
        Play(new VoiceAudio(new float[240], 24000, Array.Empty<byte>()), 0);
        Stop();
    }

    internal void Play(VoiceAudio audio, float volume, MitaPerson? target = null, bool storyBusy = false)
    {
        Stage = "reset";
        Stop();
        if (root == null)
        {
            Stage = "create-source";
            root = new GameObject("MitaAI Speech");
            Object.DontDestroyOnLoad(root);
            source = root.AddComponent(Il2CppType.Of<AudioSource>()).TryCast<AudioSource>();
            if (source == null) throw new InvalidOperationException("Voice AudioSource unavailable.");
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
        }
        Stage = "create-clip";
        clip = AudioClip.Create("MitaAI Reply", audio.Samples.Length, 1, audio.SampleRate, false);
        Stage = "upload-samples";
        // The reconstructed instance SetData uses CLR ldlen on an IL2CPP array wrapper.
        // Call the generated native binding with an explicit sample count instead.
        var samples = new Il2CppStructArray<float>(audio.Samples);
        if (clip == null || !AudioClip.SetData(clip, samples, audio.Samples.Length, 0))
            throw new InvalidOperationException("Voice clip upload failed.");
        Stage = "play-source";
        source!.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.Play();
        Stage = "playing";
        if (target != null) TryBindLipSync(target, storyBusy);
    }

    internal void Tick(bool shouldPause, bool storyBusy = false)
    {
        if (!Active || source == null)
        {
            SafeReleaseLipSync("inactive");
            return;
        }
        if (shouldPause && !paused) { source.Pause(); paused = true; }
        else if (!shouldPause && paused) { source.UnPause(); paused = false; }
        SafeTickLipSync(shouldPause || paused, storyBusy);
        if (!shouldPause && !source.isPlaying) Stop();
    }

    internal void LateTick(bool shouldPause, bool storyBusy = false)
    {
        if (!Active || source == null) return;
        SafeTickLipSync(shouldPause || paused, storyBusy);
    }

    internal void Stop()
    {
        SafeReleaseLipSync("stopped");
        if (source != null) { source.Stop(); source.clip = null; }
        if (clip != null) Object.Destroy(clip);
        clip = null;
        paused = false;
    }

    internal void Dispose()
    {
        Stop();
        if (root != null) Object.Destroy(root);
        root = null;
        source = null;
    }

    private void TryBindLipSync(MitaPerson target, bool storyBusy)
    {
        try
        {
            var candidates = DiscoverLipSyncCandidates(target);
            lipSync.TryBind(target, source, storyBusy, candidates, out _);
        }
        catch (Exception error)
        {
            SafeReleaseLipSync("bind-error");
            Plugin.Logger?.LogWarning($"MITAAI_VOICE_LIPSYNC_SKIPPED error={error.GetType().Name}");
        }
    }

    private void SafeTickLipSync(bool shouldPause, bool storyBusy)
    {
        try
        {
            lipSync.Tick(shouldPause, storyBusy);
        }
        catch (Exception error)
        {
            SafeReleaseLipSync("tick-error");
            Plugin.Logger?.LogWarning($"MITAAI_VOICE_LIPSYNC_RELEASED error={error.GetType().Name}");
        }
    }

    private void SafeReleaseLipSync(string reason)
    {
        try
        {
            lipSync.Release(reason);
        }
        catch (Exception error)
        {
            Plugin.Logger?.LogWarning($"MITAAI_VOICE_LIPSYNC_RELEASE_ERROR error={error.GetType().Name}");
        }
    }

    private static List<Audio_BlendShapeVoice?> DiscoverLipSyncCandidates(MitaPerson target)
    {
        var list = new List<Audio_BlendShapeVoice?>();
        if (target.meshHead != null)
        {
            var direct = target.meshHead.GetComponent(Il2CppType.Of<Audio_BlendShapeVoice>())?.TryCast<Audio_BlendShapeVoice>();
            if (direct != null) list.Add(direct);
        }
        var model = target.animMita != null ? target.animMita.gameObject : target.gameObject;
        foreach (var item in model.GetComponentsInChildren(Il2CppType.Of<Audio_BlendShapeVoice>(), false))
        {
            var candidate = item.TryCast<Audio_BlendShapeVoice>();
            if (candidate != null && !list.Contains(candidate)) list.Add(candidate);
        }
        if (model != target.gameObject)
        {
            foreach (var item in target.gameObject.GetComponentsInChildren(Il2CppType.Of<Audio_BlendShapeVoice>(), false))
            {
                var candidate = item.TryCast<Audio_BlendShapeVoice>();
                if (candidate != null && !list.Contains(candidate)) list.Add(candidate);
            }
        }
        return list;
    }
}
