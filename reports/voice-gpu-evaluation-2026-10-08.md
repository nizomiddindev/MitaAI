# F5 Russian GPU Evaluation And Shutdown Recovery

Report date: 8 October 2026. Timed synthesis: 7 October. Post-restart integrity/runtime checks: 8 October. Standalone evaluation only; no game integration, deployment or game/save edits.

Subsequent user verdict, 8 October: `preview_f5_russian_v2_gpu_02_ru_curious.wav` is explicitly accepted for sound. This passes that preview's listening gate, not exact actor matching, joint game/LLM testing or integration approval. The result metadata records the named preview, hash and approval date; audio files and timing values are unchanged.

## State Recovered

The GPU download, environment installation, CUDA computation check, four timed synthesis calls and WAV verification had all completed before the laptop lost battery power. Only handoff/report completion remained. Saved logs and results were inspected rather than regenerating samples or treating old handoff text as current evidence.

On 8 October both downloaded wheel hashes/sizes and all eight generated WAV hashes matched saved evidence. CUDA computation still succeeds; all eight WAVs decode with the expected signal/format/duration. The original CPU environment remains Torch/Torchaudio 2.6.0+cpu. No download, reinstall or synthesis rerun was needed after restart.

## Installation

1. User authorized GPU evaluation. The approximately 2.54 GB transfer was disclosed; the user initially postponed it, then explicitly requested continuation. Future large downloads must also wait for internet-readiness confirmation.
2. Created `D:\MitaAI\local\voice\.venv-f5-gpu` using the existing managed Python 3.11.16. Existing CPU F5 and Chatterbox environments remain separate and unchanged.
3. Recorded/reproduced 119 existing non-Torch distribution versions. Offline cache-only installation missed some exact wheel URLs; seven small package wheels were fetched at the same pinned versions. No model or package-version substitution was required. This is the existing CLI inference dependency set, not a complete upstream demo/training installation.
4. Downloaded official CPython 3.11 Windows x64 Torch/Torchaudio 2.6.0+cu124 wheels into `local/voice/downloads`, verified SHA-256 against official index links, then installed offline with dependency resolution disabled. Combined wheel size: **2,536,514,006 bytes**, excluding small dependency wheels/metadata. No separate CUDA Toolkit or NVIDIA driver installation.
5. Reused F5 Russian v2, Vocos and the confirmed continuous recording/transcript. No model re-download, ASR, training, RVC or upstream engine-file edits. Added `CUDA_CACHE_PATH` under the existing D: voice cache; the environment helper redirects all intended downloads/runtime/cache/temp outputs to the voice lab.

| Package | Bytes | Official SHA-256 |
| --- | ---: | --- |
| Torch 2.6.0+cu124 | 2,532,350,702 | `6a1fb2714e9323f11edb6e8abf7aad5f79e45ad25c081cde87681a18d99c29eb` |
| Torchaudio 2.6.0+cu124 | 4,163,304 | `a25e146ce66ea9a6aed39008cc2001891bdf75253af479a4c32096678b2073b3` |

