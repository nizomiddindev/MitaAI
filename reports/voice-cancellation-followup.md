# Voice Cancellation Follow-up: Keep the Warm Worker (Finding 2)

**Date:** 22 October 2026 · **Author:** Claude Code · **Checkout:** `D:\MitaAI\local\worktrees\claude-code`, branch `codex/claude-work` (base `8935813`)
**Status:** implemented and checked with automated tests only. Not integrated, not deployed, not verified in game, and the live GPU smoke test was not run.

## Codex Integration Review (22 October)

- Reviewed original commit 517697d and cherry-picked it as cc98177 onto main without conflicts. This supersedes the earlier not-integrated status; nothing was deployed.
- Independently verified the combined Release plugin build with zero warnings/errors, 381 managed voice/input checks, and 98 context/formatter checks (including the separately integrated Antigravity formatter). No blocking issue identified in the current scope.
- Real Python/F5, Unity playback and the updated GPU smoke test remain unexercised. Installed 0.6.2 DLL is unchanged and still uses the old stop-on-submitted-cancel behavior. Source integration must not be reported as gameplay verification or a measured performance improvement.
- No game/model launch or push. Final process check found no game or fake-worker process. HANDOFF.md records the integration state and pending acceptance gate.

## Problem

Finding 2 of `reports/claude-voice-review.md` was that cancelling a request already sent to the worker killed the warm F5 worker. In game, that cancellation comes from a story line, a scene change or a lost binding during generation. The next worker then needed a cold restart of about 25–70 s, and replies were text-only until it finished.

## New behaviour (`src/MitaAI.Plugin/LocalVoiceClient.cs`)

**Cancelling a submitted request**
- The caller is released immediately with `cancelled-abandoned`, and the abandoned request's audio is never returned to it.
- The request stays in the single `pending` slot, marked `abandoned`, and the log shows `MITAAI_VOICE_ABANDONED reason=cancelled`.
- The worker finishes the abandoned request. Until its matching response arrives, new calls return `busy`, so overlapping work is never submitted.

**The late response**
- When the worker's response for that id arrives, the slot is freed and the log shows `MITAAI_VOICE_ABANDONED_DRAINED`.
- That response's base64/WAV payload is **not decoded**.
- After that, the next request is submitted normally.

**One deadline per request**
- A per-request watchdog starts at submission and runs for `SynthesisTimeout` (45 s in production).
- It applies whether the caller is still waiting or has abandoned the request.
- If the request is still pending when the deadline passes, the worker is stopped (`synthesis-timeout`). The check and the stop happen under one lock, so a response arriving at that moment cannot be misattributed.
- The caller no longer has its own timeout; a waiting caller receives `synthesis-timeout` from the stop.

**Protocol safety**
- The request line is written completely before cancellation is observed, because cancellation is only observed by the wait after the write.
- A write or transport exception still stops the worker (`transport`).
- Responses from an old process or with a non-matching id are ignored before any decoding.
- The pending slot is now cleared when the response arrives rather than in the caller's `finally`.
- If decoding a live (not abandoned) response fails, its caller is completed with `worker-protocol-or-runtime` before the existing protocol-error stop. Without this, that caller could wait forever, since the request is no longer in the `pending` slot.

**Kept from the earlier fixes**
- Pre-submit cancellation still returns `cancelled-before-submit` without touching the worker.
- The startup retry limit and `Unavailable` state are unchanged.
- Cleanup still runs in a tracked background task, and the no-overlap check on restart is unchanged.
- Explicit `Stop("disabled")`, `Stop("plugin-failed")` and `Dispose()` (shutdown) still stop the worker, including while it is draining.
- A worker that crashes or exits during a drain is stopped through the existing `worker-exited` path.

**Plugin impact:** no change to `Plugin.cs`. `BeginVoice` already treats a `busy` result as text-only and logs `MITAAI_VOICE_TEXT_ONLY reason=busy` through `TickVoice`. During a drain, a new reply is therefore text-only for the remaining generation time of the abandoned request (a few seconds) instead of the 25–70 s cold restart.

## Changed files

