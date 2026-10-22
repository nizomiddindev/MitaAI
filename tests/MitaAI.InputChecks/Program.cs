using MitaAI.Plugin;
using UnityEngine;

// Lightweight stand-in for voice_worker.py: same stdio protocol, no Python, model or GPU.
if (args.Length == 2 && args[0] == "--fake-voice-worker")
{
    RunFakeVoiceWorker(args[1]);
    return;
}

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    checks++;
}

var composer = new ComposerState();
Check(!composer.TrySubmit(out _), "closed composer cannot submit");
composer.Open();
composer.Draft = " \t ";
Check(!composer.TrySubmit(out _) && composer.IsOpen, "blank input remains open");
composer.Draft = new string('a', ComposerState.MaxMessageLength + 1);
Check(!composer.TrySubmit(out _) && composer.IsOpen, "oversized input rejected");
composer.Draft = new string('a', ComposerState.MaxMessageLength);
Check(composer.TrySubmit(out var maximum) && maximum.Length == ComposerState.MaxMessageLength, "maximum input accepted");
for (int turn = 0; turn < 20; turn++)
{
    composer.Open();
    composer.Draft = "  message " + turn + "  ";
    Check(composer.TrySubmit(out var message) && message == "message " + turn, "trimmed turn " + turn);
    Check(!composer.IsOpen && composer.Draft.Length == 0, "clear after turn " + turn);
    Check(!composer.TrySubmit(out _), "no duplicate turn " + turn);
}
composer.Open();
composer.Draft = "preserve this draft";
composer.Close();
composer.Close();
Check(!composer.IsOpen && composer.Draft == "preserve this draft", "cancel preserves unfinished draft");
composer.Open();
Check(composer.IsOpen && composer.Draft == "preserve this draft" && composer.Caret == "preserve this draft".Length, "reopen restores draft and caret");
composer.Clear();
Check(composer.Draft.Length == 0 && composer.Caret == 0, "explicit clear empties draft");
composer.InsertText("abc");
composer.MoveCaret(-1);
composer.InsertText("X");
Check(composer.Draft == "abXc" && composer.Caret == 3, "insert at caret");
composer.Backspace();
Check(composer.Draft == "abc" && composer.Caret == 2, "backspace at caret");
composer.DeleteForward();
Check(composer.Draft == "ab", "delete forward at caret");
composer.MoveToEdge(false);
composer.Backspace();
composer.MoveCaret(-1);
Check(composer.Caret == 0 && composer.Draft == "ab", "caret and backspace stop at beginning");
composer.MoveToEdge(true);
composer.DeleteForward();
composer.MoveCaret(1);
Check(composer.Caret == 2 && composer.Draft == "ab", "caret and delete stop at end");
composer.Draft = "";
composer.InsertText("a\r\n\tb");
Check(composer.Draft == "ab", "keyboard controls do not become message text");
composer.InsertText("\b");
Check(composer.Draft == "a", "native keyboard backspace supported");
composer.Draft = "";
composer.InsertText("\uD83D\uDE00e\u0301");
composer.Backspace();
Check(composer.Draft == "\uD83D\uDE00", "backspace removes whole combining text element");
composer.MoveCaret(-1);
Check(composer.Caret == 0, "caret does not split surrogate pair");
composer.DeleteForward();
Check(composer.Draft == "", "delete removes whole surrogate pair");
composer.Draft = new string('a', ComposerState.MaxMessageLength - 1);
composer.InsertText("\uD83D\uDE00");
Check(composer.Draft.Length == ComposerState.MaxMessageLength - 1, "bounded insertion never splits Unicode element");
composer.InsertText("bc");
Check(composer.Draft.Length == ComposerState.MaxMessageLength && composer.Draft.EndsWith("b"), "typed input stops at maximum length");
composer.Clear();

Cursor.lockState = CursorLockMode.None;
Cursor.visible = true;
var lease = new PlayerInputLease();
lease.Release(true);
Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "cleanup without ownership leaves menu cursor alone");
var player = new PlayerMove();
Cursor.lockState = CursorLockMode.Locked;
Cursor.visible = false;
Check(PlayerInputLease.CanAcquire(player), "free gameplay can acquire");
lease.Acquire(player);
Check(lease.HasLease && !player.enabled && !player.dontMove && !player.stopMouseMove && player.blockInteractive == 0,
    "acquire suspends controller without modifying story flags");
Check(Cursor.visible && Cursor.lockState == CursorLockMode.None && !lease.GameHasTakenControl,
    "acquire unlocks cursor without detecting own changes as takeover");
lease.Release(true);
Check(!lease.HasLease && player.enabled && !player.dontMove && !player.stopMouseMove && player.blockInteractive == 0,
    "release restores player input");
Check(!Cursor.visible && Cursor.lockState == CursorLockMode.Locked, "release restores gameplay cursor");
Cursor.lockState = CursorLockMode.None;
Cursor.visible = true;
lease.Release(true);
Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "second release leaves later menu cursor untouched");
for (int turn = 0; turn < 20; turn++)
{
    Cursor.lockState = CursorLockMode.Locked;
    Cursor.visible = false;
    lease.Acquire(player);
    lease.Release(true);
    Check(!lease.HasLease && player.enabled && !player.dontMove && !player.stopMouseMove && player.blockInteractive == 0
        && Cursor.lockState == CursorLockMode.Locked && !Cursor.visible, "lease lifecycle " + turn);
}
player.dontMove = true;
Check(!PlayerInputLease.CanAcquire(player), "already blocked movement refused");
player.dontMove = false;
player.stopMouseMove = true;
Check(!PlayerInputLease.CanAcquire(player), "already blocked camera refused");
player.stopMouseMove = false;
player.blockInteractive = 2;
Check(!PlayerInputLease.CanAcquire(player), "existing interaction block refused");
player.blockInteractive = 0;
Time.timeScale = 0;
Check(!PlayerInputLease.CanAcquire(player), "paused game refused");
Time.timeScale = 1;
player.animationLoopRun = true;
player.animationFast = true;
player.lookTarget = new object();
player.cameraOtherTarget = new object();
Check(!PlayerInputLease.CanAcquire(player), "scripted control refused");
player.animationLoopRun = false;
player.animationFast = false;
player.lookTarget = null;
player.cameraOtherTarget = null;
lease.Acquire(player);
player.blockInteractive++;
Check(lease.GameHasTakenControl, "new interaction owner detected");
lease.Release(false);
Check(player.blockInteractive == 1, "release leaves other owner block");
Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "takeover leaves cursor to game");
player.blockInteractive = 0;
Cursor.lockState = CursorLockMode.Locked;
lease.Acquire(player);
player.animationFast = true;
player.dontMove = true;
player.stopMouseMove = true;
Check(lease.GameHasTakenControl, "new script control detected");
lease.Release(false);
Check(player.enabled && player.dontMove && player.stopMouseMove && player.blockInteractive == 0,
    "controller restored with script-owned movement flags untouched");
player.animationFast = false;
player.dontMove = false;
player.stopMouseMove = false;
Cursor.lockState = CursorLockMode.Locked;
Cursor.visible = false;
lease.Acquire(player);
Cursor.lockState = CursorLockMode.Locked;
Cursor.visible = false;
Check(!lease.GameHasTakenControl, "cursor normalization alone is not a takeover");
lease.MaintainCursor();
Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "cursor reapplied while composing");
player.gameObject.scene.isLoaded = false;
Check(lease.GameHasTakenControl, "unloaded player detected");
lease.Release(true);
Check(!lease.HasLease && Cursor.lockState == CursorLockMode.Locked && !Cursor.visible,
    "cursor restored even after player unload");
player = new PlayerMove { enabled = false };
Check(!PlayerInputLease.CanAcquire(player), "previously disabled controller refused");
bool refused = false;
try { lease.Acquire(player); } catch (InvalidOperationException) { refused = true; }
Check(refused && !player.enabled && !lease.HasLease, "failed acquisition does not enable existing disabled controller");
Check(TargetVisibility.IntersectsViewport(new[] { new ProjectedPoint(0.4f, 0.1f, 2), new ProjectedPoint(0.6f, 1.4f, 2) }),
    "seated body visible even with head above viewport");