Official [installation matrix](https://pytorch.org/get-started/previous-versions/), [Torch index](https://download.pytorch.org/whl/cu124/torch/) and [Torchaudio index](https://download.pytorch.org/whl/cu124/torchaudio/). Actual sources are recorded in `local/voice/setup/cuda-wheels.json`.

## Code Changes

- `tools/voice-evaluation/test_russian_f5.py`: explicit `--device cpu|cuda`, separate default GPU output directory/preview prefix, one-to-three repetitions with unique filenames, fail-closed CUDA availability, model/vocoder placement verification, synchronized GPU timing, import/load measurements, CUDA memory metrics and cold-generation identification. CPU default behavior and previous outputs are retained.
- `tools/voice-evaluation/test_preflight.py`: five additional offline checks for separate output paths, voice-lab containment, readiness without synthesis/download, CUDA refusal rather than fallback, and unique bounded repetitions.
- `tools/voice-evaluation/Set-VoiceEnvironment.ps1`: D:-contained CUDA cache directory.
- Recovery documentation: AGENTS, HANDOFF, voice README, implementation model's current-stage note and this report. No Unity/plugin/launcher/UI/config changes or builds were required.

## Measured Results

Hardware: NVIDIA GeForce RTX 3050 6 GB Laptop GPU, compute capability 8.6, observed driver 592.82. CUDA runtime 12.4. F5 model and Vocos verified on CUDA; model dtype FP16 as selected by the unmodified upstream loader. Same reference, manual stress markers, 32 diffusion steps, CFG 2, speed 1, seed 42 and six CPU threads as the preceding CPU audition, except device/precision.

| Call | Audio Duration | Generation Time | RTF |
| --- | ---: | ---: | ---: |
| Calm, first generation | 3.200 s | 5.310 s | 1.659 |
| Curious, warm | 3.531 s | 3.468 s | 0.982 |
| Calm, warm repeat | 3.200 s | 3.110 s | 0.972 |
| Curious, warm repeat | 3.531 s | 3.347 s | 0.948 |

RTF means generation time divided by audio duration. These three warm calls are slightly faster than playback duration, not a guarantee for longer messages, simultaneous workloads, or end-to-end LLM-to-audio delay. CPU curious generation was 124.58 s; the GPU curious calls are approximately 36-37 times faster for this sentence.

Import startup: **72.116 s**. Model loading after imports: **3.952 s**. These are separate from synthesis latency; do not present warm generation as cold startup. The future voice service should initialize/prewarm once and stay resident.

Peak PyTorch allocated: **2,082,945,536 bytes (1.94 GiB)**. Peak reserved: **2,103,443,456 bytes (1.96 GiB)**. Statistics cover this process including model loading, not all driver/other-application memory. No simultaneous MiSide/LM Studio benchmark. User-reported Radeon game rendering is not independently verified.

## Verification And Evidence

- **23 standalone offline tests pass**, including the five new GPU guards. No game tests or deployment were performed because no game code changed.
- Four raw WAVs plus four gain-only previews: mono 24 kHz PCM16, finite/non-silent, peaks below full scale and expected durations. Same-seed repeats are byte-identical within the GPU run. This checks output integrity, not pronunciation, emotional quality or actor likeness.
- Source game recording and confirmed reference hashes remain unchanged. Previous CPU outputs were retained in their separate directory. The GPU curious preview subsequently received direct listening approval; no approval is inferred merely from FP16 or CPU acceptance.
- A nonfatal missing-FFmpeg warning appeared from Pydub. WAV conversion succeeded without it; no extra installation was made just to silence the warning.
- Saved evidence: `local/voice/setup/f5-existing-dependencies.txt`, `cuda-wheels.json`, `f5-gpu-benchmark.log`, `f5-gpu-audio-verification.json`, `restart-verification-2026-10-08.json`, and `local/voice/f5-russian-gpu-samples/results.json`.
- Post-restart verification did not overwrite timed results or repeat generation. No speech service/process is intentionally left running. No personal-memory entry created; the comprehensive project roadmap remains deferred.

## Reuse And Next Gate

No setup or download is needed to listen. Matching GPU curious preview: `D:\MitaAI\local\voice\f5-russian-gpu-samples\preview_f5_russian_v2_gpu_02_ru_curious.wav`.

For a deliberately new benchmark only:

```powershell
. ./tools/voice-evaluation/Set-VoiceEnvironment.ps1
local/voice/.venv-f5-gpu/Scripts/python.exe tools/voice-evaluation/test_preflight.py
local/voice/.venv-f5-gpu/Scripts/python.exe tools/voice-evaluation/test_russian_f5.py --device cuda --generate --steps 32 --repeats 2 --output-dir local/voice/f5-russian-gpu-next-benchmark
```

Next: measure coexistence with the user's game/chosen LLM before requesting integration approval; the GPU curious preview's listening gate has passed. More English auditions remain requested, but were not performed in this GPU setup task. Do not add another engine, a new installer or game audio hooks automatically.
