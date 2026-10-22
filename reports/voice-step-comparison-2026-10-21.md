# F5 Russian Step Comparison

## Scope

User approved comparing fewer generation steps after confirming audible v0.6.1 speech, pause/history recovery, and dissatisfaction with latency. This is an isolated audition, not deployment or approval of a new voice setting. Existing room-like coloration remains undiagnosed; no audio filtering or Unity effects changes made.

Reused existing Python/F5/CUDA installation, model, vocoder, confirmed reference, seed 42, speed 1, CFG 2, six CPU threads and gain-to-0.8 preview handling. Only the inference step count varies: 32, 24, 16. No downloads, engine installation, game/save/plugin DLL/config or UI changes.

## Method

- One GPU engine instance; initialization/imports took 22.78 seconds, excluded from warm measurements.
- Warmed each step setting before measurements. Three original text cases: greeting, short reply, previously approved curious line. Two measured repetitions per case/setting: 18 WAVs. Settings rotate across cases/repeats to reduce simple order bias; this is still a small sample, not statistical performance certification.
- Timing covers the same worker synthesis path (reference preprocessing, model/vocoder, gain normalization, WAV encoding), not LLM generation, transport, Unity upload or time to first audible sound.
- Game was not running. GPU snapshot before loading reported 0 MiB used; this is not an LM Studio resident/active concurrency test. Later application performance may differ.

## Results

Median warm generation seconds across two repetitions:

| Text Case | 32 Steps | 24 Steps | 16 Steps |
| --- | --- | --- | --- |
| Greeting | 2.198 | 1.648 | 1.159 |
| Short reply | 2.261 | 1.731 | 1.173 |
| Curious line | 3.195 | 2.408 | 1.615 |

Approximately 23-25% lower generation time at 24 steps and 47-49% lower at 16 on these cases. Audio durations match across settings within each case: 0.693 s greeting, 1.077 s short reply, 3.531 s curious line. Same duration does not establish equal pronunciation, similarity or quality.

All 18 WAVs verified mono 24 kHz PCM16, finite/nonzero/unclipped during generation; saved headers and SHA-256 hashes reverified afterward. All nine case/setting pairs have byte-identical repeat WAVs. The 32-step curious line exactly matches the approved earlier preview hash `f009a45d718236fbd7c008eb8517322f311f0327ff88dd126d9c30d47868b998`, establishing the baseline was retained.

PyTorch post-warmup peak allocation 765458944 bytes, reserved 799014912 bytes. Peaks reset after loading/warmup; these are not startup or aggregate system peaks. Evaluation process exited successfully; GPU snapshot afterward returned 0 MiB. No evaluation worker retained.

## Files And Verification

- `tools/voice-evaluation/benchmark_f5_steps.py`: bounded offline benchmark; fresh D:-voice-lab output directory required; incremental JSON evidence, labelled engine/step/case/repeat filenames.
- `voice_worker.py`: synthesize accepts a keyword-only evaluation step parameter restricted to 16/24/32, default remains 32. Stdio protocol still calls synthesize with text only; production setting is unchanged and callers cannot change it through IPC.
- `test_voice_worker.py`: added four checks for default-step preservation, invalid-step rejection, bounded balanced schedule and rotated order. **12 worker/comparison + 28 preflight checks pass**. No C# source changes, build or deployment needed.
- Outputs: `D:\MitaAI\local\voice\step-comparison-2026-10-21\results.json` and 18 uniquely labelled WAVs. All runtime/temp/cache paths remain under the existing voice lab on D:.
- Existing nonfatal FFmpeg discovery warning appeared; WAV synthesis/decoding succeeded. No dependency installed just to silence it.

## Next Gate

### Longer-Line Follow-Up (Completed, Listening Pending)

User approved extending the audition. Added a `--case-set long` evaluation option with three original Russian texts (casual statement, question, expressive wording), each about 20 words and below the existing subtitle limit. No emotion-control engine or prompt framework added. Texts use plain Russian, with no manual stress markers, closer to normal model replies. Existing short-case default remains unchanged.

Same model/reference/seed/gain and rotated 32/24/16 schedule, two repetitions: 18 additional WAVs under `D:\MitaAI\local\voice\step-comparison-long-2026-10-21`. Initialization/imports 17.29 s; excluded from warm timing. Game closed, GPU initially 0 MiB used, no active/resident LLM concurrency established.

| Longer Case | Audio Duration | 32-Step Median | 24-Step Median | 16-Step Median |
| --- | --- | --- | --- | --- |
| Casual | 11.061 s | 5.940 s | 4.477 s | 2.993 s |
| Question | 10.624 s | 5.866 s | 4.398 s | 2.960 s |
| Expressive wording | 11.275 s | 6.042 s | 4.528 s | 3.039 s |

All 18 saved files reverified for SHA, mono 24 kHz format, finite/nonzero/unclipped samples and 10-12 s duration. Nine repeat pairs byte-identical. 28 preflight + 13 worker/comparison checks pass (added long-case balance/text bounds check). Evaluation process exited and GPU returned to 0 MiB. Post-warm-up PyTorch peak allocation 783260672 bytes, reserved 807403520 bytes; excludes loading peaks and other processes.

No production runtime configuration/default change or deployment; current speech remains 32 steps. No downloads or audio-effect changes. Need user listening on longer lines before a default choice; better generation timings do not fix LM Studio text latency, game-context hallucinations or prove equal voice quality.

User heard the curious-line previews and reports 24/32 sound identical, while 16 differs slightly but remains fine. They also point out the recording is only about three seconds. This is subjective approval of these samples only, not evidence across longer lines. 24 is a promising candidate; production remains 32 and no deployment authorization is inferred.

Subsequent longer-line verdict: user finds 24/32 almost identical, with some word/pitch variation. All long samples sound somewhat artificial but acceptable; user still prefers 16 as the effective speed/quality compromise. Preference recorded without changing production or claiming naturalness/artifact issues resolved. Alternative reference/RVC assets discussed separately; no engine or reference switch performed.

User listens to 32/24/16 versions, ideally both greeting and curious sentence, checking pronunciation, character similarity and rough/roomy artifacts. Do not select the faster setting merely because signal checks pass. If approved, make a separate bounded production-setting change and verify latency/pause/cancellation in the real game with the intended LM Studio setup. No full-turn speed or echo fix claimed here. Full professional project roadmap remains deferred.