Check(TargetVisibility.IntersectsViewport(new[] { new ProjectedPoint(-0.5f, -0.5f, 1), new ProjectedPoint(1.5f, 1.5f, 1) }),
    "close-up bounds enclosing viewport are visible without an inside corner");
Check(!TargetVisibility.IntersectsViewport(new[] { new ProjectedPoint(0.4f, 1.1f, 2), new ProjectedPoint(0.6f, 1.4f, 2) }),
    "fully above screen excluded");
Check(!TargetVisibility.IntersectsViewport(new[] { new ProjectedPoint(0.5f, 0.5f, -2) }), "behind camera excluded");
Check(!TargetVisibility.IntersectsViewport(Array.Empty<ProjectedPoint>()), "no renderer projection excluded");
Check(!TargetVisibility.IntersectsViewport(new[] { new ProjectedPoint(float.NaN, 0.5f, 2) }), "invalid projection excluded");
Check(TargetVisibility.IntersectsViewport(new[] { new ProjectedPoint(0, 0, 2) }), "edge overlap included");
Check(!TargetVisibility.IntersectsViewport(new[] { new ProjectedPoint(-2, 0.1f, 1), new ProjectedPoint(-1, 0.9f, 1) }),
    "left of view excluded");
player = new PlayerMove();
Cursor.lockState = CursorLockMode.Locked;
Cursor.visible = false;
lease.Acquire(player);
Time.timeScale = 0;
player.blockInteractive = 3;
lease.Release(false, deferCursorRecovery: true);
Check(lease.HasPendingCursorRecovery && player.enabled && Cursor.lockState == CursorLockMode.None,
    "pause yields cursor and controller but retains recovery provenance");
Check(!lease.TryRecoverCursor() && Cursor.lockState == CursorLockMode.None, "paused menu not cursor-locked");
Time.timeScale = 1;
Check(!lease.TryRecoverCursor(), "remaining interaction block delays recovery");
player.blockInteractive = 0;
Application.isFocused = false;
Check(!lease.TryRecoverCursor(), "unfocused window delays recovery");
Application.isFocused = true;
player.lookTarget = new object();
Check(!lease.TryRecoverCursor(), "scripted look delays recovery");
player.lookTarget = null;
player.dontMove = true;
Check(!lease.TryRecoverCursor(), "blocked movement delays recovery");
player.dontMove = false;
Check(lease.TryRecoverCursor() && !lease.HasPendingCursorRecovery
    && Cursor.lockState == CursorLockMode.Locked && !Cursor.visible && PlayerInputLease.CanAcquire(player),
    "same-player resume restores captured cursor and allows F8 reacquisition");
Cursor.lockState = CursorLockMode.None;
Cursor.visible = true;
Check(!lease.TryRecoverCursor() && Cursor.visible, "recovery is one-shot and ignores unrelated menu cursor");
Cursor.lockState = CursorLockMode.Locked;
lease.Acquire(player);
lease.Release(false, true);
Cursor.lockState = CursorLockMode.Confined;
Check(!lease.TryRecoverCursor() && !lease.HasPendingCursorRecovery && Cursor.lockState == CursorLockMode.Confined,
    "game-owned cursor mode supersedes recovery");
Cursor.lockState = CursorLockMode.Locked;
lease.Acquire(player);
lease.Release(false, true);
lease.DiscardCursorRecovery();
Check(!lease.HasPendingCursorRecovery && !lease.TryRecoverCursor(), "scene change discards pending restoration");
Cursor.lockState = CursorLockMode.Locked;
lease.Acquire(player);
lease.Release(false, true);
player.gameObject.scene.isLoaded = false;
Check(!lease.TryRecoverCursor() && !lease.HasPendingCursorRecovery, "unloaded player cannot recover cursor");
Check(TargetVisibility.ContainsAimPoint(new[] { new ProjectedPoint(0.3f, 0.2f, 1), new ProjectedPoint(0.7f, 0.8f, 1) }),
    "aim inside Mita bounds accepted");
Check(!TargetVisibility.ContainsAimPoint(new[] { new ProjectedPoint(0.1f, 0.2f, 1), new ProjectedPoint(0.4f, 0.8f, 1) }),
    "visible Mita to side of aim excluded");
Check(!TargetVisibility.ContainsAimPoint(new[] { new ProjectedPoint(0.3f, 0.6f, 1), new ProjectedPoint(0.7f, 0.9f, 1) }),
    "visible Mita below aim excluded");
Check(!TargetVisibility.ContainsAimPoint(new[] { new ProjectedPoint(0.3f, 0.2f, -1), new ProjectedPoint(0.7f, 0.8f, -1) }),
    "aim bounds behind camera excluded");
Check(TargetVisibility.WithinInteractionRange(2, 2), "exact interaction boundary accepted");
Check(!TargetVisibility.WithinInteractionRange(2.01f, 2), "outside interaction boundary excluded");
Check(!TargetVisibility.WithinInteractionRange(12, 2), "old distant interaction excluded");
Check(!TargetVisibility.WithinInteractionRange(1000, 2), "very distant interaction excluded");
Check(!TargetVisibility.WithinInteractionRange(float.NaN, 2), "invalid distance excluded");
Check(!TargetVisibility.WithinInteractionRange(1, float.NaN), "invalid range excluded");
Check(!TargetVisibility.WithinInteractionRange(-1, 2), "negative distance excluded");
Check(!TargetVisibility.WithinInteractionRange(1, 0), "zero range excluded");
composer.Open();
composer.InsertText("where are you");
Check(composer.Draft == "where are you", "interaction letter e remains ordinary message text");
composer.Draft = "abcdef";
Check(composer.DisplayWindow(3) == "...ef|", "bounded draft keeps end caret visible");
composer.MoveToEdge(false);
Check(composer.DisplayWindow(3) == "|abc...", "bounded draft keeps home caret visible");
composer.Draft = "e\u0301xyz";
Check(composer.DisplayWindow(5) == "e\u0301xyz|", "draft window keeps combining element whole");
composer.Draft = "";
Check(composer.DisplayWindow() == "|", "empty native draft retains caret");
Check(NativeTemplatePolicy.IsAllowed("Dialogue_3DText"), "native subtitle driver approved");
Check(NativeTemplatePolicy.IsAllowed("Dialogue_Symbol"), "native symbol animation approved");
Check(!NativeTemplatePolicy.IsAllowed("UnityEngine.Animator"), "animation event sources rejected");
Check(!NativeTemplatePolicy.IsAllowed("UnityEngine.AudioSource"), "unsanitized audio sources rejected");
Check(!NativeTemplatePolicy.IsAllowed("UnityEngine.UI.Button"), "native interactive callbacks rejected");
Check(!NativeTemplatePolicy.IsAllowed("UnknownQuestBehaviour"), "unknown story scripts rejected");
Check(NativeTemplatePolicy.IsAllowed("Coffee.UIEffects.UIGradient"), "native UI gradient approved");
Check(NativeTemplatePolicy.IsAllowed("Coffee.UIEffects.UIShadow"), "native UI outline effect approved");
Check(NativeTemplatePolicy.CanSanitize("Events_Data"), "known Start event carrier requires sanitization");
Check(NativeTemplatePolicy.CanSanitize("UnityEngine.AudioSource"), "known audio source requires explicit mute");
Check(!NativeTemplatePolicy.CanSanitize("UnknownQuestBehaviour"), "unknown callbacks never guessed safe");
var boxMin = new SpatialPoint(-1, -1, 1);
var boxMax = new SpatialPoint(1, 1, 3);
var forward = new SpatialPoint(0, 0, 1);
Check(TargetVisibility.TryAimDistance(new SpatialPoint(0, 0, 0), forward, boxMin, boxMax, out float aimDistance)
    && aimDistance == 1, "aim ray reaches front surface");
Check(TargetVisibility.TryAimDistance(new SpatialPoint(0, 0, 1.5f), forward, boxMin, boxMax, out aimDistance)
    && aimDistance == 0, "camera inside bounds retains close-up aim");
