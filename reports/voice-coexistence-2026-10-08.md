# Local AI And GPU Voice Coexistence Evaluation

Test performed 8 October 2026; evidence and handoff finalized 9 October. Standalone evaluation only: no TTS integration, plugin deployment, UI changes or game/save edits.

## Scope And Configuration

The user approved the benchmark and confirmed MiSide was unpaused in gameplay with the laptop plugged in. MiSideFull PID 10760 remained running before each synthesis and at completion. The user reports Radeon 760M renders the game; this adapter assignment was not independently measured. Game frame rate was not measured, and subjective smoothness feedback is pending.

Existing RTX 3050 Laptop 6 GB and `local/voice/.venv-f5-gpu` were reused. F5 1.1.4 / Russian v2 ran on cuda:0, FP16, 32 diffusion steps, CFG 2, speed 1, seed 42 and six CPU threads. Existing reference, confirmed transcript, weights and Vocos were reused. No downloads, dependency installations or cleanup of retained files occurred.

Existing LM Studio and Llama 3.2 3B Instruct Q4_K_M (2,019,377,440-byte local model) were used. Temporary identifier: `mitaai-voice-benchmark`; full GPU offload, 2048-token context, default four parallel slots, TTL 900 seconds. Loopback server: port 1234. Two neutral benchmark requests used 128 maximum output tokens. These were not Mita conversations, were not saved to conversation history, and were not played in-game. Response text was not retained; only status, character count, usage and timing were recorded. The game's AI configuration was left unchanged.

## Results

| Case | Audio duration | Synthesis time | RTF |
| --- | ---: | ---: | ---: |
| LLM resident idle, first calm generation | 3.200 s | 8.955 s | 2.798 |
| LLM resident idle, warm curious generation | 3.531 s | 6.093 s | 1.726 |
| Active LLM request, warm calm generation | 3.200 s | 7.926 s | 2.477 |
| Active LLM request, warm curious generation | 3.531 s | 7.968 s | 2.257 |

Both local AI requests succeeded: 5.991 and 5.867 seconds, each generating 128 completion tokens. Their HTTP request intervals overlapped synchronized TTS inference by 5.989 and 5.865 seconds respectively. This establishes successful overlapping requests, not exact per-kernel scheduling or a complete dialogue-pipeline benchmark.

The sampled aggregate RTX memory peak was 4468 MiB (4.36 GiB), against about 2370 MiB before F5 loaded. Sampling includes initialization and inference, at roughly one-second intervals plus command overhead; it is not an instantaneous peak or proof of worst-case memory headroom. F5's separate PyTorch peaks were 2,082,945,536 bytes allocated and 2,103,443,456 reserved. No CUDA out-of-memory failure occurred in this bounded run.

Imports took 29.741 seconds and model loading 10.780 seconds. Previous voice-only warm generation was 3.11-3.47 seconds; the current loaded-idle/active conditions were slower. A persistent prewarmed worker remains necessary, but does not by itself remove shared-load inference latency. These four short utterances do not establish long-utterance speed, arbitrary LLM compatibility, game FPS or universal real-time readiness.

All eight raw/preview WAVs decode as non-silent, unclipped mono PCM16 at 24 kHz. Each hash matches the corresponding earlier standalone GPU output, including the user-approved curious preview SHA-256 `f009a45d718236fbd7c008eb8517322f311f0327ff88dd126d9c30d47868b998`. Sound approval remains specific to that preview; exact actor likeness is deferred.

## Failed First Attempt And Recovery

The first attempt's local AI requests failed with URLError: LM Studio's server/model did not remain available across separate command sessions. Its successful voice files do not prove coexistence and are not included in the table above. The underlying process-lifetime cause was not independently established.

The retry kept server startup, model loading, synthesis, unload and server shutdown in one command session. A new `/v1/models` availability guard checks the exact benchmark identifier before engine startup and before each utterance. Both guarded overlapping requests then succeeded. Failed and successful evidence remains retained separately; nothing was deleted.

## Changed Code And Verification

- `tools/voice-evaluation/benchmark_coexistence.py`: bounded loopback-only benchmark, exact game PID/name checks, required model availability, background AI requests, sampled GPU memory and explicit success/overlap assertions.
- `tools/voice-evaluation/test_russian_f5.py`: optional concurrent-request hook and interval-intersection metrics; default standalone generation remains unchanged.
- `tools/voice-evaluation/test_preflight.py`: five focused additional checks for loopback restriction, response metadata, failure handling, game identity and required model availability. All 28 checks passed, including a rerun on 9 October.

No plugin build/tests were run because plugin code was unchanged. Own benchmark model was unloaded and local server stopped after the run; MiSide was left open. The selected voice assets and original recordings were unchanged. No personal-memory entry was made.

## Evidence And Next Gate

- Successful measurements: `local/voice/gpu-coexistence-2026-10-08-retry/results.json`.
- Successful run log: `local/voice/setup/coexistence-benchmark-retry-2026-10-08.log`.
- Failed attempt: `local/voice/gpu-coexistence-2026-10-08/results.json` and `local/voice/setup/coexistence-benchmark-2026-10-08.log`.
- Prior voice-only benchmark: `reports/voice-gpu-evaluation-2026-10-08.md`.

Compatibility is demonstrated for this bounded configuration, with a material latency tradeoff. Obtain game-smoothness feedback and agree on acceptable response delay before integration. The normal LLM-then-TTS sequence can avoid deliberately simultaneous inference, but its full turn latency has not been measured here. Lower-step optimization, more English auditions and in-game speech integration remain separate future work requiring scope agreement. Keep text-only fallback and lifecycle cancellation in any eventual integration. The full professional project roadmap remains deferred.