- `src/MitaAI.Plugin/LocalVoiceClient.cs`
- `tests/MitaAI.InputChecks/Program.cs`:
  - Two new fake-worker modes: `delayed-<ms>` and `exit-after-<ms>`.
    - `delayed-<ms>` waits like a real worker. It then sends a decoy reply for a foreign id, followed by the real reply. For texts containing `abandon`, the real reply carries an undecodable WAV.
    - `exit-after-<ms>` exits during a request.
  - The old check "cancelling a submitted request still stops the worker" is replaced by 25 checks (net +23).
  - The live `--voice-smoke` expectation is updated: after cancelling, the worker must stay ready. The test then waits while the client reports `busy` (at most 60 s) and requires the next synthesis to succeed. **It was not run.**
- `reports/voice-cancellation-followup.md` (this report)

Not changed: voice settings or synthesis (still 32 steps), the Python worker, playback, animation, `Plugin.cs`, configuration, `HANDOFF.md`, `AGENTS.md`.

## Verification (automated only)

| Check | Result |
| :--- | :--- |
| `dotnet build src/MitaAI.Plugin/MitaAI.Plugin.csproj -c Release -p:GameDirectory=D:\MitaAI\MiSide_Dev` (PowerShell) | Build succeeded, 0 warnings, 0 errors. A first attempt from Git Bash lost the path escaping and could not resolve the game references; that was a command-quoting problem, not a code problem. |
| `MitaAI.InputChecks` (net9.0 override) | **PASS: 381** managed checks (previously 358), on 6 consecutive runs of about 6 s each. Afterwards no fake-worker processes remained. |
| `MitaAI.ContextChecks` (net9.0 override) | PASS: 39 (unchanged code; regression check only) |
| `git diff --check` | clean |
| Python worker / preflight tests | Not run, because the Python code is unchanged. Running them would also mean using the shared voice environment from a worker checkout, which the coordination rules avoid. |

What the new fake-worker checks cover:
- **Caller release:** a submitted cancel returns `cancelled-abandoned` within 250 ms, and the same warm worker stays ready.
- **Busy while draining:** a request made during the drain returns `busy`.
- **Drain:** the late response drains, and the log shows `MITAAI_VOICE_ABANDONED_DRAINED`.
- **No decode, no misattribution:** the abandoned undecodable WAV and the foreign-id decoy do not stop the worker.
- **Next request:** it succeeds with the fake's request count at 2, which proves the `busy` attempt was never sent and the worker was not restarted.
- **Deadline from submission:** cancelling at 900 ms with a 1000 ms deadline stops the hung worker before 1600 ms after submission. A deadline measured from the cancel would stop it at about 1900 ms.
- **Awaited timeout:** a request the caller is still waiting on returns `synthesis-timeout`, and the worker is stopped.
- **Crash during drain:** a worker that exits while draining is stopped (`worker-exited`).
- **Explicit stops:** `disabled`, `plugin-failed` and `shutdown` during a drain each stop the worker, and the process exits.

## Remaining limitations

- **Unity is stubbed in these checks.** The fake worker uses the real client pipe code, but neither Unity nor the real Python/F5 worker was exercised. The live smoke test is updated but not run, since it loads the model on the GPU.
- **A worker hang during a drain** is still bounded only by the 45 s deadline. In that window new replies are text-only (`busy`), and then the worker is stopped and cold-restarted, as before.
- **Release waits for the write.** If cancellation lands while the request line is being written, the caller is released once that write finishes. The request is at most about 3.7 KB to an idle pipe, so this should be milliseconds; it was not measured.
- **The `busy` window has no user notice.** Only the log records `reason=busy`. Whether players notice this short text-only window is a gameplay question.
- **Gameplay checks still needed after integration:**
  - A story line or scene change during **generation**: speech stops; the log shows `MITAAI_VOICE_ABANDONED` then `MITAAI_VOICE_ABANDONED_DRAINED`, with no `MITAAI_VOICE_STOPPED` / `MITAAI_VOICE_STARTING`; the next Russian reply after the drain speaks without a cold-start delay.
  - Interrupting during **playback** is unchanged, because the request has already completed by then.
  - Normal speech, H/Escape pause and resume, and re-interaction (regression checks).