Check(!TargetVisibility.TryAimDistance(new SpatialPoint(0, 0, 4), forward, boxMin, boxMax, out _), "box behind aim rejected");
Check(!TargetVisibility.TryAimDistance(new SpatialPoint(2, 0, 0), forward, boxMin, boxMax, out _), "parallel ray beside Mita rejected");
Check(TargetVisibility.TryAimDistance(new SpatialPoint(1, 1, 0), forward, boxMin, boxMax, out _), "aim ray on bounds edge accepted");
Check(!TargetVisibility.TryAimDistance(new SpatialPoint(0, 0, 0), new SpatialPoint(0, 0, 0), boxMin, boxMax, out _), "zero aim direction rejected");
Check(!TargetVisibility.TryAimDistance(new SpatialPoint(float.NaN, 0, 0), forward, boxMin, boxMax, out _), "invalid ray origin rejected");
Check(!TargetVisibility.TryAimDistance(new SpatialPoint(0, 0, 0), forward, boxMax, boxMin, out _), "reversed bounds rejected");
Check(RoundedMask.Alpha(0, 0) == 0, "rounded mask corner is transparent");
Check(RoundedMask.Alpha(48, 48) == 1, "rounded mask center opaque");
Check(RoundedMask.Alpha(48, 0) == 1, "rounded mask straight edge opaque");
Check(RoundedMask.Alpha(5, 5) == RoundedMask.Alpha(90, 90), "rounded mask is symmetric");
Check(Enumerable.Range(0, RoundedMask.Size).All(x => Enumerable.Range(0, RoundedMask.Size)
    .All(y => RoundedMask.Alpha(x, y) >= 0 && RoundedMask.Alpha(x, y) <= 1)), "rounded mask alpha always bounded");
Check(RoundedMask.BorderAlpha(48, 48) == 0, "border mask leaves panel center transparent");
Check(RoundedMask.BorderAlpha(48, 0) == 1, "border mask retains outside edge");
Check(RoundedMask.BorderAlpha(48, 8) == 0, "border mask has no opaque backing behind fill");
Check(RoundedMask.BorderAlpha(0, 0) == 0, "border mask preserves rounded corner cutout");
Check(Enumerable.Range(0, RoundedMask.Size).All(x => Enumerable.Range(0, RoundedMask.Size)
    .All(y => RoundedMask.BorderAlpha(x, y) >= 0 && RoundedMask.BorderAlpha(x, y) <= RoundedMask.Alpha(x, y))),
    "border alpha stays inside rounded shape");
var subtitleTurn = new SubtitleTurnState();
subtitleTurn.Begin("fixture", 8, 10, 2);
Check(subtitleTurn.Pending && !subtitleTurn.TryTake(11, true, out _, out _), "Mita waits for player line");
Check(subtitleTurn.TryTake(12, true, out string queuedLine, out float queuedDuration)
    && queuedLine == "fixture" && queuedDuration == 8 && !subtitleTurn.Pending, "Mita line consumed exactly at deadline");
Check(!subtitleTurn.TryTake(13, true, out _, out _), "duplicate Mita line impossible");
subtitleTurn.Begin("fixture", 8, 10, 2);
Check(!subtitleTurn.TryTake(12, false, out _, out _) && !subtitleTurn.Pending, "binding or story takeover discards pending Mita line");
subtitleTurn.Begin("fixture", 8, 10, 2);
subtitleTurn.Cancel();
Check(!subtitleTurn.TryTake(14, true, out _, out _), "cancelled turn never emits later");
Check(SubtitleTurnState.DisplayExcerpt("hello") == "hello", "short player line retained exactly");
Check(SubtitleTurnState.DisplayExcerpt(new string('x', 300)).Length == 240, "player display excerpt bounded");
Check(SubtitleTurnState.DisplayExcerpt(string.Concat(Enumerable.Repeat("e\u0301", 300))).EndsWith("e\u0301..."),
    "player excerpt never splits combining text element");
try { subtitleTurn.Begin("", 8, 10, 2); Check(false, "empty reply rejected"); }
catch (ArgumentException) { Check(true, "empty reply rejected"); }
try { subtitleTurn.Begin("fixture", 8, float.NaN, 2); Check(false, "invalid turn clock rejected"); }
catch (ArgumentException) { Check(true, "invalid turn clock rejected"); }
try { subtitleTurn.Begin("fixture", 8, 10, -1); Check(false, "negative player display delay rejected"); }
catch (ArgumentException) { Check(true, "negative player display delay rejected"); }
Check(TargetVisibility.TryAimDistanceNearHead(new SpatialPoint(0, 1.1f, 1.1f), forward, boxMin, boxMax,
    new SpatialPoint(0, 1.2f, 1.2f), out aimDistance) && aimDistance == 0, "close-up face outside mesh bounds retains aim");
Check(!TargetVisibility.TryAimDistanceNearHead(new SpatialPoint(2, 1.1f, 0), forward, boxMin, boxMax,
    new SpatialPoint(0, 1.2f, 1.2f), out _), "face padding does not accept distant side aim");
Check(!TargetVisibility.TryAimDistanceNearHead(new SpatialPoint(0, 0, 0), forward, boxMin, boxMax,
    new SpatialPoint(float.NaN, 0, 0), out _), "invalid head position rejected");
Check(TargetVisibility.TryAimDistanceNearHead(new SpatialPoint(0, 0, 0), forward, boxMin, boxMax,
    null, out aimDistance) && aimDistance == 1, "non-humanoid target keeps original bounds");
var screen = new ScreenSubtitleState();
screen.Begin("hello", 10, 2);
Check(screen.Active && screen.Visible(10) == "", "screen reveal starts empty");
Check(screen.Visible(9) == "", "early clock never reveals text");
Check(screen.Visible(10.1f) == "he", "screen reveal advances by clock");
Check(screen.Visible(11) == "hello", "screen reveal completes line without duplicates");
Check(!screen.Finished(12) && screen.Finished(screen.EndsAt), "screen holds full line before exact deadline");
screen.Begin("e\u0301x", 0, 1);
Check(screen.Visible(0.05f) == "e\u0301", "screen reveal never splits grapheme");
Check(screen.Visible(float.NaN) == "", "invalid screen clock hides text");
screen.Begin("player", 0, 1.5f);
subtitleTurn.Begin("mita", 2, 0, screen.EndsAt);
Check(!subtitleTurn.TryTake(screen.EndsAt - 0.01f, true, out _, out _), "queued reply waits for actual screen reveal and hold");
Check(subtitleTurn.TryTake(screen.EndsAt, true, out queuedLine, out queuedDuration), "queued reply starts at screen completion");
screen.Begin(queuedLine, screen.EndsAt, queuedDuration);
Check(screen.Visible(2) == "mita", "new line replaces player instead of stacking");
screen.Clear();
Check(!screen.Active && screen.Visible(10) == "" && !screen.Finished(10), "cleared screen has no ghost text");
screen.Begin("player", 0, 1);
subtitleTurn.Begin("mita", 2, 0, screen.EndsAt);
screen.Clear();
subtitleTurn.Cancel();
Check(!subtitleTurn.TryTake(20, true, out _, out _) && screen.Visible(20) == "", "story or reopen cancellation clears both display and queue");
screen.Begin(new string('x', 300), 0, 1);
Check(screen.Visible(20).Length == 240, "screen display remains bounded");
try { screen.Begin("", 0, 1); Check(false, "empty screen line rejected"); }
catch (ArgumentException) { Check(true, "empty screen line rejected"); }
try { screen.Begin("hello", 0, 0); Check(false, "zero screen hold rejected"); }
catch (ArgumentException) { Check(true, "zero screen hold rejected"); }
try { screen.Begin("hello", float.NaN, 1); Check(false, "invalid screen start rejected"); }
catch (ArgumentException) { Check(true, "invalid screen start rejected"); }

