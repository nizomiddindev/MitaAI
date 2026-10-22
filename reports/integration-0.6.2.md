# MitaAI 0.6.2 Integration Preparation

Date: 22 October 2026. Installed test build; gameplay acceptance pending.

## Included Changes

- Reviewed worker startup retry limits, safe error diagnostics, background process cleanup, early-cancellation handling, and shutdown after mod failure.
- Native Audio_BlendShapeVoice binding to the mod-owned speech source, including pause/stop/takeover handling. Cleanup checks the mouth shape name before resetting a cached index after a mesh change.
- Scene/room collector with raw-value validation, unknown/ambiguous results, and scene-aware 0.5-second caching.
- Explicit diagnostic hook: Ctrl+F9 outside input/history, or opening a conversation with E. Each capture logs MITAAI_CONTEXT with trigger and capturedAt. The collector is constructed from the main-thread event, and diagnostic failures are isolated from gameplay handling.
- [Diagnostics] ContextCaptureEnabled defaults true for this development test build. Setting it false disables both capture hooks on the next load. No AI prompt injection, room labels or UI redesign.
- Plugin attribute, startup log and project version: 0.6.2. F5 synthesis remains 32 steps, with existing model/reference/settings.

## Verification And Installation

- Release plugin build: zero warnings/errors.
- 358 managed input/AI/voice checks and 39 context snapshot/gate checks pass. Unity/native animation is stubbed; these do not establish real mouth movement or room readings.
- 16 Python worker tests and 28 preflight tests pass without loading the voice model.
- Existing DLL matched its install manifest before backup. Backup: local/voice/install-backups/before-0.6.2-20261022-120137 (DLL plus manifest).
- Installed using tools/Build-InstallProbe.ps1 with MiSide closed. Build, installed DLL and manifest SHA256 match: CF1981DB2588F6AAD1D1F91EF2D5DB8DA42DE5C0F4E61522C8BBFDFAD1D0E4DC.
- Prior installed 0.6.1 SHA256: A33E230DAE7CD8DD375BC2A015E2AC1C5CC2A35E533DBC78CD3CC5B746791AFA.
- No game/save/config changes beyond the plugin DLL and install manifest. The new diagnostic config binding is created when the game next loads the plugin. No game launch, GPU synthesis, downloads or commits. Final check found no game or actual voice-worker process.

## Next Gameplay Session

Notify the user first and wait for confirmation that the laptop is plugged in and they are ready. Do not launch automatically.

1. Confirm startup log version=0.6.2. Let the existing voice worker warm up, then request a short Russian response near Mita.
2. Verify audible speech, lipSync=bound in the playback log, mouth movement during playback rather than generation, normal completion, and repeat interaction.
3. Check H history, Escape pause and focus loss during playback; speech and mouth should pause and recover without changing body/story animations.
4. Check story and scene interruptions during synthesis and during playback. Confirm speech releases, story dialogue still animates normally, and E works again when eligible. Submitted synthesis cancellation still restarts the worker cold by design.
5. In Chapter 3 free roam, close input/history and press Ctrl+F9 fully inside each reachable room. Associate each MITAAI_CONTEXT raw room value with the physical room. Repeat on the reverse route and at two doorways. Record source count and unknown reasons.
6. Two captures within 0.5 seconds should reuse capturedAt; later captures should refresh. A scene change bypasses the cache. Identical room text alone does not prove reuse.
7. If available, repeat room sampling in Chapter 4 and confirm a scene without the controller reports unknown/no-source. Do not rename raw values or feed them to the AI yet.
8. Record visible smoothness and any stutters; subjective feedback is not measured FPS. Native spectrum amplitude, pause update ordering and variant coverage remain runtime questions.

## Rollback

With MiSide closed, restore both the backed-up DLL and install-manifest.json together to MiSide_Dev/BepInEx/plugins/MitaAI, then verify the DLL against the restored manifest and prior hash above. No rollback was performed. The worker's added code field remains protocol-compatible with the old client; no Python environment or model changes were made in this preparation.
