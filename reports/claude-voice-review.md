# Voice Integration Review (Claude, 21 October 2026)

Scope: a read-only review of the current working tree (v0.6.1 source) covering `src/MitaAI.Plugin/LocalVoiceClient.cs`, `VoicePlayback.cs`, `VoiceAudio.cs`, the voice lifecycle in `Plugin.cs` (with the related `NativePresentation.cs` turn state), `tools/voice-evaluation/voice_worker.py`, and the voice checks in `tests/MitaAI.InputChecks/Program.cs` and `tools/voice-evaluation/test_voice_worker.py`.

Nothing was built, run, launched or modified apart from this report. The findings come from reading the code. Nothing below was reproduced in game.

**Status update (same day):** the user approved fixes for findings 1, 3, 4 and 5. They are implemented and checked with automated tests, but not deployed and not verified in game; see "Fixes applied" at the end. Findings 2 and 6 are unchanged, as instructed.

Labels used:
- **Confirmed defect**: the failure follows directly from the code as written.
- **Confirmed behaviour / design decision**: the code does this on purpose, but the cost to the user is worth deciding on again.
- **Risk**: plausible from the code, but it needs in-game or runtime verification.

---

## 1. Medium: a worker that fails at startup is relaunched every 60 s indefinitely (confirmed defect)

**Where**
- `LocalVoiceClient.cs:132`: a `fatal` message from the worker is turned into an exception.
- `LocalVoiceClient.cs:155`: that exception leads to `StopOwned(owned, "worker-protocol-or-runtime")`.
- `LocalVoiceClient.cs:169`: `retryAfter` is set to now + 60 s. There is no count of failures and no cap.
- `LocalVoiceClient.cs:79-80`: a 180 s startup timeout takes the same path.
- `Plugin.cs:131`: `EnsureStarted` is called every frame while voice is enabled, so the worker is relaunched as soon as `retryAfter` passes.
- `voice_worker.py:136-143`: `cuda_unavailable`, `missing_local_model`, `engine_version_mismatch`, a CUDA OOM during load and similar errors are only detected after the heavy imports (torch / f5_tts) and the model load.
- `Plugin.cs:1056-1059`: every reply shows "Voice is warming up. This reply is text-only.", including when the worker can never start.

**Failure scenario**
1. The RTX GPU is unavailable or full. For example, LM Studio has taken the VRAM, or the driver resets.
2. The worker imports torch and F5 (measured at 30-72 s), fails, and emits `fatal`.
3. 60 s later the plugin starts a new worker, which repeats the same imports and model load.
4. This continues for the whole session. The repeated CPU, disk and RAM load during gameplay is likely to cause stutter.
5. Throughout, the player is told voice is "warming up". The log shows only `reason=worker-protocol-or-runtime`, because the worker's `error` field (for example `cuda_unavailable`) is discarded at line 132.

**Smallest correction**
- In `LocalVoiceClient`, count consecutive stops that happen before `ready` completed.
- After the first one (or two), stop retrying automatically until voice is disabled and re-enabled, or the game restarts. An escalating backoff (60 s → 5 min → 15 min) would also work.
- Log the `error` string from the `fatal` message before throwing.
- Optionally, expose a `Failed` state so `BeginVoice` can say "voice unavailable" instead of "warming up".

## 2. Medium: any cancellation of an in-flight synthesis kills the warm worker (confirmed behaviour; design decision)

**Where**
- `LocalVoiceClient.cs:106-110`: cancellation leads to `StopOwned(..., "cancelled")`, which kills the process tree (line 171).
- `LocalVoiceClient.cs:169`: the retry is only 2 s, but the restart is cold.
- The triggers all call `CancelVoice()` → `activeVoiceCts.Cancel()` (`Plugin.cs:1123`):
  - scene change (`Plugin.cs:150-152`)
  - `StoryBusy` in both Update and LateUpdate (`Plugin.cs:164-167`, `Plugin.cs:263-265`)
  - binding loss or the turn ending (`Plugin.cs:1078-1082`, `Plugin.cs:214-215`)
  - voice being disabled (`Plugin.cs:132`)
- `StoryBusy` is true whenever native dialogue text is active, not only during cutscenes (`NativePresentation.cs:114-117`).

**Failure scenario**
1. Mita's reply is being synthesised (about 3-8 s).
2. A native game line appears (`dialogueTextNow` becomes active), or the scene changes.
3. The plugin rightly drops the speech, but it also kills the warm worker.
4. The next worker needs the full cold startup: 25.5 s was measured in the C# smoke test, and imports alone took up to 72 s on a cold cache.
5. Every reply in that window is text-only with the "warming up" notice, and the restart adds CPU and disk load at a story moment.