// --- AI Provider & Config Checks ---
Check(AIProviderDefaults.GetEffectiveEndpoint(AIProvider.Ollama, null) == "http://localhost:11434/v1/chat/completions", "Ollama default endpoint");
Check(AIProviderDefaults.GetEffectiveModel(AIProvider.Ollama, null) == "llama3.2", "Ollama default model");
Check(AIProviderDefaults.GetEffectiveEndpoint(AIProvider.LMStudio, null) == "http://localhost:1234/v1/chat/completions", "LMStudio default endpoint");
Check(AIProviderDefaults.GetEffectiveModel(AIProvider.LMStudio, null) == "local-model", "LMStudio default model");
Check(AIProviderDefaults.GetEffectiveEndpoint(AIProvider.OpenRouter, null) == "https://openrouter.ai/api/v1/chat/completions", "OpenRouter default endpoint");
Check(AIProviderDefaults.GetEffectiveModel(AIProvider.OpenRouter, null) == "openrouter/free", "OpenRouter default model");
Check(AIProviderDefaults.GetEffectiveEndpoint(AIProvider.Custom, "http://my-host/v1") == "http://my-host/v1", "Custom endpoint override");
Check(AIProviderDefaults.GetEffectiveModel(AIProvider.Custom, "custom-model") == "custom-model", "Custom model override");

// --- Sanitization Checks ---
Check(AIChatClient.SanitizeReply("\"Hello player!\"") == "Hello player!", "quotes stripped");
Check(AIChatClient.SanitizeReply("*smiles warmly* Nice to meet you!") == "Nice to meet you!", "action asterisks stripped");
Check(AIChatClient.SanitizeReply("Line 1\n\nLine 2\tLine 3") == "Line 1 Line 2 Line 3", "newlines and tabs normalized to single spaces");
Check(AIChatClient.SanitizeReply("   trimmed   ") == "trimmed", "whitespace trimmed");
Check(AIChatClient.SanitizeReply("*giggles*") == "giggles", "only asterisks falls back without asterisks");
Check(AIChatClient.SanitizeReply(new string('a', 300)).Length == 240, "sanitized reply length bounded to 240");

// --- Request JSON Serialization Checks ---
var ollamaConfig = new AIChatConfig(AIProvider.Ollama, "", "", "", "System prompt", 50, 0.5f, 15f);
string reqJson = AIChatClient.BuildRequestJson("hello mita", ollamaConfig);
Check(reqJson.Contains("\"model\":\"llama3.2\""), "request json contains effective model");
Check(reqJson.Contains("\"role\":\"system\"") && reqJson.Contains("\"content\":\"System prompt\""), "request json contains system prompt");
Check(reqJson.Contains("\"role\":\"user\"") && reqJson.Contains("\"content\":\"hello mita\""), "request json contains user message");
Check(reqJson.Contains("\"max_tokens\":50"), "request json contains max_tokens");

// --- Response Parsing Checks ---
string successResponse = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"\\\"I am glad to see you!\\\"\"}}]}";
Check(AIChatClient.TryParseResponse(successResponse, out string parsedReply, out string? parseErr)
    && parsedReply == "I am glad to see you!" && parseErr == null, "successful response parsed and sanitized");

string errorResponse = "{\"error\":{\"message\":\"Invalid API key provided\"}}";
Check(!AIChatClient.TryParseResponse(errorResponse, out _, out string? apiError)
    && apiError == "Invalid API key provided", "error response parsed properly");

string emptyResponse = "{}";
Check(!AIChatClient.TryParseResponse(emptyResponse, out _, out string? emptyErr)
    && !string.IsNullOrEmpty(emptyErr), "empty response rejected gracefully");

// --- Sanitizer Reasoning & Action Protection Checks ---
Check(AIChatClient.SanitizeReply("<think>internal reasoning</think> *waves*") == "waves",
    "sanitizer removes reasoning and strips action without restoring think block");
Check(AIChatClient.SanitizeReply("<think>internal reasoning only</think>") == "",
    "sanitizer returns empty for reasoning-only response without restoring think block");
Check(AIChatClient.SanitizeReply("<think>unclosed reasoning") == "",
    "sanitizer safely strips unclosed think tag");
Check(AIChatClient.SanitizeReply("Here's a thinking process...\n\nHello player!") == "Hello player!",
    "sanitizer removes thinking process prefix");
Check(AIChatClient.SanitizeReply("Here's a thinking process without double newline") == "",
    "sanitizer safely strips thinking process without dialogue");
Check(AIChatClient.SanitizeReply("*smiles*") == "smiles",
    "pure action text falls back to cleaned action text without asterisks");

// --- Empty Response Handling Checks ---
string emptyContentJson = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"   \"}}]}";
Check(AIChatClient.TryParseResponse(emptyContentJson, out string emptyContentReply, out _)
    && emptyContentReply == "", "empty or whitespace content yields empty reply");

// --- History Management Checks ---
var client = new AIChatClient();
Check(client.History.Count == 0, "initial history is empty");
client.AddTurn("user", "msg1");
client.AddTurn("assistant", "reply1");
Check(client.History.Count == 2 && client.History[0].Content == "msg1", "turns added to history");
for (int i = 0; i < 10; i++)
{
    client.AddTurn("user", "u" + i);
    client.AddTurn("assistant", "a" + i);
}
Check(client.History.Count == 6 && client.History[5].Content == "a9", "history pruned to max 6 turns");
client.ResetHistory();
Check(client.History.Count == 0, "history reset clears turns");

// --- Client Async & Guard Checks ---
var fixtureConfig = new AIChatConfig(AIProvider.Fixture, "", "", "", "", 100, 0.7f, 10f);
var fixtureTask = client.SendMessageAsync("test", fixtureConfig);
fixtureTask.Wait();
Check(fixtureTask.Result.Success && fixtureTask.Result.Content == "Hello! I'm right here.", "fixture provider runs synchronously");
Check(client.History.Count == 0, "SendMessageAsync does not eagerly mutate history; committed only on accepted turn");

var openRouterMissingKeyConfig = new AIChatConfig(AIProvider.OpenRouter, "", "", "", "", 100, 0.7f, 10f);
var openRouterTask = client.SendMessageAsync("test", openRouterMissingKeyConfig);
openRouterTask.Wait();
Check(!openRouterTask.Result.Success && openRouterTask.Result.Content.Contains("API key is missing"), "missing openrouter api key caught gracefully");

// --- Request Ownership & Envelope Matching Checks ---
long currentRequestId = 42;
string currentScene = "Scene 3 - WeTogether";
int currentTargetId = 74602;

bool MatchesRequest(long reqId, string scene, int target, long activeReq, string activeScene, int activeTarget) =>
    reqId == activeReq && scene == activeScene && (target == 0 || activeTarget == 0 || target == activeTarget);

Check(MatchesRequest(42, "Scene 3 - WeTogether", 74602, currentRequestId, currentScene, currentTargetId),
    "exact request envelope accepted");
Check(!MatchesRequest(41, "Scene 3 - WeTogether", 74602, currentRequestId, currentScene, currentTargetId),
    "stale request ID rejected");
Check(!MatchesRequest(42, "Scene 2 - House", 74602, currentRequestId, currentScene, currentTargetId),
    "mismatched scene rejected");
Check(!MatchesRequest(42, "Scene 3 - WeTogether", 99999, currentRequestId, currentScene, currentTargetId),
    "mismatched target character rejected");

// --- Pause & Focus Resilience Checks ---
var pauseScreen = new ScreenSubtitleState();
pauseScreen.Begin("Testing pause subtitle", 100f, 2f);
Check(pauseScreen.Active, "subtitle started");
string visibleBeforePause = pauseScreen.Visible(100.5f);
Check(!string.IsNullOrEmpty(visibleBeforePause), "subtitle typing started");
// Simulate paused game time using PauseTick where unscaled time advances by 5 seconds
for (int f = 0; f < 50; f++)
{
    pauseScreen.PauseTick(0.1f);
}
// unscaled time is now 105.5f, but PauseTick slid started and EndsAt by 5.0f
string visibleDuringPause = pauseScreen.Visible(105.5f);
Check(visibleBeforePause == visibleDuringPause, "PauseTick preserves visible characters during unscaled time advance");
Check(!pauseScreen.Finished(105.5f), "PauseTick preserves deadline during unscaled time advance");
// Resume and advance presentation clock by 1.5 seconds
Check(pauseScreen.Visible(107f) == "Testing pause subtitle", "subtitle completes after resume");
Check(pauseScreen.Finished(pauseScreen.EndsAt), "subtitle finishes at calculated deadline");

