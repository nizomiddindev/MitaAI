# MitaAI 0.6.0 Russian Voice Prototype

Work started 20 October 2026 and continued across local midnight. User explicitly redirected work away from NeuroMita and approved getting MitaAI itself working. This supersedes earlier documentation saying game TTS integration was not authorized.

## Scope

First in-game speech implementation using the previously selected F5 Russian v2 voice, existing CUDA environment, confirmed reference/transcript and 32-step settings. No NeuroMita fixes, new engine, model/runtime downloads, global installations, game UI redesign or game/save changes. English replies remain text-only in this first prototype; Nano English integration is deferred. Exact Mita actor matching remains optional.

## Architecture

- `voice_worker.py` owns one prewarmed CUDA F5 instance. Uses the existing `.venv-f5-gpu` Python 3.11 / F5 1.1.4 / Torch 2.6.0+cu124 installation. Rechecks local model sizes and confirmed reference/source hashes. Explicit nonempty reference transcript and ASR guard prevent automatic transcription; HF/Transformers are offline. All voice cache/temp paths resolve into `D:\MitaAI\local\voice`.
- Worker transport is owned process stdin/stdout JSON lines, not an HTTP service. No listening port, API key or pairing token needed. Progress is drained separately without logging conversation text. Each request carries a generated identifier; successful audio is bounded mono 24 kHz PCM16 WAV encoded in the response. No automatic per-turn audio-file retention.
- `LocalVoiceClient.cs` starts the process without a console window, waits for explicit protocol readiness, permits one pending request, checks matching response identifiers and decodes audio off the Unity thread. Startup deadline 180 seconds; synthesis deadline 45 seconds. Cancellation/timeout terminates only the owned worker tree and restores text fallback. Ordinary turns reuse the warm worker. Disabled speech releases the worker; component teardown releases worker/audio objects.
- `VoiceAudio.cs` validates RIFF framing, chunk boundaries, PCM format/rate/channels, duration/size and nonsilent samples before Unity receives anything. `VoiceStamp` associates playback with request, scene and nonzero character identity.
- `VoicePlayback.cs` creates a separate mod-owned AudioSource and uploads PCM to an AudioClip on the main thread. Initial playback is non-spatial (2D), with a separate volume; no changes to native Mita animations or game audio sources. Clips are destroyed after use. Pausing, focus loss and history opening pause playback.
- Only an accepted successful reply enters synthesis. Scene/story/target changes discard pending audio and stop current speech. Provider errors never become spoken dialogue. Fixture replies remain diagnostic and only speak if explicitly configured with Russian content.
- Existing subtitles appear as before without waiting for synthesis. Audio waits until the Mita line, rather than the player echo, is displayed. The existing subtitle turn is held while speech is pending/playing, with bounded synthesis timeout, then resumes normal completion. Speech uses the same 240-text-element excerpt as the subtitle; history retains the full accepted reply. This is not word-level synchronization or streaming speech.

## Configuration

New `[Voice]` entries in BepInEx's existing `local.mitaai.probe.cfg`:

| Key | Default | Meaning |
| --- | --- | --- |
| Enabled | true | Enable this approved local Russian prototype; false keeps text-only mode |
| ProjectDirectory | Parent of game directory | Project with tools/voice-evaluation and local/voice; here D:\MitaAI |
| Volume | 0.8 | Separate mod speech volume, clamped to 0-1 |

No launcher redesign. Worker prewarming can take tens of seconds; replies during warm-up remain text-only with an existing notification. Non-Cyrillic replies remain text-only. Ask the AI for a short Russian reply to test this engine; do not silently overwrite the user's model/persona/provider settings.

## Verification

- Plugin builds for net6.0 with **zero warnings/errors** against the existing local Unity AudioModule and other interop references.
- **301 managed checks pass**, including 18 added WAV/ownership/fallback checks. Unity playback remains a real-game manual boundary, not established by those stubs.
- **28 existing offline voice checks + 8 new worker protocol checks pass**. These cover framing, invalid inputs, matching request IDs, engine reuse and keeping errors/private traces out of audio responses. Fixed an initially found protocol issue where validation errors lost an otherwise valid request ID; they now return promptly to that request rather than timing out.
- This laptop currently has .NET SDKs 6/9, not 10. Initial test-harness builds failed NETSDK1045. Added an opt-in `MitaAITestFramework` property, preserving net10.0 as the default; verified using `-p:MitaAITestFramework=net9.0`, followed by executing the resulting net9.0 DLL. No SDK installation/download.
- Real C# client -> owned Python process -> GPU F5 -> validated WAV smoke test succeeded twice with one resident worker. Latest run: **25,552 ms startup including warm-up**, **3195 / 3193 ms synthesis**, **3.531-second audio** for each original curious sentence. These are voice-only smoke measurements, not full game/LLM latency measurements.
- Invalid English request returned failure without breaking worker readiness. A subsequent canceled request returned no audio and stopped the owned worker. No smoke-test worker left running afterward.
- Both smoke WAV hashes match the user-approved preview exactly: `f009a45d718236fbd7c008eb8517322f311f0327ff88dd126d9c30d47868b998`. Generated listening artifacts: `local/voice/integration-smoke/f5-russian-worker-01.wav` and `02.wav`.

