# Standalone Voice Evaluation

## Current Integration Prototype

User subsequently authorized MitaAI integration and stopped NeuroMita troubleshooting. Plugin v0.6.0 starts `voice_worker.py` with the existing `.venv-f5-gpu` environment, prewarms offline F5 Russian, then exchanges request-ID-tagged JSON/WAV through owned stdin/stdout. It does not download anything or expose a network server. New voice caches/temp remain inside this D: lab. Plugin text/UI is preserved; audio uses a separate mod-owned AudioSource with lifecycle cancellation and pause support. English remains text-only for now.

The original audition scripts below remain standalone. `voice_worker.py` is now the integration entry point, not an installer or an audition CLI. 28 existing offline checks plus 8 checks in `test_voice_worker.py` pass. Real C# client smoke verifies engine reuse, 3.195/3.193-second warm generations, rejection and cancellation; startup including warm-up 25.552 seconds. In-game audible speech/controls regression verification remains pending. See `reports/voice-integration-0.6.0.md`. The earlier notes below describe prior evaluation stages, not current integration authorization.

Standalone evaluation only. This does not change the MiSide plugin, launcher, settings, saves, subtitles, or installed DLL. The user rejected the sampled Kokoro voices; do not integrate Kokoro as an accepted voice.

## Latest Coexistence Result (8 October; Verified 9 October)

The approved bounded test ran MiSide with existing LM Studio Llama 3.2 3B Q4_K_M and GPU F5. Two successful AI requests overlapped speech generation. Warm TTS took 6.09 s with the LLM resident idle and 7.93-7.97 s under active inference; previous voice-only warm timing was 3.11-3.47 s. Sampled aggregate RTX memory peaked at 4468 MiB. No OOM occurred; game remained running, but FPS was not measured and smoothness feedback is pending. This is compatibility evidence with a latency tradeoff, not integration or universal real-time readiness.

`benchmark_coexistence.py` requires a running game, explicit gameplay confirmation and the exact loaded local model. It permits only loopback HTTP, records response metadata rather than text, and checks successful request overlap. Start/load/test/unload/stop in one command session: the first separate-session attempt lost its server and failed. Do not rerun merely to replay audio. All 28 focused checks pass. All eight retry WAV hashes match previous GPU auditions. No new downloads or game/config changes; own temporary model/server cleaned up. Full evidence and limitations: `reports/voice-coexistence-2026-10-08.md`. Integration remains unapproved/unimplemented.

The sections below record the earlier staged evaluations; their historical pending-test statements are superseded by this result.

## Current State

- Languages: English fallback and provisional Russian voice; TTS is not integrated.
- Candidate: Chatterbox Nano is user-accepted as an English fallback, not the preferred Mita imitation.
- Python 3.11.16, CPU PyTorch 2.6.0, Chatterbox 0.1.7 and all nine selected Nano files are installed under `local/voice` on D:. Three English WAV samples were generated successfully. Chatterbox Multilingual Russian/English auditions were subsequently rejected by the user; do not integrate them.
- Nano's bundled voice conditioning was the initial English engine check, not a promise of a Mita-like voice. Later F5 auditions use a human-confirmed Russian game reference; originals remain untouched.
- Hardware observed: Ryzen 5 8645HS, RTX 3050 Laptop 6 GB, approximately 16 GB RAM. User reports Radeon 760M renders the game. F5 has a separate working CUDA environment, `.venv-f5-gpu`, while `.venv-f5` remains CPU-only. The bounded coexistence measurements and remaining limitations are above.
- Selected model files: 1,942,099,748 bytes, approximately 1.94 GB decimal, excluding dependencies. Slow internet makes download size a user decision.

Research, alternatives and acceptance gates: `reports/voice-evaluation-2026-10-06.md`.

## GPU Setup And Recovered Benchmark

Completed 7 October; installation/artifacts reverified after the battery shutdown on 8 October. Do not redownload or recreate the ready GPU environment. The user approved the disclosed 2.54 GB CUDA wheel transfer after initially postponing it. Ask about internet readiness before any future large download.