This matches the documented 0.6.0 choice ("canceling active inference kills owned worker and later restarts cold"). It is listed here because the trigger set is broader than "the user cancelled", and the cost is a restart of up to about a minute.

**Smallest correction (needs your decision)**
- When a request that was already written is cancelled, leave the worker running. Instead, mark the request id as abandoned:
  - keep `pending` occupied, so the next request gets `busy` (text-only for that one reply);
  - have `ReadResponses` drop the late audio and clear `pending` when the abandoned id arrives.
- Keep killing the worker for timeout, disable and shutdown.
- Stale audio still cannot play: `VoiceStamp` and the request id already reject it.

## 3. Low-Medium: the worker kill on cancellation runs synchronously on the Unity main thread (confirmed path; hitch size unmeasured)

**Where**
- `Plugin.cs:1123` calls `activeVoiceCts.Cancel()` on the main thread.
- That cancels the linked `timeout` token (`LocalVoiceClient.cs:97`).
- `Task.WaitAsync` (net6.0, `MitaAI.Plugin.csproj:3`) completes its promise inside the cancellation callback. That promise is not created with `RunContinuationsAsynchronously`.
- So the `ConfigureAwait(false)` continuation of `SynthesizeAsync` can run inline. That executes `StopOwned` (`LocalVoiceClient.cs:158-173`) on the main thread:
  - `Kill(entireProcessTree: true)`, which on Windows enumerates system processes to find descendants;
  - `Process.Dispose()`, which closes the redirected pipes;
  - a log write.

**Failure scenario**
- A story line or scene change happens during synthesis, and the frame that handles it also kills and disposes the process tree. That could cause a visible stutter exactly at a story transition.
- The cost was not measured. It needs a frame-time check in game.

**Smallest correction**
- In `StopOwned`, keep the state reset under the lock synchronous, but do the `Kill`/`Dispose`/log on a pool thread, for example `_ = Task.Run(() => { try { if (!owned.HasExited) owned.Kill(true); } catch { } owned.Dispose(); log(...); });`.
- Alternatively, wrap only the cancellation-path call in `Task.Run`.

## 4. Low: a cancellation that arrives before the request is written still kills an idle warm worker (confirmed defect, narrow window)

**Where**
- `Plugin.cs:1067-1071`: `BeginVoice` starts `SynthesizeAsync` via `Task.Run`.
- `LocalVoiceClient.cs:93-95`: `SynthesizeAsync` claims `pending` and then (line 101) calls `ThrowIfCancellationRequested()`.
- The `catch` at lines 106-108 then calls `StopOwned(owned, "cancelled")`, even though nothing was sent to the worker.
- If the CTS was already cancelled and disposed (`Plugin.cs:1123-1124`), `CreateLinkedTokenSource` at line 97 also produces a cancelled token, and the result is the same.

**Failure scenario**
1. A reply is accepted and `BeginVoice` queues the task.
2. In the same frame, LateUpdate sees `StoryBusy` (`Plugin.cs:263-265`), or `TickVoice` sees `!HasActiveTurn`, and cancels.
3. The thread pool picks up the task afterwards. This is more likely when the pool is busy during loading.
4. The idle, warm worker is killed and a cold restart follows (see finding 2).

**Smallest correction**
- Inside the `lock` at `LocalVoiceClient.cs:88-96`, return `new(null, "cancelled")` if `cancellationToken.IsCancellationRequested`, before claiming `pending`.
- Or track a `written` flag and only call `StopOwned` from the cancellation catch once the request line has been written.

## 5. Low: after `MITAAI_PROBE_FAILED` the voice worker stays resident (confirmed defect)

**Where**
- `Plugin.cs:250-255` and `Plugin.cs:288-293`: these catches set `failed = true` and call `ResetBinding()`. That stops playback through `CancelVoice`, but it does not stop `voiceClient`.
- `Plugin.cs:123` and `Plugin.cs:260`: once `failed` is set, Update and LateUpdate return early, so voice is never used or stopped again until `OnApplicationQuit`/`OnDestroy`.

**Failure scenario**
- Any unrelated exception in Update disables the mod for the session.
- The F5 worker keeps about 2 GB of VRAM (as measured) and a Python process for the rest of the session. That takes RTX headroom away from the local LLM for nothing.

