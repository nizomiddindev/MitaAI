using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MitaAI.Plugin;

[BepInPlugin("local.mitaai.probe", "MitaAI Test Plugin", "0.6.2")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;
    internal static ConfigEntry<KeyCode> TestKey = null!;
    internal static ConfigEntry<KeyCode> HistoryKey = null!;
    internal static ConfigEntry<string> TestLine = null!;
    internal static ConfigEntry<float> Duration = null!;
    internal static ConfigEntry<float> InteractionRange = null!;
    internal static ConfigEntry<bool> VoiceEnabled = null!;
    internal static ConfigEntry<string> VoiceProjectDirectory = null!;
    internal static ConfigEntry<float> VoiceVolume = null!;
    internal static ConfigEntry<bool> ContextCaptureEnabled = null!;

    // AI Configuration
    internal static ConfigEntry<AIProvider> AIProviderSetting = null!;
    internal static ConfigEntry<string> AIEndpointUrl = null!;
    internal static ConfigEntry<string> AIModel = null!;
    internal static ConfigEntry<string> AIApiKey = null!;
    internal static ConfigEntry<string> AISystemPrompt = null!;
    internal static ConfigEntry<int> AIMaxTokens = null!;
    internal static ConfigEntry<float> AITemperature = null!;
    internal static ConfigEntry<float> AITimeoutSeconds = null!;

    public override void Load()
    {
        Logger = Log;
        TestKey = Config.Bind("Interaction", "Key", KeyCode.E, "Open the message field when the nearby Mita prompt is available.");
        HistoryKey = Config.Bind("Interaction", "HistoryKey", KeyCode.H, "Open or close the chat history overlay.");
        InteractionRange = Config.Bind("Interaction", "Range", 2f, "Maximum camera-to-model-center distance in Unity units (0.8-3).");
        TestLine = Config.Bind("Test", "Line", "Hello! I'm right here.", "Fixed diagnostic reply; used when AI Provider is set to Fixture.");
        Duration = Config.Bind("Test", "DurationSeconds", 8f, "Caption duration in seconds (2-30).");
        VoiceEnabled = Config.Bind("Voice", "Enabled", true, "Enable the existing local GPU F5 Russian voice. Other languages remain text-only.");
        VoiceProjectDirectory = Config.Bind("Voice", "ProjectDirectory", Path.GetFullPath(Path.Combine(Paths.GameRootPath, "..")), "MitaAI project containing tools/voice-evaluation and local/voice. No automatic downloads.");
        VoiceVolume = Config.Bind("Voice", "Volume", 0.8f, "MitaAI speech volume (0-1). Existing game audio is unchanged.");
        ContextCaptureEnabled = Config.Bind("Diagnostics", "ContextCaptureEnabled", true,
            "Test-build room diagnostics: log scene/room on conversation open or Ctrl+F9 outside input/history. No AI context injection.");

        AIProviderSetting = Config.Bind("AI", "Provider", AIProvider.Groq, "AI provider to use: Groq, Gemini, OpenRouter, LMStudio, Ollama, Custom, Fixture.");
        AIEndpointUrl = Config.Bind("AI", "EndpointUrl", "", "Custom API endpoint URL. Leave empty to use provider default.");
        AIModel = Config.Bind("AI", "Model", "", "Model identifier. Leave empty to use provider default.");
        AIApiKey = Config.Bind("AI", "ApiKey", "", "API key (required for Groq, Gemini, OpenRouter, or authenticated Custom endpoints).");
        AISystemPrompt = Config.Bind("AI", "SystemPrompt", AIProviderDefaults.DefaultSystemPrompt, "System prompt defining Mita's character and response constraints.");
        AIMaxTokens = Config.Bind("AI", "MaxTokens", 120, "Maximum tokens to generate for Mita's reply (10-500).");
        AITemperature = Config.Bind("AI", "Temperature", 0.7f, "Temperature for sampling (0.0 to 2.0).");
        AITimeoutSeconds = Config.Bind("AI", "TimeoutSeconds", 25f, "Timeout in seconds for AI requests (3-120).");

        AddComponent<MitaAITestBehaviour>();
        Log.LogInfo($"MITAAI_PLUGIN_LOADED version=0.6.2 mode=ai-chat provider={AIProviderSetting.Value} interaction=nearby-aimed ui=screen-subtitle voice={VoiceEnabled.Value} contextDiagnostics={ContextCaptureEnabled.Value} questDriver=False");
    }
}

public sealed class MitaAITestBehaviour : MonoBehaviour
{
    private const float ScanInterval = 2f;
    private readonly List<MitaPerson> candidates = new();
    private readonly ComposerState composer = new();
    private readonly PlayerInputLease inputLease = new();
    private readonly NativePresentation native = new();
    private readonly AIChatClient aiClient = new();
    private readonly ConcurrentQueue<AIResponseEnvelope> aiResponses = new();
    private readonly LocalVoiceClient voiceClient = new(message => Plugin.Logger.LogInfo(message));
    private readonly VoicePlayback voicePlayback = new();
    private GameContextCollector? contextCollector;
    private bool voiceUploadChecked;
    private readonly ConcurrentQueue<VoiceResponseEnvelope> voiceResponses = new();
    private CancellationTokenSource? activeVoiceCts;
    private VoiceStamp? voiceStamp;
    private VoiceAudio? waitingAudio;
    private long currentRequestId;
    private CancellationTokenSource? activeRequestCts;
    private MitaPerson? target;
    private PlayerMove? boundPlayer;
    private Transform? anchor;
    private Camera? replyCamera;
    private Renderer? captionRenderer;
    private bool headAnchor;
    private TargetMatch? promptMatch;
    private float nextPromptScan;
    private float promptOpacity;
    private float composerOpenedAt;
    private float interactionRequestedUntil;
    private float nextScan;
    private float noticeExpires;
    private string notice = "";
    private string sceneStamp = "";
    private string bindingStamp = "";
    private GUIStyle? noticeStyle;
    private GUIStyle? inputStyle;
    private GUIStyle? keyStyle;
    private GUIStyle? headingStyle;
    private GUIStyle? buttonStyle;
    private GUIStyle? historyHeaderStyle;
    private GUIStyle? historyMessageStyle;
    private GUIStyle? historyHintStyle;
    private bool historyOpen;
    private int historyStartIndex = -1;
    private bool isDraggingHistoryScrollbar;
    private float nextHistoryToggleTime;
    private PlayerMove? historyBoundPlayer;
    private bool historyBeforeEnabled = true;
    private bool historyBeforeStopMouse;
    private bool started;
    private bool failed;