Python 3.11.16, F5 1.1.4, Torch/Torchaudio 2.6.0+cu124 are installed under `local/voice/.venv-f5-gpu`. Existing model files, Vocos and the confirmed reference/transcript are reused. CPU F5 and Chatterbox environments are unchanged. `CUDA_CACHE_PATH` now also points into the D: voice lab. Package sources/hashes are in `local/voice/setup/cuda-wheels.json`; the reproduced non-Torch dependencies are in `local/voice/setup/f5-existing-dependencies.txt`.

```powershell
. ./tools/voice-evaluation/Set-VoiceEnvironment.ps1
# Offline checks/readiness only:
local/voice/.venv-f5-gpu/Scripts/python.exe tools/voice-evaluation/test_preflight.py
local/voice/.venv-f5-gpu/Scripts/python.exe tools/voice-evaluation/test_russian_f5.py --device cuda
# Already completed; only rerun for a deliberate new timing test:
local/voice/.venv-f5-gpu/Scripts/python.exe tools/voice-evaluation/test_russian_f5.py --device cuda --generate --steps 32 --repeats 2
```

Rerunning synthesis replaces the named **GPU** samples/results, not the separate CPU outputs. Use `--output-dir` with another directory under `local/voice` to retain earlier GPU results. Readiness does not load the model or establish actual GPU availability; synthesis explicitly refuses unavailable CUDA and verifies model/vocoder device placement.

The RTX run used upstream-selected FP16, 32 steps, CFG 2, speed 1 and seed 42. First generation 5.31 s; warm generations 3.47/3.11/3.35 s for 3.531/3.20/3.531-second audio. Peak PyTorch allocation 1.94 GiB, reservation 1.96 GiB, including model loading; not total GPU/system usage. Initial imports took 72.12 s and model loading 3.95 s separately. A future speech worker must stay loaded rather than restarting per turn. No joint game/LLM benchmark yet.

Four raw WAVs and four volume-balanced previews are under `local/voice/f5-russian-gpu-samples`. The matching curious preview is `preview_f5_russian_v2_gpu_02_ru_curious.wav`, explicitly approved by the user for sound on 8 October. All files decode as finite, non-silent, unclipped mono 24 kHz PCM16; hashes match after restart. Exact actor matching and concurrent game/LLM performance remain unverified. **23 offline checks pass.** See `reports/voice-gpu-evaluation-2026-10-08.md`; TTS integration remains unapproved/unimplemented.

## Actual First Run

- CPU, six threads; model loading 4.61 seconds, excluding Python/module import startup.
- First generation: 14.78 seconds for 3.00 seconds of audio (cold overhead).
- Warm generations: 4.25 and 4.37 seconds for 3.28 and 3.36 seconds of audio. Not faster than real time on this run; upstream speed claims are not this laptop's benchmark.
- Output: three non-silent, finite, mono PCM16 WAVs at 24 kHz. Peak amplitudes are below full scale. This verifies valid audio, not correct pronunciation, tag realization, speaker identity or user preference.
- Download recovered from transient timeouts/DNS failures; no manual restart was needed. Future setup uses one download worker and longer timeouts.
- Folder logical file-size total approximately 5.97 GB: environment 1.93 GB, caches 2.02 GB, model 1.94 GB, managed Python 0.073 GB, plus tiny samples. No cache cleanup was performed.
- No speech process or server left running; no game integration performed.

## Offline Checks

From `D:\MitaAI`:

```powershell
. ./tools/voice-evaluation/Set-VoiceEnvironment.ps1
python tools/voice-evaluation/test_preflight.py
python tools/voice-evaluation/test_english.py
```

The second command only prints readiness. Neither command installs dependencies, downloads weights, loads a model or touches game files. The offline checks can run on the existing Python 3.14; the engine environment should use Python 3.11 to align with upstream's tested environment.

## Step Comparison (21 October)

Longer follow-up: `--case-set long` generates three original plain-Russian lines, each about 11 s audio. Completed outputs in local/voice/step-comparison-long-2026-10-21; timings and verification in the same step report. 41 Python checks pass. Longer-line user listening pending, production remains 32 steps. Use a fresh output folder for any intentional regeneration; saved audio is already available for replay.

