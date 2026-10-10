# MitaAI Project Inspection

Inspected 6 October 2026 at the user's request. Reviewed project guidance, handoff, implementation model, research notes, runtime plugin and helpers, launcher, build/configuration tools, existing managed checks, installed manifest, and available game logs. No runtime source or installed plugin was changed; no game launch or provider request was performed.

## Verified In This Inspection

- Plugin Release build: zero warnings and zero errors.
- Launcher Release build: zero warnings and zero errors.
- Existing managed checks: 256 pass. They run linked production helpers with stubbed Unity boundaries; they do not run Plugin.Update, NativePresentation, actual IL2CPP rendering, or an HTTP failure/cancellation integration suite.
- Source plugin attribute, project version, startup marker, installed manifest and available startup log all identify 0.5.0. The handoff calls the pause/draft changes 0.5.1, but that version number is not reflected in the current artifact metadata.
- Built DLL, installed DLL and install manifest SHA-256 agree: D8C3791315AA1B0CD52C9736218E54E5A48542058CE56CA4993B4670BF862821.
- Available BepInEx log records gameplay in Scene 3 - WeTogether, native language font selection, E opening, player submission, and a successful Gemini response in 1353 ms. It ends at the response marker, so this log alone does not establish the subsequent visible Mita line or a complete long session.
- Available Unity Player.log is empty. That cannot establish absence of engine errors.
- The user independently reports testing local AI successfully in-game. Earlier local/cloud latency measurements and screenshots in HANDOFF.md are prior reports, not newly reproduced measurements here.
- The workspace is not a Git repository.

## Implemented Features

- BepInEx IL2CPP integration with the existing game and Mita discovery.
- Nearby aimed E interaction, range and collider occlusion checks, native interaction priority.
- Unicode-aware text editing, bounded orange player text and white/pink Mita subtitles.
- Game-selected font assets and copied text effects on a mod-owned screen Canvas. Typewriter/fades are mod animations, not native quest dialogue playback.
- Async OpenAI-compatible provider client: LM Studio, Ollama, OpenRouter, Gemini, Groq, custom endpoints, and deterministic fixture mode.
- Configurable persona, model, token budget, timeout, and temperature.
- Recent history of six messages: normally three player/Mita exchanges, not six exchanges. History is intended to reset on active-scene change and has no persistent storage.
- Windows launcher with provider/persona selection, connection test, configuration save and game launch.
- Draft text preservation and a presentation clock that freezes while paused or unfocused.
- Guarded input ownership and story-dialogue priority.

## Findings

### High: replies lack request and scene ownership

References: src/MitaAI.Plugin/Plugin.cs:103-124 and 364-373; src/MitaAI.Plugin/AIChatClient.cs:354-362.

The background request receives no lifecycle cancellation token. Results contain no request ID, scene epoch or target ID. Scene reset clears presentation/history but neither cancels requests nor invalidates queued results. If an old response arrives after a new turn has begun, TryShowMitaReply can attach it to that new turn. A late successful request also appends its old messages after ResetHistory, contaminating the new scene's conversation. Story cancellation and presentation timeout similarly do not end the HTTP request. Lifecycle shutdown does not cancel it either.

Fix before voice: identify each request and its binding, cancel on invalidation, validate completed results on the main thread, and commit history only for accepted turns. This is a source-supported race condition, not a reproduced gameplay failure in this inspection.

### Medium: failed requests are presented as Mita speech

Reference: src/MitaAI.Plugin/Plugin.cs:114-124.

The consumer logs Success but always passes Content to TryShowMitaReply. Network/authentication/rate-limit failures contain diagnostic strings, so those strings receive the Mita subtitle style and would become spoken error messages if TTS used the same path. Failures need a separate notice and ready-state recovery. Empty successful content is also replaced with "..." in AIChatClient.cs:356-359 instead of being treated as an empty provider reply.

### Medium: sanitizer can restore removed reasoning

Reference: src/MitaAI.Plugin/AIChatClient.cs:75-154.

After removing thinking blocks and action text, the empty-output fallback uses raw.Replace rather than the already cleaned string. A reasoning-only output can therefore restore its removed think block. A message such as "<think>private reasoning</think> *waves*" can restore both reasoning and action text. This is unsuitable as a future TTS input. Existing sanitizer checks do not cover this path.

### Medium: launcher saves reset unrelated settings

Reference: src/MitaAI.Launcher/MainForm.cs:591-680 and 803-805.

Saving replaces the entire BepInEx configuration with a fixed template, forcing E, range 2, temperature 0.7, timeout 25, fixture text, and duration 8 regardless of prior custom values. Play calls the same save operation. Load/save should preserve settings not edited in the launcher. Save failures are caught without returning failure, so LaunchGame may proceed with old configuration.

### Low: runtime evidence labels and documents drift

References: src/MitaAI.Plugin/NativePresentation.cs:175; docs/test-plugin.md; HANDOFF.md; docs/implementation-model.md; AGENTS.md.

All Mita subtitle starts still log mode=fixed-fixture, even for live AI. The test guide names AI markers that are not emitted by the current plugin. HANDOFF.md labels the latest artifact 0.5.1; current metadata remains 0.5.0. AGENTS.md describes fixed replies despite live connectivity. The implementation model's older bridge/native-dialogue plan has not been reconciled with direct HTTP and isolated screen subtitles. Provider quotas, model availability, and explanations of why another project changed direction are not independently verified by this inspection.

### Remaining verification gaps

The managed suite covers state helpers rather than the runtime orchestration. Its pause checks supply a fixed timestamp to ScreenSubtitleState; they do not exercise NativePresentation's actual virtual-clock integration. Full repeated-turn, scene-change-while-pending, request timeout, story progression/coexistence, and device/audio acceptance gates remain open. Draft text is retained, but ComposerState.Open moves the caret to the end rather than restoring its previous position.

## Architecture And Roadmap

The current system is game plugin -> provider HTTP request -> main-thread queue -> screen subtitle. The launcher is a configurator, not a running AI bridge. The model receives the persona, recent messages and current player text; it receives no verified room/animation/event context. Existing Mita discovery and story guards are useful foundations but do not yet constitute world awareness or character control.

No TTS synthesis/playback, microphone/STT, lip sync, persistent player facts, memory retrieval, action arbitration, navigation commands or autonomous initiation exists in the inspected source. An AudioModule reference and old template policy are not implemented voice functionality.

Recommended order: request lifecycle/error handling -> minimal TTS with subtitle fallback and cancellation -> push-to-talk STT -> verified world context -> bounded actions -> saved memory and initiative. Do not resume the failed native quest-driver experiment just to add voice; use owned audio playback attached to the verified character.

## Approximate Progress

Engineering estimate, not a measured percentage of total hours:

- Usable first text-conversation milestone: approximately 80-85%, with the core loop user-tested and robustness/coexistence gates still open.
- Full original vision: approximately 25-30%. One illustrative scope weighting assigns 25% to the text/game foundation, 10% to character conversation, 20% to voice, 15% to world context, 15% to bounded actions, and 15% to persistence/initiative. With the foundation about 85% and character conversation about 60%, that gives about 27%; the other areas remain unimplemented.

The dependency value of solving injection and live dialogue is larger than this scope percentage suggests. It establishes that the central modding approach works on the user's game copy.