    public MitaAITestBehaviour(IntPtr pointer) : base(pointer) { }

    private sealed record TargetMatch(MitaPerson Mita, Transform Anchor, Renderer Renderer, bool Head, Camera Camera);
    private sealed record AIResponseEnvelope(long RequestId, string SceneStamp, int TargetId, string UserMessage, AIChatResult Result);
    private sealed record VoiceResponseEnvelope(VoiceStamp Stamp, VoiceResult Result);

    public void Update()
    {
        if (failed) return;
        try
        {
            if (!started)
            {
                started = true;
                Plugin.Logger.LogInfo("MITAAI_MAIN_THREAD_READY");
            }
            if (Plugin.VoiceEnabled.Value) voiceClient.EnsureStarted(Plugin.VoiceProjectDirectory.Value);
            else { CancelVoice(); voiceClient.Stop(); }
            if (Plugin.VoiceEnabled.Value && !voiceUploadChecked)
            {
                voiceUploadChecked = true;
                try
                {
                    voicePlayback.VerifyUpload();
                    Plugin.Logger.LogInfo("MITAAI_VOICE_UPLOAD_CHECK passed=True audible=False");
                }
                catch (Exception error)
                {
                    object details = error is System.Runtime.CompilerServices.RuntimeWrappedException wrapped
                        ? wrapped.WrappedException : error;
                    Plugin.Logger.LogWarning($"MITAAI_VOICE_UPLOAD_CHECK passed=False stage={voicePlayback.Stage} error={details}");
                    voicePlayback.Dispose();
                }
            }
            string current = SceneManager.GetActiveScene().handle.ToString();
            if (current != sceneStamp)
            {
                CancelActiveAiRequest();
                ResetBinding(restoreCursor: false);
                native.Reset();
                composer.Clear();
                bool isMenu = SceneManager.GetActiveScene().name.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0;
                aiClient.ResetHistory(clearSessionHistory: isMenu);
                aiResponses.Clear();
                inputLease.DiscardCursorRecovery();
                sceneStamp = current;
                nextScan = 0;
                Plugin.Logger.LogInfo($"MITAAI_SCENE name={SceneManager.GetActiveScene().name}");
            }
            if (native.StoryBusy)
            {
                CancelActiveAiRequest();
            }
            while (aiResponses.TryDequeue(out var envelope))
            {
                if (envelope.RequestId != currentRequestId || envelope.SceneStamp != sceneStamp)
                {
                    Plugin.Logger.LogInfo($"MITAAI_AI_REPLY_DROPPED reason=stale-request-or-scene-changed reqId={envelope.RequestId} activeReqId={currentRequestId}");
                    continue;
                }

                var aiResult = envelope.Result;
                Plugin.Logger.LogInfo($"MITAAI_AI_RESPONSE success={aiResult.Success} durationMs={aiResult.DurationMs} chars={aiResult.Content.Length}");

                if (!aiResult.Success)
                {
                    if (!string.IsNullOrEmpty(aiResult.ErrorMessage))
                    {
                        Plugin.Logger.LogWarning($"MITAAI_AI_ERROR {aiResult.ErrorMessage}");
                    }
                    native.CancelReplyWait();
                    Notify(aiResult.ErrorMessage ?? aiResult.Content);
                    continue;
                }

                if (target != null && envelope.TargetId != 0 && target.GetInstanceID() != envelope.TargetId)
                {
                    Plugin.Logger.LogInfo("MITAAI_AI_REPLY_DROPPED reason=target-character-changed");
                    native.CancelReplyWait();
                    continue;
                }

                float displayDuration = Mathf.Clamp(Plugin.Duration.Value, 2f, 30f);
                if (native.TryShowMitaReply(aiResult.Content, displayDuration))
                {
                    aiClient.AddTurn("user", envelope.UserMessage);
                    aiClient.AddTurn("assistant", aiResult.Content);
                    BeginVoice(aiResult.Content);
                }
                else
                {
                    Plugin.Logger.LogInfo("MITAAI_AI_REPLY_DROPPED reason=presentation-not-ready-or-cancelled");
                }
            }
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + ScanInterval;
                ScanScene();
            }
            if (target != null && (!target.gameObject.activeInHierarchy || !target.gameObject.scene.isLoaded))
                ResetBinding();
            if (!native.StoryBusy && inputLease.TryRecoverCursor()) Plugin.Logger.LogInfo("MITAAI_CURSOR_RECOVERED reason=gameplay-resumed");
            if (composer.IsOpen && (replyCamera == null || !replyCamera.isActiveAndEnabled || inputLease.GameHasTakenControl || native.StoryBusy))
            {
                Plugin.Logger.LogInfo($"MITAAI_INPUT_TAKEOVER {inputLease.TakeoverReason}");
                CloseComposer("game-control-changed", restoreCursor: false, deferCursorRecovery: true);
                native.HideComposer();
            }
            if (!composer.IsOpen && !historyOpen && Time.unscaledTime >= nextPromptScan)
            {
                nextPromptScan = Time.unscaledTime + 0.1f;
                var camera = Camera.main;
                promptMatch = !native.StoryBusy && !native.HasActiveTurn && camera != null && FindPlayer(camera) != null ? FindTarget(camera, false) : null;
            }
            promptOpacity = Mathf.MoveTowards(promptOpacity, !composer.IsOpen && !historyOpen && !native.HasActiveTurn && promptMatch != null ? 1 : 0,
                Time.unscaledDeltaTime / 0.16f);
            native.Tick(composer.IsOpen, composer.DisplayWindow(), Ease(promptOpacity), Plugin.TestKey.Value.ToString(), historyOpen);
            TickVoice();
            if (composer.IsOpen) ReadKeyboard();
            else
            {
                if (Plugin.ContextCaptureEnabled.Value && Input.GetKeyDown(KeyCode.F9)
                    && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                    CaptureContext("ctrl-f9");
                if (Input.GetKeyDown(Plugin.HistoryKey.Value)) ToggleHistory();
                else if (historyOpen && Input.GetKeyDown(KeyCode.Escape)) CloseHistory();
                else if (!historyOpen && promptMatch != null && Input.GetKeyDown(Plugin.TestKey.Value)) RequestInteraction("legacy");
            }
            if (!composer.IsOpen && !historyOpen && interactionRequestedUntil > 0)
            {
                if (Time.unscaledTime <= interactionRequestedUntil) OpenComposer();
                else
                {
                    interactionRequestedUntil = 0;
                    Plugin.Logger.LogInfo("MITAAI_INTERACTION_BLOCKED reason=eligibility-not-restored-within-key-window");
                }
            }
        }
        catch (Exception exception)
        {
            DisableAfterFailure();
            Plugin.Logger.LogError($"MITAAI_PROBE_FAILED {exception}");
        }
    }