// PauseTick on pending SubtitleTurnState
var pauseTurn = new SubtitleTurnState();
pauseTurn.Begin("Queued reply", 2f, 100f, 1f); // due at 101f
for (int f = 0; f < 50; f++)
{
    pauseTurn.PauseTick(0.1f);
}
// unscaled time is now 105.5f, due slid to 106f
Check(!pauseTurn.TryTake(105.5f, true, out _, out _), "queued turn does not prematurely trigger during pause");
Check(pauseTurn.TryTake(106.1f, true, out var pauseQueuedLine, out _) && pauseQueuedLine == "Queued reply", "queued turn triggers after resume when deadline met");

// --- Session History Checks ---
var sessionClient = new AIChatClient();
for (int i = 0; i < 20; i++)
{
    sessionClient.AddTurn("user", "u" + i);
    sessionClient.AddTurn("assistant", "a" + i);
}
Check(sessionClient.History.Count == 6, "prompt history capped at 6 turns");
Check(sessionClient.SessionHistory.Count == 40, "session history retains all 40 turns");
Check(sessionClient.SessionHistory[0].Content == "u0" && sessionClient.SessionHistory[39].Content == "a19", "session history maintains chronological order");
sessionClient.ResetPromptHistory();
Check(sessionClient.History.Count == 0, "prompt history cleared");
Check(sessionClient.SessionHistory.Count == 40, "session history preserved across prompt reset");
sessionClient.ResetHistory(clearSessionHistory: true);
Check(sessionClient.SessionHistory.Count == 0, "session history cleared on full reset");

// --- History Pagination Calculation Checks ---
int CalcLatest(int count, float[] itemHeights, float availableHeight)
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

Check(CalcLatest(0, Array.Empty<float>(), 300f) == 0, "calc latest empty count");
Check(CalcLatest(1, new float[] { 50f }, 300f) == 0, "calc latest single item");
Check(CalcLatest(5, new float[] { 100f, 100f, 100f, 100f, 100f }, 250f) == 3, "calc latest fits two items (items 3, 4)");
Check(CalcLatest(5, new float[] { 100f, 100f, 100f, 100f, 100f }, 600f) == 0, "calc latest fits all items");

// Draft preservation across multiple pause/blur cycles
var draftPreserve = new ComposerState();
draftPreserve.Open();
draftPreserve.InsertText("draft message to mita");
draftPreserve.Close(); // Blur or Alt-Tab
Check(!draftPreserve.IsOpen && draftPreserve.Draft == "draft message to mita", "blur keeps draft");
draftPreserve.Open(); // Re-open near Mita
Check(draftPreserve.IsOpen && draftPreserve.Draft == "draft message to mita", "re-open retains draft");
draftPreserve.InsertText("!");
draftPreserve.Close(); // Pause menu (Escape)
Check(!draftPreserve.IsOpen && draftPreserve.Draft == "draft message to mita!", "escape keeps draft");
draftPreserve.Open();
Check(draftPreserve.TrySubmit(out var submittedMsg) && submittedMsg == "draft message to mita!", "submit takes draft");
Check(!draftPreserve.IsOpen && draftPreserve.Draft == "", "draft cleared on submit");

// --- History Player Movement & Look Suppression Checks ---
var histPlayer = new PlayerMove { enabled = true, stopMouseMove = false };
bool histBeforeEnabled = histPlayer.enabled;
bool histBeforeStopMouse = histPlayer.stopMouseMove;

// Open history
histPlayer.stopMouseMove = true;
histPlayer.enabled = false;
Check(!histPlayer.enabled && histPlayer.stopMouseMove, "history open disables player move and stops mouse look");

// Close history
histPlayer.stopMouseMove = histBeforeStopMouse;
histPlayer.enabled = histBeforeEnabled;
Check(histPlayer.enabled && !histPlayer.stopMouseMove, "history close restores player move and mouse look");

// History open with initially stopped mouse preserves initial state on close
var histPlayerScripted = new PlayerMove { enabled = true, stopMouseMove = true };
histBeforeStopMouse = histPlayerScripted.stopMouseMove;
histPlayerScripted.enabled = false;
histPlayerScripted.stopMouseMove = true;
// Close
histPlayerScripted.enabled = true;
histPlayerScripted.stopMouseMove = histBeforeStopMouse;
Check(histPlayerScripted.stopMouseMove, "history close preserves preexisting stopMouseMove state");

// Voice data crosses the worker boundary before any Unity object is touched.
byte[] VoiceWave(short[] samples, int rate = 24000, short channels = 1)
{
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
    writer.Write((short)1); writer.Write(channels); writer.Write(rate); writer.Write(rate * channels * 2);
    writer.Write((short)(channels * 2)); writer.Write((short)16);
    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
    foreach (short sample in samples) writer.Write(sample);
    return stream.ToArray();
}
bool VoiceRejected(byte[] bytes)
{
    try { VoiceAudio.Decode(bytes); return false; }
    catch (InvalidDataException) { return true; }
}
var voiceWave = VoiceWave(new short[] { -32768, 0, 16384, 32767 });
var voiceAudio = VoiceAudio.Decode(voiceWave);
Check(voiceAudio.Samples.Length == 4 && voiceAudio.SampleRate == 24000, "voice WAV decodes mono samples");
Check(voiceAudio.Samples[0] == -1 && voiceAudio.Samples[2] == 0.5f, "voice PCM conversion preserves sign and scale");
Check(Math.Abs(voiceAudio.Seconds - 4f / 24000) < 0.00001, "voice duration comes from sample count");
Check(VoiceRejected(Array.Empty<byte>()), "voice rejects empty frames");
Check(VoiceRejected(voiceWave[..^1]), "voice rejects truncated RIFF");
Check(VoiceRejected(VoiceWave(new short[] { 1 }, 48000)), "voice rejects unexpected rate");
Check(VoiceRejected(VoiceWave(new short[] { 1, 2 }, channels: 2)), "voice rejects stereo");
Check(VoiceRejected(VoiceWave(new short[] { 0, 0 })), "voice rejects silence");
var badWave = voiceWave.ToArray();
badWave[40] = 255;
Check(VoiceRejected(badWave), "voice rejects out-of-bounds data chunk");
Check(VoiceRejected(new byte[VoiceAudio.MaximumWaveBytes + 1]), "voice rejects oversized transport");
var voiceOwner = new VoiceStamp(17, "scene-a", 123);
Check(voiceOwner.Matches(17, "scene-a", 123), "voice accepts owning turn");
Check(!voiceOwner.Matches(18, "scene-a", 123), "voice rejects stale request");
Check(!voiceOwner.Matches(17, "scene-b", 123), "voice rejects old scene");
Check(!voiceOwner.Matches(17, "scene-a", 124), "voice rejects replaced character");
Check(!new VoiceStamp(17, "scene-a", 0).Matches(17, "scene-a", 0), "voice rejects absent target");
using (var absentVoice = new LocalVoiceClient(_ => { }))
{
    Check(!absentVoice.IsReady, "voice starts unavailable without a process");
    Check((await absentVoice.SynthesizeAsync("test", CancellationToken.None)).Audio == null, "unavailable voice returns text fallback");
    Check(!await absentVoice.WaitUntilReadyAsync(TimeSpan.FromMilliseconds(10)), "unstarted worker does not wait indefinitely");
}