Commands:

```powershell
. ./tools/voice-evaluation/Set-VoiceEnvironment.ps1
local/voice/.venv-f5-gpu/Scripts/python.exe tools/voice-evaluation/test_preflight.py
local/voice/.venv-f5-gpu/Scripts/python.exe tools/voice-evaluation/test_voice_worker.py
dotnet build tests/MitaAI.InputChecks/MitaAI.InputChecks.csproj -c Release -p:MitaAITestFramework=net9.0
dotnet tests/MitaAI.InputChecks/bin/Release/net9.0/MitaAI.InputChecks.dll --voice-smoke D:\MitaAI
```

The optional smoke run deliberately loads the model, generates two lines and cancels a final request. Do not rerun just to replay the existing audio.

## Deployment

Confirmed MiSide stopped, backed up the previous installed DLL/manifest/config under `local/voice/install-backups/before-0.6.0`, then used `tools/Build-InstallProbe.ps1`. Install manifest now records 0.6.0. Previous assembly metadata was 0.5.1 despite its 0.5.2 BepInPlugin version; this release consistently uses 0.6.0.

Installed DLL SHA-256: `D7E091462254B0A4549F4279C4B5EE400B0C49D8ABF824779E57FA9F60D3F789`. Config hash was unchanged by build/install before game launch. Backup config contains local credentials: keep it private and out of version control.

Launched MiSide for interactive testing. Initial game logs confirm plugin 0.6.0 loaded, main-thread behavior registered and worker started with voice enabled; existing provider remains Gemini. No claim of audible in-game reply or playback/controls regression pass until the remaining gate below is observed.

## Remaining Acceptance Gate

Follow-up user test confirms H history pause/resume, Escape game pause/resume, and talking again after speech ends. These supersede pending wording for those specific gates below. Scene/story interruption and FPS/smoothness remain unverified. English is intentionally text-only in this integration. User requests shorter dialogue and lower latency; no tuning implemented yet.

**21 October listening result:** user confirms audible Russian dialogue and subjectively rates its similarity to Mita at 80-90%, with approximately 2-3 s perceived delay. Successful session logs independently show Gemini text generation 12999 ms, F5 generation 4346 ms, 5.33 s speech, and MITAAI_VOICE_PLAYING. Earlier provider request timed out after 25 s; the subsequent request succeeded. These timings are distinct from perceived delay and are not a universal benchmark. E/input reopened afterward. Audible playback gate passes for this reply; pause/history/scene/story interruption and smoothness remain unverified.

### 0.6.1 Playback Correction

The first human game test failed: text appeared without speech. Logs showed successful generation (4496 ms, 5.55 s WAV) followed by RuntimeWrappedException before the playing marker. No original wrapped exception payload was captured.

Inspection of the local generated AudioModule assembly with existing Mono.Cecil found CLR ldlen instructions operating on Il2CppStructArray in the reconstructed instance SetData method. Replaced that call with the generated static native SetData binding, passing an explicit IL2CPP sample buffer and count. Kept normal clip creation and owned AudioSource lifecycle. Added stage-aware wrapped-exception reporting and an isolated silent 10 ms upload/play/stop startup probe.

Plugin 0.6.1 builds cleanly; all 301 managed checks still pass. Deployed after user closed game, through the existing installer; backup under local/voice/install-backups/before-0.6.1. Restarted game logs show MITAAI_VOICE_UPLOAD_CHECK passed=True audible=False. This verifies upload and playback calls no longer throw in that probe, not that a person heard dialogue. Russian reply listening and interruption checks are still required. No new downloads or engine/UI/provider changes.

Wait for `MITAAI_VOICE_READY`, enter ordinary gameplay, approach Mita and request a short Russian reply. Verify `MITAAI_VOICE_AUDIO_READY` -> `MITAAI_VOICE_PLAYING` and actual audible speech. Confirm E interaction/typing/history remain usable, pause/resume stops and resumes speech correctly, and story/scene changes stop it without stale playback. Test unavailable worker, text-only language and provider-error fallback. Preserve reported game smoothness as untested until someone actually plays during generation.

Known limits: Russian-only GPU engine, 2D audio, whole-utterance buffering, no word/lip synchronization, subtitle excerpts, noticeable startup/inference latency, worker cold restart after cancellation during synthesis. Earlier shared-LLM latency was 6-8 seconds; the new standalone 3.2-second result does not supersede it. No game-world perception, new memory system, initiative or installer framework added. Full professional roadmap remains deferred.