    public void LateUpdate()
    {
        if (failed) return;
        try
        {
            if (native.StoryBusy)
            {
                CancelVoice();
                native.Hide();
                if (historyOpen) CloseHistory();
            }
            else
            {
                voicePlayback.LateTick(native.IsPaused || historyOpen, native.StoryBusy);
            }
            if (historyOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (historyBoundPlayer != null && historyBoundPlayer.gameObject.scene.isLoaded)
                {
                    if (historyBoundPlayer.enabled) historyBoundPlayer.enabled = false;
                    if (!historyBoundPlayer.stopMouseMove) historyBoundPlayer.stopMouseMove = true;
                }
            }
            if (!composer.IsOpen) return;
            if (inputLease.GameHasTakenControl || native.StoryBusy)
            {
                Plugin.Logger.LogInfo($"MITAAI_INPUT_TAKEOVER {inputLease.TakeoverReason}");
                CloseComposer("game-control-changed", restoreCursor: false, deferCursorRecovery: true);
                native.HideComposer();
            }
            else inputLease.MaintainCursor();
        }
        catch (Exception exception)
        {
            DisableAfterFailure();
            Plugin.Logger.LogError($"MITAAI_INPUT_UPDATE_FAILED {exception}");
        }
    }

    private void ScanScene()
    {
        candidates.Clear();
        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<MitaPerson>()))
        {
            var mita = item.TryCast<MitaPerson>();
            if (mita == null || !mita.gameObject.scene.IsValid() || !mita.gameObject.scene.isLoaded
                || !mita.gameObject.activeInHierarchy) continue;
            // Menu previews do not establish a gameplay character binding.
            if (mita.gameObject.scene.name.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            candidates.Add(mita);
        }
        string stamp = string.Join(",", candidates.Select(m => m.GetInstanceID()).OrderBy(id => id));
        if (stamp != bindingStamp)
        {
            bindingStamp = stamp;
            Plugin.Logger.LogInfo($"MITAAI_CANDIDATES count={candidates.Count}");
            foreach (var mita in candidates)
                Plugin.Logger.LogInfo($"MITAAI_MITA id={mita.GetInstanceID()} scene={mita.gameObject.scene.name} path={ObjectPath(mita.transform)}");
        }
    }

    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private TargetMatch? FindTarget(Camera camera, bool logAttempt)
    {
        TargetMatch? chosen = null;
        float distance = float.MaxValue;
        float range = Mathf.Clamp(Plugin.InteractionRange.Value, 0.8f, 3f);
        foreach (var mita in candidates)
        {
            if (mita == null || !mita.gameObject.activeInHierarchy || !mita.gameObject.scene.isLoaded) continue;
            var model = mita.animMita != null ? mita.animMita.transform : mita.transform;
            var head = mita.animMita != null && mita.animMita.isHuman
                ? mita.animMita.GetBoneTransform(HumanBodyBones.Head) : null;
            bool visible = false;
            foreach (var component in model.gameObject.GetComponentsInChildren(Il2CppType.Of<Renderer>(), false))
            {
                var renderer = component.TryCast<Renderer>();
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var bounds = renderer.bounds;
                var center = bounds.center;
                float candidateDistance = Vector3.Distance(camera.transform.position, center);
                if (!TargetVisibility.WithinInteractionRange(candidateDistance, range) || candidateDistance >= distance) continue;
                var origin = camera.transform.position;
                var direction = camera.transform.forward;
                var min = bounds.min;
                var max = bounds.max;
                if (!TargetVisibility.TryAimDistanceNearHead(new SpatialPoint(origin.x, origin.y, origin.z),
                    new SpatialPoint(direction.x, direction.y, direction.z),
                    new SpatialPoint(min.x, min.y, min.z), new SpatialPoint(max.x, max.y, max.z),
                    head != null ? new SpatialPoint(head.position.x, head.position.y, head.position.z) : null,
                    out float aimDistance)) continue;
                // Test the aimed surface, not the model's center below a close-up face.
                if (!HasClearView(camera, mita, origin + direction * aimDistance)) continue;
                visible = true;
                distance = candidateDistance;
                chosen = new TargetMatch(mita, head != null ? head : model, renderer, head != null, camera);
            }
            if (logAttempt) Plugin.Logger.LogInfo($"MITAAI_MITA_VISIBILITY id={mita.GetInstanceID()} nearAimedUnobstructed={visible} range={range} camera={ObjectPath(camera.transform)}");
        }
        return chosen;
    }

    private static bool HasClearView(Camera camera, MitaPerson mita, Vector3 point)
    {
        var offset = point - camera.transform.position;
        float distance = offset.magnitude;
        if (distance < 0.01f) return true;
        const int defaultRaycastLayers = ~(1 << 2);
        var playerComponent = camera.GetComponentInParent(Il2CppType.Of<PlayerMove>());
        var player = playerComponent != null ? playerComponent.TryCast<PlayerMove>() : null;
        foreach (var hit in Physics.RaycastAll(camera.transform.position, offset / distance, distance, defaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            var hitTransform = hit.transform;
            if (hitTransform == null || camera.transform.IsChildOf(hitTransform)
                || hitTransform.IsChildOf(mita.transform)) continue;
            if (player != null && hitTransform.IsChildOf(player.transform)) continue;
            return false;
        }
        return true;
    }

    private static PlayerMove? FindPlayer(Camera camera)
    {
        if (!Application.isFocused) return null;
        PlayerMove? chosenPlayer = null;
        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<PlayerMove>()))
        {
            var player = item.TryCast<PlayerMove>();
            if (player == null || !player.gameObject.scene.IsValid() || !player.gameObject.scene.isLoaded
                || !player.isActiveAndEnabled) continue;
            if (!PlayerInputLease.CanAcquire(player)) continue;
            if (!camera.transform.IsChildOf(player.transform)) continue;
            // Do not compete with MiSide's native E interactions on other objects.
            if (player.objectCastInteractive != null || player.objectCastHandInteractive != null) continue;
            if (chosenPlayer != null) return null;
            chosenPlayer = player;
        }
        return chosenPlayer;
    }

    private void OpenComposer()
    {
        if (native.StoryBusy || historyOpen) return;
        var camera = Camera.main;
        var chosenPlayer = camera != null ? FindPlayer(camera) : null;
        var match = camera != null && chosenPlayer != null ? FindTarget(camera, true) : null;
        if (match == null || chosenPlayer == null)
        {
            return;
        }
        interactionRequestedUntil = 0;
        ResetBinding();
        target = match.Mita;
        anchor = match.Anchor;
        replyCamera = match.Camera;
        captionRenderer = match.Renderer;
        headAnchor = match.Head;
        inputLease.Acquire(chosenPlayer);
        boundPlayer = chosenPlayer;
        composer.Open();
        composerOpenedAt = Time.unscaledTime;
        promptMatch = null;
        Plugin.Logger.LogInfo($"MITAAI_INPUT_OPENED mitaId={target!.GetInstanceID()} playerId={chosenPlayer.GetInstanceID()}");
        CaptureContext("conversation-open");
    }

    private void CaptureContext(string trigger)
    {
        if (!Plugin.ContextCaptureEnabled.Value) return;
        try
        {
            // Called only from Update/OpenComposer; capture the owner thread here, not in a field initializer.
            contextCollector ??= new GameContextCollector();
            var snapshot = contextCollector.Capture();
            Plugin.Logger.LogInfo(snapshot.ToLogLine() + $" trigger={trigger} capturedAt={snapshot.CapturedAt.ToString("F3", CultureInfo.InvariantCulture)}");
        }
        catch (Exception error)
        {
            Plugin.Logger.LogWarning("MITAAI_CONTEXT_FAILED error=" + error.GetType().Name);
        }
    }

    private void SubmitMessage()
    {
        if (target == null || boundPlayer == null || !target.gameObject.activeInHierarchy || !target.gameObject.scene.isLoaded
            || replyCamera == null || !replyCamera.isActiveAndEnabled || inputLease.GameHasTakenControl || native.StoryBusy)
        {
            CloseComposer("binding-invalid", restoreCursor: false, deferCursorRecovery: inputLease.GameHasTakenControl || native.StoryBusy);
            native.Hide();
            return;
        }
        if (!composer.TrySubmit(out string message))
        {
            Notify("Enter a message first.");
            return;
        }
        CloseComposer("submitted");
        CancelActiveAiRequest();
        Plugin.Logger.LogInfo($"MITAAI_INPUT_SUBMITTED characters={message.Length} provider={Plugin.AIProviderSetting.Value}");

        float timeout = Mathf.Clamp(Plugin.AITimeoutSeconds.Value, 3f, 120f);
        if (!native.TryBeginPlayerTurn(boundPlayer!, target, replyCamera, message, timeout))
        {
            Notify(native.StoryBusy ? "Wait for the story dialogue to finish, then try again."
                : "Subtitle UI unavailable. Diagnostic recorded.");
            return;
        }

        if (Plugin.AIProviderSetting.Value == AIProvider.Fixture)
        {
            string line = Plugin.TestLine.Value;
            if (line.Length > 240) line = line[..240];
            if (native.TryShowMitaReply(line, Mathf.Clamp(Plugin.Duration.Value, 2, 30)))
            {
                aiClient.AddTurn("user", message);
                aiClient.AddTurn("assistant", line);
                BeginVoice(line);
            }
            return;
        }

        long thisRequestId = currentRequestId;
        string thisScene = sceneStamp;
        int thisTargetId = target != null ? target.GetInstanceID() : 0;

        activeRequestCts?.Cancel();
        activeRequestCts?.Dispose();
        activeRequestCts = new CancellationTokenSource();
        var cts = activeRequestCts;

        var config = new AIChatConfig(
            Plugin.AIProviderSetting.Value,
            Plugin.AIEndpointUrl.Value,
            Plugin.AIModel.Value,
            Plugin.AIApiKey.Value,
            Plugin.AISystemPrompt.Value,
            Plugin.AIMaxTokens.Value,
            Plugin.AITemperature.Value,
            Plugin.AITimeoutSeconds.Value
        );

        Task.Run(async () =>
        {
            try
            {
                var result = await aiClient.SendMessageAsync(message, config, cts.Token).ConfigureAwait(false);
                aiResponses.Enqueue(new AIResponseEnvelope(thisRequestId, thisScene, thisTargetId, message, result));
            }
            catch (OperationCanceledException)
            {
                aiResponses.Enqueue(new AIResponseEnvelope(thisRequestId, thisScene, thisTargetId, message,
                    new AIChatResult(false, "[Request Cancelled]", "Request was cancelled.")));
            }
            catch (Exception ex)
            {
                aiResponses.Enqueue(new AIResponseEnvelope(thisRequestId, thisScene, thisTargetId, message,
                    new AIChatResult(false, $"[AI Error: {ex.Message}]", ex.ToString())));
            }
        });
    }

    private void CloseComposer(string reason, bool restoreCursor = true, bool deferCursorRecovery = false)
    {
        bool wasOpen = composer.IsOpen || inputLease.HasLease;
        composer.Close();
        try { inputLease.Release(restoreCursor, deferCursorRecovery); }
        catch (Exception exception) { Plugin.Logger.LogError($"MITAAI_INPUT_RELEASE_FAILED {exception}"); }
        if (wasOpen) Plugin.Logger.LogInfo($"MITAAI_INPUT_CLOSED reason={reason}");
    }

    public void OnGUI()
    {
        if (failed) return;
        try
        {
            if (!composer.IsOpen && Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == Plugin.HistoryKey.Value)
                {
                    ToggleHistory();
                    Event.current.Use();
                }
                else if (historyOpen && Event.current.keyCode == KeyCode.Escape)
                {
                    CloseHistory();
                    Event.current.Use();
                }
            }
            if (!composer.IsOpen && !historyOpen && (promptMatch != null || promptOpacity > 0.5f) && Event.current.type == EventType.KeyDown
                && Event.current.keyCode == Plugin.TestKey.Value)
            {
                // Native gameplay may consume legacy input before our Update; GUI key events remain available.
                RequestInteraction("gui-event");
                Event.current.Use();
            }
            noticeStyle ??= MakeStyle(18, TextAnchor.MiddleCenter);
            headingStyle ??= MakeStyle(20, TextAnchor.MiddleLeft);
            headingStyle.fontStyle = FontStyle.Bold;
            headingStyle.padding = new RectOffset { left = 12, right = 12, top = 4, bottom = 4 };
            keyStyle ??= MakeStyle(22, TextAnchor.MiddleCenter);
            keyStyle.fontStyle = FontStyle.Bold;
            keyStyle.padding = new RectOffset { left = 2, right = 2, top = 2, bottom = 2 };
            buttonStyle ??= MakeStyle(17, TextAnchor.MiddleCenter);
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.padding = new RectOffset { left = 4, right = 4, top = 2, bottom = 2 };
            if (!native.HasUi)
            {
                if (!composer.IsOpen && !historyOpen && promptOpacity > 0.001f) DrawInteractionPrompt();
                if (composer.IsOpen) DrawComposer();
            }
            if (historyOpen) DrawHistoryOverlay();
            if (Time.unscaledTime < noticeExpires)
            {
                float width = Mathf.Min(440, Screen.width - 32);
                DrawLabel(new Rect((Screen.width - width) / 2, Screen.height - (composer.IsOpen ? 158 : 90), width, 56), notice, noticeStyle);
            }
        }
        catch (Exception exception)
        {
            Plugin.Logger.LogError($"MITAAI_PRESENTATION_FAILED {exception}");
        }
    }

    private void RequestInteraction(string source)
    {
        if (interactionRequestedUntil > 0) return;
        interactionRequestedUntil = Time.unscaledTime + 0.2f;
        Plugin.Logger.LogInfo($"MITAAI_INTERACTION_REQUESTED source={source}");
    }

    private void DrawComposer()
    {
        inputStyle ??= MakeStyle(20, TextAnchor.MiddleLeft);
        inputStyle.wordWrap = false;
        inputStyle.clipping = TextClipping.Clip;
        inputStyle.padding = new RectOffset { left = 8, right = 8, top = 3, bottom = 3 };
        float alpha = Ease(Mathf.Clamp01((Time.unscaledTime - composerOpenedAt) / 0.16f));
        float width = Mathf.Min(620, Screen.width - 32);
        float x = (Screen.width - width) / 2;
        float y = Screen.height - 120 + 8 * (1 - alpha);
        var panel = new Rect(x, y, width, 104);
        DrawPanel(panel, alpha);
        DrawText(new Rect(x + 8, y + 4, width - 56, 36), "Mita", headingStyle!, alpha);
        if (DrawButton(new Rect(x + width - 40, y + 8, 28, 28), "x", alpha, true))
        {
            CloseComposer("cancelled-button");
            return;
        }
        var inputRect = new Rect(x + 16, y + 50, width - 112, 38);
        FillRect(inputRect, new Color(0.12f, 0.04f, 0.13f, 0.95f * alpha));
        FillRect(new Rect(inputRect.x, inputRect.yMax - 2, inputRect.width, 2), new Color(0.94f, 0.44f, 0.77f, alpha));
        DrawText(inputRect, VisibleDraft(inputRect.width - 24), inputStyle, alpha);
        bool previousEnabled = GUI.enabled;
        try
        {
            GUI.enabled = previousEnabled && !string.IsNullOrWhiteSpace(composer.Draft);
            if (DrawButton(new Rect(x + width - 80, y + 50, 64, 38), "Send", alpha, false)) SubmitMessage();
        }
        finally { GUI.enabled = previousEnabled; }
    }

    private void DrawInteractionPrompt()
    {
        float alpha = Ease(promptOpacity);
        float width = Mathf.Min(212, Screen.width - 32);
        float x = (Screen.width - width) / 2;
        float y = Mathf.Min(Screen.height / 2 + 78, Screen.height - 80) + 8 * (1 - alpha);
        DrawPanel(new Rect(x, y, width, 52), alpha);
        FillRect(new Rect(x + 8, y + 7, 38, 38), new Color(0.91f, 0.32f, 0.69f, alpha));
        DrawText(new Rect(x + 8, y + 7, 38, 38), Plugin.TestKey.Value.ToString(), keyStyle!, alpha);
        DrawText(new Rect(x + 50, y + 7, width - 58, 38), "Talk to Mita", buttonStyle!, alpha);
    }

    private bool DrawButton(Rect rect, string text, float alpha, bool quiet)
    {
        bool hover = rect.Contains(Event.current.mousePosition) && GUI.enabled;
        var fill = quiet ? new Color(0.40f, 0.14f, 0.32f, alpha)
            : new Color(hover ? 0.99f : 0.83f, hover ? 0.43f : 0.24f, hover ? 0.78f : 0.62f, alpha);
        if (!GUI.enabled) fill.a *= 0.45f;
        FillRect(rect, fill);
        DrawText(rect, text, buttonStyle!, GUI.enabled ? alpha : alpha * 0.55f);
        return GUI.Button(rect, new GUIContent("") { tooltip = quiet ? "Cancel message" : "Send message" }, buttonStyle!);
    }

    private static float Ease(float value) => value * value * (3 - 2 * value);

    private static void FillRect(Rect rect, Color color)
    {
        var previous = GUI.color;
        try { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); }
        finally { GUI.color = previous; }
    }

    private static void DrawPanel(Rect rect, float alpha)
    {
        var border = new Color(0.91f, 0.39f, 0.73f, 0.85f * alpha);
        FillRounded(rect, border, 8);
        FillRounded(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2),
            new Color(0.23f, 0.07f, 0.21f, 0.94f * alpha), 7);
        FillRect(new Rect(rect.x + 1, rect.y + 8, 2, Mathf.Max(0, rect.height - 16)), new Color(1, 0.55f, 0.83f, alpha));
    }

    private static void FillRounded(Rect rect, Color color, int radius)
    {
        FillRect(new Rect(rect.x, rect.y + radius, rect.width, rect.height - radius * 2), color);
        for (int row = 0; row < radius; row++)
        {
            float offset = radius - row - 0.5f;
            float inset = radius - Mathf.Sqrt(radius * radius - offset * offset);
            FillRect(new Rect(rect.x + inset, rect.y + row, rect.width - 2 * inset, 1), color);
            FillRect(new Rect(rect.x + inset, rect.yMax - row - 1, rect.width - 2 * inset, 1), color);
        }
    }

    private static void DrawText(Rect rect, string text, GUIStyle style, float alpha)
    {
        var previous = GUI.color;
        try { GUI.color = new Color(1, 0.94f, 0.99f, alpha); GUI.Label(rect, text, style); }
        finally { GUI.color = previous; }
    }

    private void ReadKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { CloseComposer("cancelled"); return; }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SubmitMessage();
            return;
        }
        bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (control && Input.GetKeyDown(KeyCode.V))
        {
            string pasted = GUIUtility.systemCopyBuffer ?? "";
            composer.InsertText(pasted.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' '));
        }
        else if (!control && !alt) composer.InsertText(Input.inputString);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) composer.MoveCaret(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) composer.MoveCaret(1);
        if (Input.GetKeyDown(KeyCode.Home)) composer.MoveToEdge(false);
        if (Input.GetKeyDown(KeyCode.End)) composer.MoveToEdge(true);
        if (Input.GetKeyDown(KeyCode.Delete)) composer.DeleteForward();
    }

    private string VisibleDraft(float availableWidth)
    {
        string prefix = composer.Draft[..composer.Caret];
        int start = 0;
        foreach (int boundary in StringInfo.ParseCombiningCharacters(prefix))
        {
            if (inputStyle!.CalcSize(new GUIContent(prefix[start..] + "|")).x <= availableWidth) break;
            start = boundary;
        }
        if (inputStyle!.CalcSize(new GUIContent(prefix[start..] + "|")).x > availableWidth) start = prefix.Length;
        return prefix[start..] + "|" + composer.Draft[composer.Caret..];
    }

    private static GUIStyle MakeStyle(int size, TextAnchor alignment)
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            alignment = alignment,
            wordWrap = true,
            richText = false,
            normal = { textColor = Color.white },
            padding = new RectOffset { left = 12, right = 12, top = 10, bottom = 10 }
        };
    }

    private static void DrawLabel(Rect rect, string text, GUIStyle style)
    {
        DrawPanel(rect, 1);
        DrawText(rect, text, style, 1);
    }

    private void Notify(string text)
    {
        notice = text;
        noticeExpires = Time.unscaledTime + 4;
    }

    private void ResetBinding(bool restoreCursor = true)
    {
        CancelVoice();
        CloseComposer("binding-reset", restoreCursor);
        if (historyOpen) CloseHistory();
        native.Hide();
        target = null;
        boundPlayer = null;
        anchor = null;
        replyCamera = null;
        captionRenderer = null;
        headAnchor = false;
        promptMatch = null;
        nextPromptScan = 0;
        promptOpacity = 0;
        interactionRequestedUntil = 0;
        noticeExpires = 0;
    }

    private static PlayerMove? FindActivePlayer()
    {
        var camera = Camera.main;
        PlayerMove? fallback = null;
        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<PlayerMove>()))
        {
            var player = item.TryCast<PlayerMove>();
            if (player == null || !player.gameObject.scene.IsValid() || !player.gameObject.scene.isLoaded
                || !player.gameObject.activeInHierarchy) continue;
            if (player.gameObject.scene.name.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (camera != null && camera.transform.IsChildOf(player.transform))
                return player;
            fallback ??= player;
        }
        return fallback;
    }

    private void OpenHistory()
    {
        historyOpen = true;
        historyStartIndex = -1;
        var player = boundPlayer ?? FindActivePlayer();
        if (player != null)
        {
            historyBoundPlayer = player;
            historyBeforeEnabled = player.enabled;
            historyBeforeStopMouse = player.stopMouseMove;
            player.stopMouseMove = true;
            player.enabled = false;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Plugin.Logger.LogInfo("MITAAI_HISTORY_OPENED");
    }

    private void CloseHistory()
    {
        if (!historyOpen) return;
        historyOpen = false;
        nextHistoryToggleTime = Time.unscaledTime + 0.2f;
        if (historyBoundPlayer != null)
        {
            if (historyBoundPlayer.gameObject.scene.isLoaded)
            {
                historyBoundPlayer.stopMouseMove = historyBeforeStopMouse;
                historyBoundPlayer.enabled = historyBeforeEnabled;
            }
            historyBoundPlayer = null;
        }
        RestoreCursorAfterHistory();
        Plugin.Logger.LogInfo("MITAAI_HISTORY_CLOSED");
    }

    private void ToggleHistory()
    {
        if (Time.unscaledTime < nextHistoryToggleTime) return;
        nextHistoryToggleTime = Time.unscaledTime + 0.2f;
        if (historyOpen) CloseHistory();
        else OpenHistory();
    }

    private void RestoreCursorAfterHistory()
    {
        if (!native.IsPaused && !composer.IsOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private static int CalculateLatestStartIndex(int count, float[] itemHeights, float availableHeight)
    {
        if (count <= 1) return 0;
        int start = count - 1;
        float accumulated = itemHeights[count - 1];
        while (start > 0)
        {
            if (accumulated + itemHeights[start - 1] > availableHeight)
                break;
            accumulated += itemHeights[start - 1];
            start--;
        }
        return start;
    }

    private static void DrawHistoryPanel(Rect rect, float alpha)
    {
        // Translucent dark magenta/purple pill matching MiSide's subtitle bar
        var fill = new Color(0.18f, 0.04f, 0.17f, 0.62f * alpha);
        // Soft translucent anime pink border
        var border = new Color(0.88f, 0.28f, 0.68f, 0.40f * alpha);
        FillRounded(rect, border, 14);
        FillRounded(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width - 3, rect.height - 3), fill, 13);
        // Soft upper rim light
        FillRect(new Rect(rect.x + 24, rect.y + 1, rect.width - 48, 1.5f), new Color(1f, 0.48f, 0.82f, 0.42f * alpha));
    }

    private void DrawHistoryOverlay()
    {
        try
        {
            var nativeFont = native.NativeFont;

            historyHeaderStyle ??= MakeStyle(14, TextAnchor.MiddleLeft);
            historyHeaderStyle.fontStyle = FontStyle.Bold;
            historyHeaderStyle.richText = true;
            historyHeaderStyle.padding = new RectOffset { left = 4, right = 4, top = 2, bottom = 2 };
            if (nativeFont != null) historyHeaderStyle.font = nativeFont;

            historyMessageStyle ??= MakeStyle(18, TextAnchor.UpperLeft);
            historyMessageStyle.wordWrap = true;
            historyMessageStyle.richText = true;
            historyMessageStyle.clipping = TextClipping.Clip;
            historyMessageStyle.padding = new RectOffset { left = 4, right = 4, top = 2, bottom = 2 };
            if (nativeFont != null) historyMessageStyle.font = nativeFont;

            historyHintStyle ??= MakeStyle(12, TextAnchor.MiddleRight);
            historyHintStyle.fontStyle = FontStyle.Italic;
            historyHintStyle.richText = true;
            historyHintStyle.padding = new RectOffset { left = 4, right = 4, top = 2, bottom = 2 };
            if (nativeFont != null) historyHintStyle.font = nativeFont;

            // Wide horizontal bar positioned at the bottom where subtitles sit
            float width = Mathf.Min(1540f, Screen.width * 0.88f);
            float height = Mathf.Min(240f, Screen.height * 0.28f);
            float x = (Screen.width - width) / 2f;
            float y = Screen.height - height - 34f;
            var panelRect = new Rect(x, y, width, height);

            DrawHistoryPanel(panelRect, 1f);

            // Top bar (Header / Title / Close button)
            DrawText(new Rect(x + 20, y + 6, 160, 24), "<color=#ff9ed8><b>CHAT HISTORY</b></color>", historyHeaderStyle, 1f);

            // Close button at top right
            if (DrawButton(new Rect(x + width - 30, y + 6, 22, 22), "x", 0.9f, true))
            {
                CloseHistory();
                return;
            }

            DrawText(new Rect(x + width - 230, y + 7, 190, 22), "<color=#d9c8e2><i>Press [H] or [Esc] to close</i></color>", historyHintStyle, 0.75f);

            // Thin subtle horizontal divider under header
            FillRect(new Rect(x + 16, y + 30, width - 32, 1), new Color(0.91f, 0.39f, 0.73f, 0.25f));

            var turns = aiClient.SessionHistory;
            float areaX = x + 20;
            float areaY = y + 34;
            float areaWidth = width - 48; // Leaves room on right for the sleek scrollbar
            float areaHeight = height - 44;

            if (turns.Count == 0)
            {
                var emptyStyle = MakeStyle(16, TextAnchor.MiddleCenter);
                if (nativeFont != null) emptyStyle.font = nativeFont;
                DrawText(new Rect(areaX, areaY, areaWidth, areaHeight), "No conversation history yet.\nWalk up to Mita and press [E] to talk!", emptyStyle, 0.7f);
                return;
            }

            // Pre-calculate height of each formatted turn
            var itemHeights = new float[turns.Count];
            var formattedLines = new string[turns.Count];
            for (int i = 0; i < turns.Count; i++)
            {
                bool isUser = turns[i].Role.Equals("user", StringComparison.OrdinalIgnoreCase);
                string speakerTag = isUser ? "<color=#ffa626><b>You: </b></color>" : "<color=#ff6ec7><b>Mita: </b></color>";
                string safeContent = turns[i].Content.Replace("<", "＜").Replace(">", "＞");
                string line = speakerTag + "<color=#faf6fc>" + safeContent + "</color>";
                formattedLines[i] = line;
                float msgH = historyMessageStyle.CalcHeight(new GUIContent(line), areaWidth - 12);
                itemHeights[i] = Mathf.Max(24f, msgH + 6f);
            }

            int maxStartIndex = CalculateLatestStartIndex(turns.Count, itemHeights, areaHeight);
            if (historyStartIndex < 0)
            {
                historyStartIndex = maxStartIndex;
            }
            historyStartIndex = Mathf.Clamp(historyStartIndex, 0, maxStartIndex);

            // Draw visible turns
            float curY = areaY;
            int lastDrawn = historyStartIndex;
            for (int i = historyStartIndex; i < turns.Count; i++)
            {
                float h = itemHeights[i];
                if (curY + h > y + height - 8 && i > historyStartIndex)
                {
                    break;
                }
                lastDrawn = i;

                GUI.Label(new Rect(areaX + 4, curY, areaWidth - 8, h), formattedLines[i], historyMessageStyle);

                // Subtle faint divider line under each dialogue item
                FillRect(new Rect(areaX + 4, curY + h - 2, areaWidth - 8, 1), new Color(1f, 1f, 1f, 0.05f));
                curY += h;
            }

            // Message counter in top header
            string countStatus = $"<color=#e0d0ea>Messages {historyStartIndex + 1}–{lastDrawn + 1} of {turns.Count}</color>";
            DrawText(new Rect(x + 180, y + 7, 240, 22), countStatus, historyHintStyle, 0.75f);

            // ==========================================
            // "THE SCROLLING SIDE THING" (Vertical Scrollbar)
            // ==========================================
            float sbX = x + width - 18;
            float sbY = y + 34;
            float sbWidth = 6;
            float sbHeight = areaHeight;

            // Scroll track
            FillRounded(new Rect(sbX, sbY, sbWidth, sbHeight), new Color(1f, 1f, 1f, 0.08f), 3);

            // Scroll thumb
            int visibleCount = Mathf.Max(1, lastDrawn - historyStartIndex + 1);
            float thumbHeight = Mathf.Clamp(sbHeight * (visibleCount / (float)turns.Count), 24f, sbHeight);
            float ratio = maxStartIndex > 0 ? (historyStartIndex / (float)maxStartIndex) : 1f;
            float thumbY = sbY + ratio * (sbHeight - thumbHeight);
            Color thumbColor = isDraggingHistoryScrollbar
                ? new Color(1f, 0.58f, 0.90f, 0.95f)
                : new Color(0.92f, 0.38f, 0.74f, 0.75f);
            FillRounded(new Rect(sbX, thumbY, sbWidth, thumbHeight), thumbColor, 3);

            // Drag / Click interaction on scrollbar
            var sbHitRect = new Rect(sbX - 8, sbY, sbWidth + 16, sbHeight);
            if (Event.current.type == EventType.MouseDown && sbHitRect.Contains(Event.current.mousePosition))
            {
                isDraggingHistoryScrollbar = true;
                float clickY = Event.current.mousePosition.y - sbY;
                float newRatio = Mathf.Clamp01((clickY - thumbHeight / 2) / Mathf.Max(1f, sbHeight - thumbHeight));
                historyStartIndex = Mathf.RoundToInt(newRatio * maxStartIndex);
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDrag && isDraggingHistoryScrollbar)
            {
                float dragY = Event.current.mousePosition.y - sbY;
                float newRatio = Mathf.Clamp01((dragY - thumbHeight / 2) / Mathf.Max(1f, sbHeight - thumbHeight));
                historyStartIndex = Mathf.RoundToInt(newRatio * maxStartIndex);
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                isDraggingHistoryScrollbar = false;
            }

            // Mouse Wheel scrolling
            if (Event.current.type == EventType.ScrollWheel)
            {
                if (Event.current.delta.y > 0 && historyStartIndex < maxStartIndex)
                {
                    historyStartIndex = Mathf.Min(maxStartIndex, historyStartIndex + 1);
                }
                else if (Event.current.delta.y < 0 && historyStartIndex > 0)
                {
                    historyStartIndex = Mathf.Max(0, historyStartIndex - 1);
                }
                Event.current.Use();
            }
            // Arrow Keys and PageUp/PageDown navigation
            else if (Event.current.type == EventType.KeyDown)
            {
                if ((Event.current.keyCode == KeyCode.UpArrow || Event.current.keyCode == KeyCode.PageUp) && historyStartIndex > 0)
                {
                    historyStartIndex = Mathf.Max(0, historyStartIndex - 1);
                    Event.current.Use();
                }
                else if ((Event.current.keyCode == KeyCode.DownArrow || Event.current.keyCode == KeyCode.PageDown) && historyStartIndex < maxStartIndex)
                {
                    historyStartIndex = Mathf.Min(maxStartIndex, historyStartIndex + 1);
                    Event.current.Use();
                }
                else if (Event.current.keyCode == KeyCode.Home)
                {
                    historyStartIndex = 0;
                    Event.current.Use();
                }
                else if (Event.current.keyCode == KeyCode.End)
                {
                    historyStartIndex = maxStartIndex;
                    Event.current.Use();
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"MITAAI_HISTORY_DRAW_FAILED {ex}");
            CloseHistory();
        }
    }

    private void CancelActiveAiRequest()
    {
        CancelVoice();
        currentRequestId++;
        if (activeRequestCts != null)
        {
            try { activeRequestCts.Cancel(); activeRequestCts.Dispose(); }
            catch { }
            activeRequestCts = null;
        }
    }

    private void BeginVoice(string text)
    {
        CancelVoice();
        if (!Plugin.VoiceEnabled.Value || target == null) return;
        text = SubtitleTurnState.DisplayExcerpt(text);
        if (!text.Any(character => character >= '\u0400' && character <= '\u04ff'))
        {
            Plugin.Logger.LogInfo("MITAAI_VOICE_TEXT_ONLY reason=russian-engine-language");
            return;
        }
        if (!voiceClient.IsReady)
        {
            bool unavailable = voiceClient.State == VoiceWorkerState.Unavailable;
            Plugin.Logger.LogInfo("MITAAI_VOICE_TEXT_ONLY reason=" + (unavailable ? "voice-unavailable" : "voice-warming"));
            Notify(unavailable ? "Voice is unavailable (see log). This reply is text-only."
                : "Voice is warming up. This reply is text-only.");
            return;
        }
        var stamp = new VoiceStamp(currentRequestId, sceneStamp, target.GetInstanceID());
        voiceStamp = stamp;
        activeVoiceCts = new CancellationTokenSource();
        var token = activeVoiceCts.Token;
        native.VoiceHolding = true;
        Plugin.Logger.LogInfo($"MITAAI_VOICE_REQUEST reqId={stamp.RequestId} chars={text.Length}");
        _ = Task.Run(async () =>
        {
            var result = await voiceClient.SynthesizeAsync(text, token).ConfigureAwait(false);
            voiceResponses.Enqueue(new VoiceResponseEnvelope(stamp, result));
        });
    }

    private void TickVoice()
    {
        try
        {
            if (voiceStamp is { } current && (!current.Matches(currentRequestId, sceneStamp,
                target != null ? target.GetInstanceID() : 0) || native.StoryBusy || !native.HasActiveTurn))
            {
                CancelVoice();
                return;
            }
            while (voiceResponses.TryDequeue(out var response))
            {
                if (voiceStamp != response.Stamp || !response.Stamp.Matches(currentRequestId, sceneStamp,
                    target != null ? target.GetInstanceID() : 0)) continue;
                activeVoiceCts?.Dispose();
                activeVoiceCts = null;
                waitingAudio = response.Result.Audio;
                if (waitingAudio == null)
                {
                    Plugin.Logger.LogWarning("MITAAI_VOICE_TEXT_ONLY reason=" + response.Result.Error);
                    CancelVoice();
                }
                else Plugin.Logger.LogInfo($"MITAAI_VOICE_AUDIO_READY generationMs={response.Result.GenerationMs} seconds={waitingAudio.Seconds.ToString("0.00", CultureInfo.InvariantCulture)}");
            }
            bool paused = native.IsPaused || historyOpen;
            if (waitingAudio != null && native.MitaReplyVisible && !paused)
            {
                voicePlayback.Play(waitingAudio, Plugin.VoiceVolume.Value, target, native.StoryBusy);
                waitingAudio = null;
                Plugin.Logger.LogInfo($"MITAAI_VOICE_PLAYING spatial=screen mono=True lipSync={voicePlayback.LipSyncStatus}");
            }
            voicePlayback.Tick(paused, native.StoryBusy);
            if (voiceStamp != null && activeVoiceCts == null && waitingAudio == null && !voicePlayback.Active)
            {
                native.VoiceHolding = false;
                voiceStamp = null;
            }
        }
        catch (Exception error)
        {
            object details = error is System.Runtime.CompilerServices.RuntimeWrappedException wrapped
                ? wrapped.WrappedException : error;
            Plugin.Logger.LogWarning($"MITAAI_VOICE_TEXT_ONLY stage={voicePlayback.Stage} error={details}");
            CancelVoice();
        }
    }

    private void CancelVoice()
    {
        activeVoiceCts?.Cancel();
        activeVoiceCts?.Dispose();
        activeVoiceCts = null;
        voiceStamp = null;
        waitingAudio = null;
        voiceResponses.Clear();
        native.VoiceHolding = false;
        voicePlayback.Stop();
    }

    private void ShutdownVoice(bool permanent = true)
    {
        CancelVoice();
        if (permanent) voiceClient.Dispose();
        else voiceClient.Stop();
        voicePlayback.Dispose();
    }

    // Update/LateUpdate stop running after this, so the GPU worker must be released here.
    private void DisableAfterFailure()
    {
        failed = true;
        FailureCleanup.Run(message => Plugin.Logger.LogError(message),
            ("voice-worker", () => voiceClient.Stop("plugin-failed")),
            ("binding", () => ResetBinding()),
            ("voice-playback", () => { CancelVoice(); voicePlayback.Dispose(); }));
    }

    public void OnDisable() { CancelActiveAiRequest(); ShutdownVoice(permanent: false); ResetBinding(); native.Reset(); inputLease.DiscardCursorRecovery(); }
    public void OnDestroy() { CancelActiveAiRequest(); ShutdownVoice(); ResetBinding(); native.Reset(); aiClient.ResetHistory(clearSessionHistory: true); inputLease.DiscardCursorRecovery(); }
    public void OnApplicationQuit() { CancelActiveAiRequest(); ShutdownVoice(); ResetBinding(); native.Reset(); aiClient.ResetHistory(clearSessionHistory: true); inputLease.DiscardCursorRecovery(); }
    public void OnApplicationFocus(bool focused)
    {
        if (!focused) voicePlayback.Tick(true, native.StoryBusy);
        if (!focused && composer.IsOpen)
        {
            CloseComposer("focus-lost", restoreCursor: false, deferCursorRecovery: true);
            native.HideComposer();
        }
    }

    private static string ObjectPath(Transform transform)
    {
        var names = new List<string>();
        for (var cursor = transform; cursor != null; cursor = cursor.parent) names.Add(cursor.name);
        names.Reverse();
        return string.Join("/", names);
    }
}