Isolated 32/24/16-step comparison is complete; lower-step listening approval remains pending. Production worker still uses 32 steps. New benchmark_f5_steps.py loads one existing engine, warms each setting, rotates two repetitions of three cases, and records labelled WAVs/timings/hashes in a fresh voice-lab folder. No download or game/config changes. Results: local/voice/step-comparison-2026-10-21/results.json; report: reports/voice-step-comparison-2026-10-21.md. 28 preflight and 12 worker/comparison checks pass. Do not rerun to replay files or infer audible quality from those checks.

## Next Agent: Local Test Procedure

These commands have run successfully on this Windows machine, including model download and sample generation. Keep all dependencies isolated; do not change the global Python environment. `uv` is already installed. Dot-source the environment script every time to keep runtime/cache/temp paths on D:; its changes apply only to the current process and child processes. The existing environment is ready; do not reinstall just to replay or generate samples.

```powershell
. ./tools/voice-evaluation/Set-VoiceEnvironment.ps1
uv venv --python 3.11 local/voice/.venv
uv pip install --python local/voice/.venv/Scripts/python.exe torch==2.6.0 torchaudio==2.6.0 --index-url https://download.pytorch.org/whl/cpu
uv pip install --python local/voice/.venv/Scripts/python.exe "https://github.com/resemble-ai/chatterbox/archive/5de7a54aa4e5e2baadb0182dde554908b48b85c2.zip"
local/voice/.venv/Scripts/python.exe tools/voice-evaluation/test_english.py --download
local/voice/.venv/Scripts/python.exe tools/voice-evaluation/test_english.py --generate
```

For another generation run, only dot-source `Set-VoiceEnvironment.ps1` and run the last command. It uses local files offline, not the hosted demo quota. The three named output WAVs and `results.json` are replaced on each successful rerun.

The upstream package also depends on Perth from Git. If setup fails, record the exact problem and fix only this isolated environment. Do not silently substitute another engine or claim Windows compatibility is verified.

The test pins the Nano model revision and requests an explicit nine-file list, skipping the unused `s3gen.safetensors`. It uses `from_local`, not upstream's broad `from_pretrained` download. Size checks detect missing/truncated files; Hugging Face's downloader handles transfer/cache integrity. Size checks alone are not cryptographic verification.

Generation loads the model once, uses its bundled conditioning and saves three original English samples in `local/voice/samples`. `results.json` records load time, generation time, duration and real-time factor (generation seconds divided by audio seconds; lower is faster). The first sample includes cold-generation overhead. A successful file write is not a listening-quality approval. Offline mode is set during model loading; if an auxiliary asset is missing, investigate and report its download rather than silently removing the guard.

Do not add emotion sliders: Nano/Turbo ignore the original model's exaggeration and CFG controls. `[chuckle]` is a discrete sound tag, not general emotional direction. Leave upstream watermarking enabled.

## Before Integration

### Current Gate: Continuous Russian Reference (7 October)

The user rejected the generated multilingual voices for poor similarity and English-accented Russian. A successful WAV export is not an accepted voice. No game changes are part of the current work.

`audit_reference.py` scans the installed Russian pack and prepares one continuous candidate from Location3/20.ogg. All 2,669 clips were scanned: longest 5.654 seconds; only one exceeds five seconds. The chosen opening Mita candidate is 4.492 seconds, not a newly found long recording. The user subsequently confirmed the speaker, clarity and exact transcript.

```powershell
. ./tools/voice-evaluation/Set-VoiceEnvironment.ps1
local/voice/.venv/Scripts/python.exe tools/voice-evaluation/audit_reference.py --prepare
local/voice/.venv/Scripts/python.exe tools/voice-evaluation/test_preflight.py
```

All 18 current checks pass (including four F5 checks added after the original 14). Output is `local/voice/reference-audit/audit.json`, `mita_ru_continuous.wav` and `preview_mita_ru_continuous.wav`. The preview uses constant volume gain only; no game recordings are edited. Regenerating the audit resets confirmations: record them again only from actual human listening, not assumed metadata.

### F5 Russian V2 CPU Audition (Provisional Sound Acceptance)

Selected model/vocabulary/Vocos files downloaded: 1,402,816,013 bytes (~1.40 GB), excluding dependencies; model license CC-BY-NC-4.0. F5 1.1.4 is installed in a separate `.venv-f5` with the CLI dependencies pinned in `requirements-f5-inference.txt`; existing Chatterbox untouched. Its import path also requires trainer packages even though no training runs. Gradio/bitsandbytes are intentionally omitted; this is not the full demo/training environment.