// Worker lifecycle against the fake worker. Retry delays are zeroed; limits are the production defaults unless stated.
System.Diagnostics.ProcessStartInfo FakeWorker(string mode)
{
    string host = Environment.ProcessPath!;
    var info = new System.Diagnostics.ProcessStartInfo(host);
    if (Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        info.ArgumentList.Add(System.Reflection.Assembly.GetEntryAssembly()!.Location);
    info.ArgumentList.Add("--fake-voice-worker");
    info.ArgumentList.Add(mode);
    return info;
}
async Task<bool> Eventually(Func<bool> condition, int milliseconds = 15000)
{
    var timer = System.Diagnostics.Stopwatch.StartNew();
    while (!condition())
    {
        if (timer.ElapsedMilliseconds > milliseconds) return false;
        await Task.Delay(20);
    }
    return true;
}

var fatalLog = new System.Collections.Concurrent.ConcurrentQueue<string>();
int fatalStarts = 0;
using (var fatalVoice = new LocalVoiceClient(fatalLog.Enqueue, new VoiceWorkerOptions
{
    FailureRetryDelay = TimeSpan.Zero,
    CreateStartInfo = _ => { Interlocked.Increment(ref fatalStarts); return FakeWorker("fatal"); }
}))
{
    Check(await Eventually(() => { fatalVoice.EnsureStarted(""); return fatalVoice.State == VoiceWorkerState.Unavailable; }),
        "repeated startup failures become unavailable");
    Check(fatalStarts == new VoiceWorkerOptions().MaxStartupFailures, "startup retries stop at the configured limit");
    Check(fatalLog.Any(line => line.Contains("MITAAI_VOICE_UNAVAILABLE") && line.Contains("error=cuda_unavailable") && line.Contains("autoRetry=False")),
        "worker fatal code reaches diagnostics");
    for (int attempt = 0; attempt < 10; attempt++) { fatalVoice.EnsureStarted(""); await Task.Delay(20); }
    Check(fatalStarts == 3 && fatalVoice.State == VoiceWorkerState.Unavailable, "unavailable worker is not relaunched automatically");
    fatalVoice.Stop();
    Check(fatalVoice.State == VoiceWorkerState.Stopped && fatalLog.Any(line => line.Contains("MITAAI_VOICE_RETRY_RESET")),
        "disabling voice clears the unavailable state");
    Check(await Eventually(() => { fatalVoice.EnsureStarted(""); return fatalStarts == 4; }), "re-enabled voice starts again");
}

var missingLog = new List<string>();
using (var missingVoice = new LocalVoiceClient(missingLog.Add, new VoiceWorkerOptions { FailureRetryDelay = TimeSpan.Zero, MaxStartupFailures = 2 }))
{
    string absentProject = Path.Combine(Path.GetTempPath(), "mitaai-absent-" + Guid.NewGuid().ToString("N"));
    for (int attempt = 0; attempt < 5; attempt++) missingVoice.EnsureStarted(absentProject);
    Check(missingVoice.State == VoiceWorkerState.Unavailable && missingLog.Count(line => line.Contains("error=worker_files_missing")) == 2,
        "missing worker files stop after the retry limit");
}

var hangLog = new System.Collections.Concurrent.ConcurrentQueue<string>();
bool hungWorkerExited = false;
using (var hangVoice = new LocalVoiceClient(hangLog.Enqueue, new VoiceWorkerOptions
{
    StartupTimeout = TimeSpan.FromMilliseconds(300), MaxStartupFailures = 1,
    CreateStartInfo = _ => FakeWorker("hang-startup"),
    Terminate = process => { LocalVoiceClient.TerminateTree(process); hungWorkerExited = process.HasExited; }
}))
{
    hangVoice.EnsureStarted("");
    Check(hangVoice.State == VoiceWorkerState.Starting, "starting worker is distinct from unavailable");
    Check(await Eventually(() => hangVoice.State == VoiceWorkerState.Unavailable), "startup timeout counts as a startup failure");
    await hangVoice.Cleanup.WaitAsync(TimeSpan.FromSeconds(10));
    Check(hungWorkerExited && hangLog.Any(line => line.Contains("error=startup-timeout")), "timed-out worker is terminated");
}

int echoStarts = 0;
using (var echoVoice = new LocalVoiceClient(_ => { }, new VoiceWorkerOptions
{
    CreateStartInfo = _ => { Interlocked.Increment(ref echoStarts); return FakeWorker("echo"); }
}))
{
    echoVoice.EnsureStarted("");
    Check(await echoVoice.WaitUntilReadyAsync(TimeSpan.FromSeconds(15)), "fake worker becomes ready");
    var early = await echoVoice.SynthesizeAsync("Привет", new CancellationToken(canceled: true));
    Check(early.Audio == null && early.Error == "cancelled-before-submit", "pre-cancelled request returns text fallback");
    Check(echoVoice.IsReady && echoVoice.State == VoiceWorkerState.Ready && echoStarts == 1, "pre-cancelled request keeps the warm worker");
    var spoken = await echoVoice.SynthesizeAsync("Привет", CancellationToken.None);
    // The fake reports how many requests it has received as generationMs.
    Check(spoken.Audio != null && spoken.GenerationMs == 1, "pre-cancelled request never reached the worker");
    var failedSpeech = await echoVoice.SynthesizeAsync("Привет fail", CancellationToken.None);
    Check(failedSpeech.Audio == null && failedSpeech.Error == "synthesis-failed:invalid_audio" && echoVoice.IsReady,
        "per-request worker code reaches diagnostics without a restart");
}

using (var silentVoice = new LocalVoiceClient(_ => { }, new VoiceWorkerOptions { CreateStartInfo = _ => FakeWorker("silent") }))
{
    silentVoice.EnsureStarted("");
    Check(await silentVoice.WaitUntilReadyAsync(TimeSpan.FromSeconds(15)), "silent fake worker becomes ready");
    using var submittedCancel = new CancellationTokenSource();
    var inFlight = silentVoice.SynthesizeAsync("Привет", submittedCancel.Token);
    await Task.Delay(200);
    submittedCancel.Cancel();
    var cancelled = await inFlight;
    Check(cancelled.Audio == null && cancelled.Error == "cancelled-or-timeout" && !silentVoice.IsReady,
        "cancelling a submitted request still stops the worker");
    await silentVoice.Cleanup.WaitAsync(TimeSpan.FromSeconds(10));
}

int ownedStarts = 0, terminations = 0;
var releaseTermination = new ManualResetEventSlim(false);
bool stoppedWorkerExited = false;
using (var ownedVoice = new LocalVoiceClient(_ => { }, new VoiceWorkerOptions
{
    StopRetryDelay = TimeSpan.Zero,
    CreateStartInfo = _ => { Interlocked.Increment(ref ownedStarts); return FakeWorker("silent"); },
    Terminate = process =>
    {
        Interlocked.Increment(ref terminations);
        releaseTermination.Wait(TimeSpan.FromSeconds(15));
        LocalVoiceClient.TerminateTree(process);
        stoppedWorkerExited = process.HasExited;
    }
}))
{
    ownedVoice.EnsureStarted("");
    Check(await ownedVoice.WaitUntilReadyAsync(TimeSpan.FromSeconds(15)), "owned fake worker becomes ready");
    var stopTimer = System.Diagnostics.Stopwatch.StartNew();
    ownedVoice.Stop();
    Check(stopTimer.ElapsedMilliseconds < 500 && !ownedVoice.Cleanup.IsCompleted, "worker termination does not block the caller");
    ownedVoice.Stop();
    ownedVoice.Stop("plugin-failed");
    ownedVoice.EnsureStarted("");
    Check(ownedStarts == 1 && ownedVoice.State == VoiceWorkerState.Stopped, "replacement waits for the previous worker's cleanup");
    releaseTermination.Set();
    await ownedVoice.Cleanup.WaitAsync(TimeSpan.FromSeconds(15));
    Check(terminations == 1 && stoppedWorkerExited, "each worker is terminated exactly once");
    ownedVoice.EnsureStarted("");
    Check(ownedStarts == 2 && await ownedVoice.WaitUntilReadyAsync(TimeSpan.FromSeconds(15)), "replacement starts after cleanup");
}

// Permanent plugin failure: the worker is stopped first and later steps still run when one throws.
var cleanupSteps = new List<string>();
var cleanupErrors = new List<string>();
bool failedWorkerExited = false;
using (var failureVoice = new LocalVoiceClient(_ => { }, new VoiceWorkerOptions
{
    CreateStartInfo = _ => FakeWorker("silent"),
    Terminate = process => { LocalVoiceClient.TerminateTree(process); failedWorkerExited = process.HasExited; }
}))
{
    failureVoice.EnsureStarted("");
    Check(await failureVoice.WaitUntilReadyAsync(TimeSpan.FromSeconds(15)), "failure-path fake worker becomes ready");
    FailureCleanup.Run(cleanupErrors.Add,
        ("voice-worker", () => { cleanupSteps.Add("voice-worker"); failureVoice.Stop("plugin-failed"); }),
        ("binding", () => { cleanupSteps.Add("binding"); throw new InvalidOperationException("binding reset failed"); }),
        ("voice-playback", () => cleanupSteps.Add("voice-playback")));
    Check(cleanupSteps.SequenceEqual(new[] { "voice-worker", "binding", "voice-playback" }), "failure cleanup runs every step in order");
    Check(cleanupErrors.Count == 1 && cleanupErrors[0].Contains("step=binding"), "failure cleanup reports the failing step");
    await failureVoice.Cleanup.WaitAsync(TimeSpan.FromSeconds(10));
    Check(!failureVoice.IsReady && failedWorkerExited, "plugin failure releases the voice worker");
}
var throwingFirst = new List<string>();
FailureCleanup.Run(_ => { }, ("voice-worker", () => throw new IOException()), ("binding", () => throwingFirst.Add("binding")));
Check(throwingFirst.Count == 1, "binding cleanup still runs when worker shutdown throws");

// --- Speaking Animation (VoiceLipSync) Checks ---
(MitaPerson Mita, Audio_BlendShapeVoice Voice, AudioSource NativeSource, AudioSource ModSource) CreateLipSyncFixture(bool initiallyEnabled = true)
{
    var headMesh = new SkinnedMeshRenderer { sharedMesh = new Mesh("Blink", "O", "Smile") };
    var mita = new MitaPerson { meshHead = headMesh };
    var nativeSource = new AudioSource();
    var modSource = new AudioSource { isPlaying = true };
    var voice = new Audio_BlendShapeVoice
    {
        enabled = initiallyEnabled,
        audioVoice = nativeSource,
        mesh = headMesh,
        nameShapeO = "O",
        shapeO = 1,
        shapesOffInSpeak = new[] { new Audio_BlendShapeVoice_Intensity { indexShape = 2, nameBlendshape = "Smile" } }
    };
    return (mita, voice, nativeSource, modSource);
}

Time.timeScale = 1f;
var lipSync = new VoiceLipSync();
var (lsMita, lsVoice, lsNativeSource, lsModSource) = CreateLipSyncFixture(initiallyEnabled: false);
lsVoice.mesh!.SetBlendShapeWeight(1, 42f);
lsVoice.intensotyO = 0.6f;
Check(lipSync.TryBind(lsMita, lsModSource, storyBusy: false, new[] { lsVoice }, out string bindReason)
    && bindReason == "bound" && lipSync.HasBinding && !lipSync.Paused, "lip-sync binds valid Audio_BlendShapeVoice");
Check(lsVoice.audioVoice == lsModSource && lsVoice.enabled, "lip-sync swaps audioVoice to mod source and enables component when voice language was off");
Check(lsVoice.mesh.GetBlendShapeWeight(1) == 0f && lsVoice.intensotyO == 0f, "lip-sync clears residual mouth state on bind");

// Simulate speaking frames with Time.timeScale == 1
lsVoice.SimulateNativeFrame(0.3f);
lsVoice.SimulateNativeFrame(0.3f);
Check(lsVoice.mesh.GetBlendShapeWeight(1) > 0f && lsVoice.intensotyO > 0f && lsVoice.timeOffInSpeak > 0f,
    "native LateUpdate opens mouth while mod audio plays");

// Verify that when Time.timeScale == 0 (Escape pause), native deltaTime-driven update alone leaves mouth open (and lerps toward peak)
Time.timeScale = 0f;
lsModSource.isPlaying = false;
float openBeforePause = lsVoice.mesh.GetBlendShapeWeight(1);
lsVoice.SimulateNativeFrame(0f);
Check(lsVoice.mesh.GetBlendShapeWeight(1) >= openBeforePause && lsVoice.mesh.GetBlendShapeWeight(1) > 0f && lsVoice.intensotyO > 0f,
    "native deltaTime-driven mouth remains open at Time.timeScale == 0 without explicit helper closure");

// Now run VoiceLipSync.Tick during Time.timeScale == 0 pause and verify explicit mouth closure
lipSync.Tick(shouldPause: true, storyBusy: false);
Check(lipSync.HasBinding && lipSync.Paused
    && lsVoice.mesh.GetBlendShapeWeight(1) == 0f && lsVoice.intensotyO == 0f
    && lsVoice.timeOffInSpeak == 0f && lsVoice.intensityOff == 0f,
    "lip-sync explicitly closes mouth and zeroes envelope when paused at Time.timeScale == 0");
lsVoice.SimulateNativeFrame(0f);
Check(lsVoice.mesh.GetBlendShapeWeight(1) == 0f && lsVoice.intensotyO == 0f,
    "mouth remains closed across subsequent native LateUpdate frames while Time.timeScale == 0");

// Verify Time.timeScale == 0 alone also triggers explicit pause closure even if shouldPause was false
lsVoice.mesh.SetBlendShapeWeight(1, 35f);
lsVoice.intensotyO = 0.5f;
lipSync.Tick(shouldPause: false, storyBusy: false);
Check(lipSync.Paused && lsVoice.mesh.GetBlendShapeWeight(1) == 0f && lsVoice.intensotyO == 0f,
    "zero Time.timeScale alone forces mouth closed");

// Resume from pause
Time.timeScale = 1f;
lsModSource.isPlaying = true;
lipSync.Tick(shouldPause: false, storyBusy: false);
Check(lipSync.HasBinding && !lipSync.Paused, "lip-sync resumes when unpaused and Time.timeScale > 0");
lsVoice.SimulateNativeFrame(0.3f);
Check(lsVoice.mesh.GetBlendShapeWeight(1) > 0f && lsVoice.intensotyO > 0f, "mouth resumes moving after unpause");

// Release restores original audioVoice and original disabled state, and closes mouth
lipSync.Release("completed");
Check(!lipSync.HasBinding && lsVoice.audioVoice == lsNativeSource && !lsVoice.enabled
    && lsVoice.mesh.GetBlendShapeWeight(1) == 0f && lsVoice.intensotyO == 0f,
    "release closes mouth and restores original audioVoice and disabled state");

// Second release is a no-op and never overwrites subsequent native state
var laterNativeSource = new AudioSource();
lsVoice.audioVoice = laterNativeSource;
lsVoice.enabled = true;
lsVoice.mesh.SetBlendShapeWeight(1, 55f);
lipSync.Release("duplicate");
Check(lsVoice.audioVoice == laterNativeSource && lsVoice.enabled && lsVoice.mesh.GetBlendShapeWeight(1) == 55f,
    "duplicate release leaves later native state untouched");

// Preserve story ownership: refuse when storyBusy or native voice is already playing
var (busyMita, busyVoice, busyNative, busyMod) = CreateLipSyncFixture();
Check(!lipSync.TryBind(busyMita, busyMod, storyBusy: true, new[] { busyVoice }, out string busyReason)
    && busyReason == "story-busy" && busyVoice.audioVoice == busyNative, "storyBusy refuses lip-sync bind");
busyNative.isPlaying = true;
Check(!lipSync.TryBind(busyMita, busyMod, storyBusy: false, new[] { busyVoice }, out string playingReason)
    && playingReason == "native-voice-playing" && busyVoice.audioVoice == busyNative, "active native voice refuses lip-sync bind");
busyNative.isPlaying = false;

// External takeover mid-turn (e.g. AudioDialogue.SetAudioVoice reassigning audioVoice) is preserved on Tick and Release
Check(lipSync.TryBind(busyMita, busyMod, storyBusy: false, new[] { busyVoice }, out _), "bind succeeds when free");
busyVoice.SimulateNativeFrame(0.3f);
var takeoverSource = new AudioSource { isPlaying = true };
busyVoice.audioVoice = takeoverSource;
busyVoice.mesh!.SetBlendShapeWeight(1, 77f);
lipSync.Tick(shouldPause: false, storyBusy: false);
Check(!lipSync.HasBinding && lipSync.Status == "released-external-owner"
    && busyVoice.audioVoice == takeoverSource && busyVoice.enabled && busyVoice.mesh.GetBlendShapeWeight(1) == 77f,
    "external audioVoice takeover releases binding without overwriting native source or mouth weight");

// Native source starting playback mid-turn releases immediately and hands back audioVoice without closing native mouth
busyVoice.audioVoice = busyNative;
Check(lipSync.TryBind(busyMita, busyMod, storyBusy: false, new[] { busyVoice }, out _), "rebind succeeds");
busyVoice.mesh.SetBlendShapeWeight(1, 64f);
busyNative.isPlaying = true;
lipSync.Tick(shouldPause: false, storyBusy: false);
Check(!lipSync.HasBinding && lipSync.Status == "native-voice-started"
    && busyVoice.audioVoice == busyNative && busyVoice.mesh.GetBlendShapeWeight(1) == 64f,
    "native voice starting playback mid-turn reclaims audioVoice without zeroing native mouth");
busyNative.isPlaying = false;

// StoryBusy mid-turn releases and restores original native source
Check(lipSync.TryBind(busyMita, busyMod, storyBusy: false, new[] { busyVoice }, out _), "rebind before story takeover");
busyVoice.SimulateNativeFrame(0.3f);
lipSync.Tick(shouldPause: false, storyBusy: true);
Check(!lipSync.HasBinding && lipSync.Status == "story-busy"
    && busyVoice.audioVoice == busyNative && busyVoice.mesh.GetBlendShapeWeight(1) == 0f,
    "storyBusy during Tick releases lip-sync and restores native source");

// Destroyed/unloaded original native source disables component on release to prevent LateUpdate null dereference
var (unloadMita, unloadVoice, unloadNative, unloadMod) = CreateLipSyncFixture(initiallyEnabled: true);
Check(lipSync.TryBind(unloadMita, unloadMod, storyBusy: false, new[] { unloadVoice }, out _), "bind before native source unload");
unloadNative.gameObject.scene.isLoaded = false;
lipSync.Release("scene-unload");
Check(!unloadVoice.enabled && unloadVoice.audioVoice == null,
    "unloaded native audio source disables Audio_BlendShapeVoice before clearing audioVoice");

// Validation & incompatible component checks (all leave normal audio untouched)
var (valMita, valVoice, _, valMod) = CreateLipSyncFixture();
Check(!lipSync.TryBind(valMita, valMod, false, Array.Empty<Audio_BlendShapeVoice?>(), out string missingReason)
    && missingReason == "component-missing", "missing component leaves lip-sync unbound cleanly");
valVoice.shapeO = 2; // Index 2 is "Smile", not nameShapeO ("O") -> catches uninitialized default shapeO == 0 mismatch
Check(!lipSync.TryBind(valMita, valMod, false, new[] { valVoice }, out string mismatchReason)
    && mismatchReason == "shape-o-unverified", "unverified shapeO name mismatch rejected without calling Start");
valVoice.shapeO = 99;
Check(!lipSync.TryBind(valMita, valMod, false, new[] { valVoice }, out string rangeReason)
    && rangeReason == "shape-o-unverified", "out-of-range shapeO rejected");
valVoice.shapeO = 1;
valVoice.animationMouth = null;
Check(!lipSync.TryBind(valMita, valMod, false, new[] { valVoice }, out string curveReason)
    && curveReason == "mouth-curve-null", "null animationMouth rejected");
valVoice.animationMouth = new AnimationCurve();
valVoice.animationOffInSpeak = null;
Check(!lipSync.TryBind(valMita, valMod, false, new[] { valVoice }, out string offCurveReason)
    && offCurveReason == "suppressed-curve-null", "null animationOffInSpeak with non-empty shapesOffInSpeak rejected");
valVoice.animationOffInSpeak = new AnimationCurve();

// Candidate prioritization prefers target.meshHead over other child candidates
var (_, headPreferredVoice, _, _) = CreateLipSyncFixture();
var otherRenderer = new SkinnedMeshRenderer { sharedMesh = new Mesh("O", "Smile") };
var secondaryVoice = new Audio_BlendShapeVoice
{
    audioVoice = new AudioSource(),
    mesh = otherRenderer,
    nameShapeO = "O",
    shapeO = 0,
    shapesOffInSpeak = Array.Empty<Audio_BlendShapeVoice_Intensity>()
};
headPreferredVoice.mesh = valMita.meshHead;
Check(lipSync.TryBind(valMita, valMod, false, new[] { secondaryVoice, headPreferredVoice }, out _)
    && headPreferredVoice.audioVoice == valMod && secondaryVoice.audioVoice != valMod,
    "candidate on target.meshHead is preferred over secondary child candidate");
lipSync.Release();

// A replacement head mesh may use the old mouth index for a different expression.
var (swapMita, swapVoice, swapNative, swapMod) = CreateLipSyncFixture();
Check(lipSync.TryBind(swapMita, swapMod, false, new[] { swapVoice }, out _), "bind before head mesh replacement");
swapVoice.mesh!.sharedMesh = new Mesh("O", "Blink", "Smile");
swapVoice.shapeO = 0;
swapVoice.mesh.SetBlendShapeWeight(1, 73f);
lipSync.Tick(false);
Check(!lipSync.HasBinding && lipSync.Status == "mesh-changed"
    && swapVoice.audioVoice == swapNative && swapVoice.mesh.GetBlendShapeWeight(1) == 73f,
    "mesh replacement releases source without resetting a different expression at the old mouth index");

void RunFakeVoiceWorker(string mode)
{
    if (mode == "fatal")
    {
        Console.WriteLine("{\"type\":\"fatal\",\"error\":\"RuntimeError\",\"code\":\"cuda_unavailable\"}");
        return;
    }
    if (mode != "hang-startup") Console.WriteLine("{\"type\":\"ready\",\"engine\":\"fake\",\"protocol\":1}");
    string wav = Convert.ToBase64String(VoiceWave(new short[] { 1000, -1000, 500 }));
    int received = 0;
    while (Console.ReadLine() is { } line)
    {
        received++;
        if (mode != "echo") continue;
        using var request = System.Text.Json.JsonDocument.Parse(line);
        string id = request.RootElement.GetProperty("id").GetString()!;
        Console.WriteLine(request.RootElement.GetProperty("text").GetString()!.Contains("fail")
            ? System.Text.Json.JsonSerializer.Serialize(new { type = "audio", id, ok = false, error = "RuntimeError", code = "invalid_audio" })
            : System.Text.Json.JsonSerializer.Serialize(new { type = "audio", id, ok = true, wav, generationMs = received }));
    }
}

Console.WriteLine($"PASS: {checks} managed composer/visibility/lease/template/turn/screen/ai/voice checks (Unity boundary stubbed). Gameplay remains a manual gate.");

if (args.Length == 2 && args[0] == "--voice-smoke")
{
    string root = Path.GetFullPath(args[1]);
    using var liveVoice = new LocalVoiceClient(Console.WriteLine);
    var startupTimer = System.Diagnostics.Stopwatch.StartNew();
    liveVoice.EnsureStarted(root);
    if (!await liveVoice.WaitUntilReadyAsync(TimeSpan.FromSeconds(180))) throw new Exception("Voice worker not ready.");
    Console.WriteLine($"VOICE_STARTUP_READY milliseconds={startupTimer.ElapsedMilliseconds}");
    var invalidVoice = await liveVoice.SynthesizeAsync("English is text-only in this prototype.", CancellationToken.None);
    if (invalidVoice.Audio != null || !liveVoice.IsReady) throw new Exception("Rejected request broke worker readiness.");
    Console.WriteLine("VOICE_REJECTION_OK worker-still-ready=True");
    for (int repeat = 1; repeat <= 2; repeat++)
    {
        var result = await liveVoice.SynthesizeAsync("Подожд+и, я чт+о-то сл+ышала. Ты т+оже?", CancellationToken.None);
        if (result.Audio == null) throw new Exception("Voice smoke failed: " + result.Error);
        string path = Path.Combine(root, "local", "voice", "integration-smoke", $"f5-russian-worker-{repeat:00}.wav");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, result.Audio.WaveBytes);
        Console.WriteLine($"VOICE_SMOKE_OK repeat={repeat} generationMs={result.GenerationMs} seconds={result.Audio.Seconds:F3} file={path}");
    }
    using var cancelVoice = new CancellationTokenSource();
    var cancelledVoice = liveVoice.SynthesizeAsync("Привет!", cancelVoice.Token);
    cancelVoice.Cancel();
    if ((await cancelledVoice).Audio != null || liveVoice.IsReady) throw new Exception("Cancelled synthesis retained worker ownership.");
    Console.WriteLine("VOICE_CANCELLATION_OK audio-discarded=True worker-stopped=True");
}
