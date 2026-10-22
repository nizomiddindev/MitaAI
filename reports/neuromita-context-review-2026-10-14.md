# NeuroMita Context And Voice Review

Date: 14 October 2026. Research and user-screenshot interpretation only. No installation, inference, performance test or changes to either application's settings.

## Evidence Boundaries

Rechecked GitHub main with `git ls-remote`: `bff1361660c2d18eace4b7ac0af2d76072b59d97`, the same commit inspected on 7 October. Therefore do not claim source changes since that review. The user's much older failed installation may differ substantially. Screenshots label the running app `v2026.09.02.1`; matching this binary to the reviewed source is not established. GitHub API requests were rate-limited; direct pinned raw source and Git remote metadata remained available.

Reviewed the README/installation guide, AI Hub catalog and voice asset installer, backend specifications, Edge/Silero RVC adapter, TTS service, F5 adapter/pipeline and GPU scheduler. This is a targeted architecture/source review, not an exhaustive audit of every repository file or unpublished Unity implementation.

## Screenshot Findings

- AI Hub offers separate engine components and Mita voice packs. Edge-TTS + RVC (CUDA) is installing a shared `torch-cu128` backend, with package preparation still in progress in the screenshot. Completion/readiness is not proven.
- Sandbox shows a text reply with a Llama 3.2 3B model selected. This supports one successful displayed turn, not general provider reliability.
- Sandbox marks Game link Offline and Voice Error. These are separate statuses; text can work without either. The specific voice failure requires local logs, not speculation from a status badge.
- No generated audio, voice-similarity verdict, warm latency or GPU memory measurement supplied yet. No screenshots establish that the installer kept all caches on D:.

## Engine Versus Character Assets

Voice assets are separately downloadable character bundles, not entire TTS engines. The installer expects a trained `.pth` RVC checkpoint, optional `.onnx`/retrieval `.index`, and WAV/text references, with optional F5 cut references. The bundle description estimates Crazy Mita at roughly 250 MB; catalog sizes are informational, not measured downloads. A bundle being labelled installed does not establish that the selected backend can load every asset.

These trained conversion models explain a different route from our current reference-conditioned F5 audition: users need not train their own RVC model to test an existing bundle. However, availability is not evidence of permission to redistribute assets or exact actor likeness. Do not copy these bundles into MitaAI automatically.

Sources: [voice components](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/installables/voice_assets.py), [bundle installer](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/utils/voice_assets_installer.py).

## Edge-TTS Plus RVC

Pipeline: generated reply text -> online Edge speech -> local trained RVC conversion -> audio file/playback. The adapter selects SvetlanaNeural for Russian and MichelleNeural for English. CUDA RVC uses PyTorch; the shared backend specifies Torch/Torchaudio 2.7.1+cu128. This is separate from MitaAI's existing 2.6.0+cu124 environment; do not upgrade or mix our environment to match it.

RVC also needs shared speech-feature/pitch assets (HuBERT and RMVPE). Pitch extraction, FP16, index influence and pitch shift are configurable. Character conversion can improve timbre but cannot guarantee natural delivery or exact identity. The adapter logs synthesis elapsed time and rejects missing character checkpoints. Its displayed 3 GB minimum / 4 GB recommended VRAM numbers are author metadata, not this laptop's benchmark or a guarantee of coexistence with LM Studio.

Edge is an online service, even when the application calls the combination local voice. Reply text is sent outside the computer for base synthesis. It needs no separate Edge API key, but network availability and delay matter. Silero + RVC is a local-base-synthesis alternative in the same adapter; do not install additional components merely for comparison before the current route works.

Sources: [Edge/RVC adapter](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/handlers/voice_models/edge_tts_rvc_model.py), [backend specifications](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/core/backends/service.py), [shared RVC assets](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/handlers/voice_models/rvc_runtime_assets.py), [Edge upstream](https://github.com/rany2/edge-tts).

## Runtime And Architecture

The TTS service explicitly selects/initializes engines, performs optional end-to-end warm-up, rejects initialization if warm-up produces no valid file, and reuses the initialized handler. Model selection and synthesis share a scheduler critical section. Its scheduler prioritizes internal tasks, not external LM Studio inference. The adapter offloads Edge/RVC work to a thread and returns completed audio paths; this inspection does not establish low-latency streaming playback.

The F5 adapter still uses the Russian-specialized F5 family, with an earlier inference checkpoint than our v2 evaluation. Installed engine choice, character assets, reference and device settings must be recorded for fair comparison. No evidence shows NeuroMita's F5 is faster on this machine.

Sources: [TTS service](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/handlers/ai_engine/services/tts_service.py), [scheduler](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/handlers/ai_engine/gpu_scheduler.py), [F5 adapter](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/handlers/voice_models/f5_tts_model.py).

## Recommended Bounded Comparison

Let the existing installation finish; keep only the chosen engine and Crazy Mita pack initially. Select/save/init the matching engine in voice settings. If Voice Error remains, inspect the last initialization/warm-up error in `NeuroMitaLogs.log` before reinstalling or deleting anything. The app folder path is not yet provided; no local diagnosis performed.

Test one short Russian line, then two comparable later lines. Separate time waiting for LLM text from time between text availability and audible speech; note cold versus warm runs and sound preference. Test English separately if desired. Source speed labels alone are not measurements.

For MitaAI, runtime prewarming and engine/asset separation are useful precedents. Edge + optional RVC is a candidate only after sound and latency tests; an online base voice without conversion may also meet the user's now-explicit preference: natural, suitable sound and low delay, not mandatory Mita cloning. Preserve accepted F5/Nano installations and the working game UI. No integration is approved by this research.