```powershell
. ./tools/voice-evaluation/Set-VoiceEnvironment.ps1
# Readiness only, no model loading or download:
local/voice/.venv-f5/Scripts/python.exe tools/voice-evaluation/test_russian_f5.py
# Already ran; very slow on CPU, not necessary just to listen:
local/voice/.venv-f5/Scripts/python.exe tools/voice-evaluation/test_russian_f5.py --generate
```

Two finite/non-silent mono PCM16 24 kHz samples generated, with a confirmed transcript and no ASR. CPU six threads, 32 steps: 124.72 s for 3.20 s audio; 124.58 s for 3.531 s. This CPU route is **not suitable for live gameplay**. Load 4.88 s excludes import startup. The later separate GPU results are above; shared-game performance remains unmeasured.

The first listening response says it sounds like the old bad-accent sample. Existing Chatterbox and F5 previews had the same basename, but hashes/durations prove different audio files. That does not prove better sound or an app cache bug. Unique F5 preview copies are now `local/voice/f5-russian-samples/preview_f5_russian_v2_01_ru_calm.wav` and `preview_f5_russian_v2_02_ru_curious.wav`. Future F5 runs use that prefix. Original `results.json` keeps original generated paths; copies are byte-identical.

User subsequently provisionally accepted `preview_f5_russian_v2_02_ru_curious.wav`'s sound and authorized the standalone GPU setup/test, not integration. The GPU curious preview also received explicit sound approval on 8 October. Exact Mita similarity is deferred; a concurrent resource test remains before integration approval. Model source/revisions/setup obstacle: `reports/voice-reference-audit-2026-10-07.md`. Full project roadmap remains deferred.

### Historical Russian-Reference Follow-Up (Rejected)

`test_mita_voice.py` now uses the existing engine with a selective four-file Chatterbox Multilingual V3 download (3,206,924,387 bytes) in `local/voice/chatterbox-multilingual-v3`. A 7.633-second reference combines opening Location3 clips 7/8/9 without altering game files. Provenance and source hashes are in `local/voice/references/mita_ru_intro.json`; per-object voice binding and subjective speaker identity are not independently verified. Ten offline checks pass.

Four Russian/English original-text samples are in `local/voice/mita-samples`. Generation on six CPU threads took 19.00-23.44 seconds per 2.76-3.88-second line. This is not real-time-ready. `results.json` contains exact parameters and timing. Volume-balanced `preview_*.wav` files target peak amplitude 0.8 and retain original durations; raw outputs are retained. Actor similarity/pronunciation/artifacts require listening; no game integration or GPU test performed.

The test temporarily skips unused Chinese lookup/segmentation initialization to avoid auto-downloads. Optional Russian stress labeling is absent. Upstream handled a reference frame-alignment warning; dependency deprecation warnings were nonfatal. No installed engine files modified to address these warnings. All new files are on D:, and original OGG hashes still match. The requested full professional technical roadmap is deferred, not replaced by these operational notes.

To rerun without downloading again:

```powershell
. ./tools/voice-evaluation/Set-VoiceEnvironment.ps1
local/voice/.venv/Scripts/python.exe tools/voice-evaluation/test_mita_voice.py --generate --previews
```

This replaces named generated samples, not original game recordings. Obtain the user's listening verdict before additional engine downloads or game changes.

1. Listen to the samples and get the user's approval of the voice, not just the engine. If the bundled voice is unsuitable, evaluate a permitted expressive reference or an original designed voice before modifying the game.
2. Review the bounded coexistence measurements above, obtain game-smoothness feedback and agree on latency; full-turn timing is still unmeasured.
3. Only then add a separate local speech worker, bounded asynchronous synthesis, and a main-thread Unity audio adapter.
4. Preserve text-only fallback, request/scene/target ownership, story priority, pause handling and cancellation. Never speak provider errors or stale replies. Subtitles must not wait forever on failed synthesis.
5. Update `HANDOFF.md` with changed files, commands/results, listening approval and limitations. Stop other agents from editing the same files concurrently.