**Smallest correction**
- In both catch blocks, after `ResetBinding()`, call `ShutdownVoice(permanent: false)` (or `voiceClient.Stop()`).

## 6. Low (risk): persistent synthesis failures never restart the worker

**Where**
- `voice_worker.py:127-129`: every engine exception becomes a per-request `ok:false`, and the worker keeps serving.
- `LocalVoiceClient.cs:147-150`: `ok:false` becomes `"synthesis-failed"` and is otherwise ignored. The worker's `error` type name is discarded, so `Plugin.cs:1093` cannot log it.

**Failure scenario**
- A sticky CUDA error, such as an illegal memory access or a device-side assert after a driver hiccup, leaves the CUDA context unusable.
- Every later request fails quickly with `ok:false`, while `IsReady` stays true.
- Voice is silently text-only until the game restarts. The log shows `reason=synthesis-failed` with no cause.
- Whether such sticky errors occur on this laptop alongside LM Studio is unverified.

**Smallest correction**
- Pass the worker's `error` field through to `VoiceResult.Error`.
- After two consecutive non-validation failures (anything other than `ValueError`), call `StopOwned(owned, "synthesis-failing")` so the normal restart path runs. That restart path should then follow the cap from finding 1.

---

## Checked and not found defective

- **Stale audio:** every voice request gets a new `currentRequestId`, and `CancelVoice` clears the queue. `TickVoice` requires both `voiceStamp == response.Stamp` and `Stamp.Matches(...)` (`Plugin.cs:1086-1087`). The client also drops worker replies with a mismatched id or from an old process (`LocalVoiceClient.cs:144`). I found no path where audio from an earlier request, scene or character can play.
- **Pause/resume:** `TickVoice` pauses on `native.IsPaused || historyOpen`. `IsPaused` includes `controller.isPaused`, focus loss and `timeScale <= 0` (`NativePresentation.cs:43`). Audio that arrives while paused waits (`Plugin.cs:1099`). `VoiceHolding` stops the subtitle turn from ending while generation or playback is pending (`NativePresentation.cs:77`). This is consistent with the user-verified H/Escape results.
- **Disabling voice:** `Plugin.cs:132` cancels, stops the worker (2 s retry) and drops `VoiceHolding`. `BeginVoice` returns early while voice is disabled.
- **Worker crash or hang:** a crash ends stdout, and `StopOwned` completes `pending` with the reason, so the reply falls back to text. A hang is bounded by the 45 s timeout. stderr is drained, so the pipe cannot block (`LocalVoiceClient.cs:57`).
- **Size limits are consistent:** text 600 (C# UTF-16, which is stricter than the Python code-point count); request frame ≤ 16384 (Cyrillic is `\u`-escaped by System.Text.Json, so ~3.6k chars); WAV ≤ 60 s = 2,880,044 B < `MaximumWaveBytes` 3,000,000; base64 line ~3.84 M < 4.1 M read cap.
- **IPC framing:** UTF-8 stdin without a BOM, CRLF handled by Python universal newlines, and the Cyrillic check in C# matches the worker's `[Ѐ-ӿ]`.

## Test coverage gaps (automated)

- Managed checks (`Program.cs:552-594`) cover WAV decoding, `VoiceStamp` matching and the unstarted client only.
- There are no automated checks for:
  - restart/backoff after `fatal` or a startup timeout;
  - cancellation before the write (finding 4);
  - the main-thread cancellation path;
  - the `TickVoice` lifecycle;
  - `VoicePlayback` pause and end detection.
- The live `--voice-smoke` cancellation check (`Program.cs:619-623`) cancels after the request has been written, and requires the worker to stop. Any change for finding 2 has to update that expectation deliberately.
- `test_voice_worker.py` covers protocol and validation only. It has no check that the `fatal` error name is emitted in a form the client logs.

## Verification gaps requiring in-game or runtime checks

1. Story interruption during **generation** and during **playback**: speech stops, the subtitle clears, E works afterwards, and how long voice stays text-only after the cold restart. This is still pending per the HANDOFF.
2. Scene change during generation and playback. The `AudioSource` root is `DontDestroyOnLoad` (`VoicePlayback.cs:31`), and cancellation depends on the **active scene handle** changing (`Plugin.cs:149-150`). Loading screens or additive loads that keep the active scene may let speech continue until the handle changes or the target deactivates.
3. Frame-time or stutter at the moment of cancellation (finding 3), and during a cold restart while playing (finding 2).
4. Other game pause paths not covered by `controller.isPaused` / `timeScale`, for example a global `AudioListener.pause`. With such a path, `VoicePlayback.Tick` (`VoicePlayback.cs:58`) could see `!isPlaying` and end the clip early instead of resuming.
5. Focus loss with the game's actual `runInBackground` setting (unknown). Pause is handled in code but was not exercised.
6. Behaviour while LM Studio holds the GPU: the startup path within 180 s and OOM handling, which feed findings 1 and 6.
7. Abnormal game exit: the worker exits on stdin EOF, but only after its current import or synthesis finishes, so GPU memory can stay held for up to about a minute. Not observed.
8. Measured FPS or smoothness with voice active. Still untested per the HANDOFF.

---

## Fixes applied (findings 1, 3, 4, 5)

Approved by the user after the review. Findings 2 and 6 were left unchanged: a cancelled request that was already submitted still stops the worker, and there is no automatic GPU recovery. Production voice settings are unchanged: 32 steps, same model, same reference audio, same playback behaviour, UI and launcher. Nothing was deployed, MiSide was not launched, and no model was loaded.

### Changed files

- `src/MitaAI.Plugin/LocalVoiceClient.cs`
  - **Finding 1, bounded retries:**
    - Added `VoiceWorkerOptions` (production defaults: 180 s startup timeout, 45 s synthesis timeout, 60 s failure retry, 2 s retry after a deliberate stop, 3 startup failures) and a `VoiceWorkerState` enum (`Stopped`, `Starting`, `Ready`, `Unavailable`).
    - Any stop before `ready` counts as a startup failure: a start exception, missing worker files, a `fatal` message, the worker exiting, a protocol error, or the startup timeout. A disable, cancel or shutdown during startup does not count.
    - After 3 consecutive startup failures the state becomes `Unavailable` and the worker is not relaunched automatically. A `ready` message resets the count.
    - Logs: `MITAAI_VOICE_START_FAILED ... failures=N retryInSeconds=60` while retries remain, then `MITAAI_VOICE_UNAVAILABLE ... autoRetry=False recovery=fix-cause-then-disable-voice-or-restart-game`.
    - **Recovery:** `Stop()` (voice disabled) clears the unavailable state and logs `MITAAI_VOICE_RETRY_RESET`. The plugin has no runtime config reload, so in practice the recovery path is: fix the cause, then restart MiSide. Toggling `[Voice] Enabled` in-session only works if something like a configuration manager changes it live.
  - **Finding 1, error codes:**
    - The worker's `code` (falling back to `error`) is passed through to the logs, but only if it is an identifier matching `^[A-Za-z][A-Za-z0-9_]{0,63}$`; anything else becomes `unknown`.
    - Per-request failures now report `synthesis-failed:<code>`, which is diagnostics only.
    - Missing worker files are reported as `worker_files_missing`.
  - **Finding 3, cleanup off the caller's thread:**
    - `StopOwned` now only resets state under the lock. The process-tree kill, a bounded `WaitForExit(5000)`, `Dispose` and the `MITAAI_VOICE_STOPPED` log run on a pool thread through a tracked `cleanup` task.
    - `EnsureStarted` refuses to start a replacement until that task has completed, so old and new workers cannot overlap.
    - Cleanup still happens exactly once per process, because the existing `process != owned` guard is kept.
    - `Dispose()` (game quit only) waits at most 2 s for the cleanup so the worker isn't orphaned. That is the only remaining wait on the main thread.
    - The `ready` TCS is now created after a successful `Start()`, not before.
  - **Finding 4, early cancellation:**
    - If the token is already cancelled, `SynthesizeAsync` returns `cancelled-before-submit` without claiming `pending`.
    - After claiming it, the linked token is checked again before submission.
    - The worker is only stopped once `submitted` is set, immediately before the request write begins, which keeps the protocol safe.
- `src/MitaAI.Plugin/FailureCleanup.cs` (new): runs each named cleanup step and reports any exception, so a later step still runs if an earlier one throws.
- `src/MitaAI.Plugin/Plugin.cs`
  - **Finding 5:** both the Update and LateUpdate exception handlers now call `DisableAfterFailure()`. It sets `failed`, then runs these steps through `FailureCleanup`: `voiceClient.Stop("plugin-failed")` first, then `ResetBinding()`, then `CancelVoice()` + `voicePlayback.Dispose()`.
  - **Finding 1:** `BeginVoice` logs `MITAAI_VOICE_TEXT_ONLY reason=voice-unavailable|voice-warming`. When the worker is unavailable the notice reads "Voice is unavailable (see log). This reply is text-only." instead of "warming up". The notice mechanism and style are unchanged.
- `tools/voice-evaluation/voice_worker.py`: `fatal` and per-request failure messages gain a `code` field.
  - The code is the exception message only when it is a worker-owned lowercase identifier (for example `cuda_unavailable`, `russian_required`); otherwise it is the exception type name. Engine text, including Cyrillic, is never echoed.
  - The existing `error` field is kept, and synthesis is unchanged (32 steps).
- `tools/voice-evaluation/test_voice_worker.py`: 3 new checks: the error-code allowlist, the startup `fatal` code, and the per-request code.
- `tests/MitaAI.InputChecks/Program.cs` and `.csproj`:
  - A `--fake-voice-worker <mode>` entry point re-runs the test executable as a stand-in worker with the same stdio protocol, and no Python, model or GPU. Modes: `fatal`, `hang-startup`, `echo`, `silent`.
  - 27 new checks:
    - **retry limits:** retries stop at 3 starts; a fatal code reaches the log; no relaunch once unavailable; disable resets and the worker starts again; missing files stop at the limit; a startup timeout counts and its worker is killed; `Starting` is distinct from `Unavailable`.
    - **early cancellation:** a pre-cancelled request keeps the same warm worker and never reaches it (the fake counts requests); a per-request code is logged without a restart.
    - **policy unchanged:** cancelling a submitted request still stops the worker.
    - **cleanup ownership:** `Stop()` returns in under 500 ms while termination is blocked; repeated stops terminate once; no replacement starts during cleanup; it starts after cleanup.
    - **failure shutdown:** the worker is stopped first and released, and every step still runs when the binding or worker step throws.

### Verification

- `dotnet build src/MitaAI.Plugin/MitaAI.Plugin.csproj -c Release`: build succeeded, **0 warnings, 0 errors**. Build only; not installed. The version is still 0.6.1, and the version bump is left for integration.
- `dotnet build tests/MitaAI.InputChecks/MitaAI.InputChecks.csproj -c Release -p:MitaAITestFramework=net9.0`: 0 warnings, 0 errors.
- `dotnet tests/MitaAI.InputChecks/bin/Release/net9.0/MitaAI.InputChecks.dll`: **PASS: 328 managed checks** (previously 301; 27 new). It passed on 6 consecutive runs, each about 2.7 s. Afterwards no `--fake-voice-worker` processes remained.
- `local/voice/.venv-f5-gpu/Scripts/python.exe -B tools/voice-evaluation/test_voice_worker.py`: **16 tests OK** (13 before, 3 new). `test_preflight.py`: **28 tests OK**.
- Not run: the live `--voice-smoke` (it loads the F5 model on the GPU). Its expectation that a submitted cancellation stops the worker still holds by design, and the fake-worker check above covers it. Nothing in game.

### Limits of this verification

- The fake worker exercises the real `LocalVoiceClient` process and pipe code, but not the Python/torch startup or real CUDA failure modes. Error codes from the real worker are covered only by the Python unit checks.
- The Unity wiring has no automated test, because the Unity boundary is stubbed. That covers `DisableAfterFailure` being called from Update/LateUpdate and the new notice text. It was checked by reading the code and by the plugin build.
- The original frame hitch (finding 3) was never measured. The change removes the kill/dispose from the cancelling thread, but it does not prove a hitch existed or that none remains.

### Remaining gameplay checks (after Codex integrates and deploys)

1. A normal Russian reply still speaks. The first reply after launch shows "warming up" until ready, then speech works.
2. H history and Escape pause/resume still pause and resume speech (regression check).
3. A story line or scene change during **generation** stops it. The log shows `MITAAI_VOICE_STOPPED reason=cancelled` from the background cleanup, then one new `MITAAI_VOICE_STARTING` only after that. Watch for a stutter at that moment.
4. A story line or scene change during **playback** stops the audio, and interaction works afterwards.
5. Optional failure drill, only with the user's approval: make the worker fail to start without touching game files (for example, point `[Voice] ProjectDirectory` at an empty folder for one session). Expect 3 `MITAAI_VOICE_START_FAILED`/`UNAVAILABLE` lines, no further launches, and the "unavailable" notice. Then restore the setting and restart.
6. The remaining gaps from the list above still apply: loading screens and additive scenes, other pause paths, behaviour while LM Studio holds the GPU, and FPS.
