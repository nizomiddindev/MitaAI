# Voice Evaluation: English

Date: 6 October 2026. Status: standalone English speech generated successfully; no in-game integration or listening approval yet.

## User Decision

The user confirmed English for the first voice test and wants a lively character voice, not the assistant-like Kokoro voices they auditioned. Codex is handling this work; Antigravity should not make simultaneous overlapping edits. Preserve working text dialogue and evaluate sound outside the game before integration.

## Observed Laptop

Local hardware queries identified a Ryzen 5 8645HS, NVIDIA RTX 3050 6 GB Laptop GPU, and 16,357,150,720 bytes of system RAM. Python 3.14 and `uv` are present. The offline readiness check found no Torch, Chatterbox, Hugging Face Hub or SoundFile packages in the queried Python environment. Other environments were not exhaustively searched.

The GPU was idle during inspection. This is not evidence of available GPU memory during MiSide plus LM Studio. CPU-first Nano evaluation is a resource-conscious candidate, not a verified speed or sound-quality guarantee.

## Shortlist

| Candidate | Model download estimate | Reason to evaluate / limitation |
| --- | --- | --- |
| Chatterbox Nano | 1.94 GB selected files | First resource-conscious English test. Bundled conditioning allows an engine proof without game clips. Voice identity and expressiveness still require listening. |
| Chatterbox Turbo | 2.99 GB necessary weights, plus tokenizer files | Alternative English engine if Nano disappoints. Not automatically better for this character; heavier download. |
| Qwen3-TTS 0.6B CustomVoice | 2.49 GB weights including speech tokenizer | Preset voices, but this variant has no instruction-based style control. Not a substitute for an expressive voice-design model. |
| Qwen3-TTS 1.7B CustomVoice / VoiceDesign | 4.52 GB for CustomVoice weights including tokenizer; VoiceDesign size not checked | Larger family includes style instructions and voice design. Defer for slow internet/shared laptop resources. |

Nano/Turbo provide discrete vocal tags, not the original Chatterbox model's exaggeration/CFG controls. Upstream tests Chatterbox on Python 3.11/Debian; this Windows setup remains unverified. The Qwen comparison is based on its official model table, not a local audition. Sources: [Chatterbox model card](https://huggingface.co/ResembleAI/chatterbox-nano), [loader implementation](https://github.com/resemble-ai/chatterbox/blob/5de7a54aa4e5e2baadb0182dde554908b48b85c2/src/chatterbox/tts_turbo.py), [Qwen documentation](https://github.com/QwenLM/Qwen3-TTS).

Download figures come from official Hugging Face file-size metadata queried locally, not parameter-count guesses: [Nano metadata](https://huggingface.co/api/models/ResembleAI/chatterbox-nano?blobs=true), [Turbo metadata](https://huggingface.co/api/models/ResembleAI/chatterbox-turbo?blobs=true), [Qwen 0.6B metadata](https://huggingface.co/api/models/Qwen/Qwen3-TTS-12Hz-0.6B-CustomVoice?blobs=true), [Qwen 1.7B metadata](https://huggingface.co/api/models/Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice?blobs=true). Sizes are decimal GB, exclude Python/PyTorch/other dependencies, and do not predict runtime RAM/VRAM requirements.

## Selective Nano Download

Pinned model revision: `71ccd1d0081b430592cea481f4307e764e07bc64`.
Pinned engine source revision: `5de7a54aa4e5e2baadb0182dde554908b48b85c2`.

| File | Bytes |
| --- | ---: |
| s3gen_meanflow.safetensors | 1,064,875,036 |
| t3_nano_v1.safetensors | 869,899,204 |
| ve.safetensors | 5,695,784 |
| conds.pt | 169,454 |
| merges.txt | 456,318 |
| vocab.json | 999,186 |
| added_tokens.json | 418 |
| special_tokens_map.json | 470 |
| tokenizer_config.json | 3,878 |
| Total | 1,942,099,748 |

The current loader uses the meanflow decoder, not `s3gen.safetensors` (1,056,484,620 bytes). Upstream's broad download patterns include both. The evaluation tool explicitly omits the unused file. User authorized download/install provided all new downloads stay inside a separate project folder on D:.

## Prepared And Verified

- Added `tools/voice-evaluation/test_english.py`: offline preflight by default; explicit download and generation modes; three original English samples, one model load, timing report and nonempty audio checks.
- Added `tools/voice-evaluation/test_preflight.py`: six focused offline tests pass. These are separate from the plugin's managed checks, not additions to its historical count.
- Added `Set-VoiceEnvironment.ps1` to redirect UV/PIP/Hugging Face/Torch/Numba caches, managed Python installation and TEMP/TMP into `D:\MitaAI\local\voice`. Checked effective interpreter base, venv, temp and cache paths; all point to D:.
- Installed Python 3.11.16, CPU Torch/Torchaudio 2.6.0 and Chatterbox 0.1.7 from the pinned archive. The package's Perth dependency resolved to commit `ff1c8ac55a976971245cdd53c18d6131ca00d993`. Nano import and inference succeeded on Windows. All nine selected files match the pinned metadata sizes. No unused decoder downloaded.
- No game files, plugin code, configuration, installed DLL, or saves changed. Three samples generated; listening approval remains pending.
- Reproduction instructions are in `tools/voice-evaluation/README.md`. No GPU speech environment installed.

## Actual CPU Results

Generated `local/voice/samples/results.json` records six CPU threads, model loading 4.6078 seconds (excluding module import startup), and these results:

| Sample | Audio duration | Generation time | Real-time factor |
| --- | ---: | ---: | ---: |
| 01_calm.wav | 3.00 s | 14.78 s | 4.93 |
| 02_curious.wav | 3.28 s | 4.25 s | 1.29 |
| 03_playful.wav | 3.36 s | 4.37 s | 1.30 |

The first sample includes cold-generation overhead. The warm samples took longer to generate than their playback duration. This is not an acceptable basis for claiming instant replies or upstream advertised speed on this laptop. No concurrent game/LLM test or peak memory measurement has been performed.

SoundFile inspection confirmed three mono 24 kHz PCM16 WAVs with finite nonzero samples; peak amplitudes were approximately 0.292, 0.442 and 0.658. Correct pronunciation, intended vocal tags, speaker identity and character suitability still require listening. No in-game playback test performed. No speech process left running.

Network timeouts and temporary DNS failures occurred during download; the downloader recovered and resumed automatically. A nonfatal Diffusers deprecation warning occurred during inference; no unrelated dependency refactor performed.

All managed Python, environment, model files, samples, caches and temporary paths were directed under `D:\MitaAI\local\voice`. Approximate logical file sizes after setup: model 1.94 GB, environment 1.93 GB, caches 2.02 GB, Python 0.073 GB, total 5.97 GB. Caches are retained to avoid repeat downloads; no cleanup performed.

## Next Acceptance Gates

1. Download/install authorization obtained; required location on D: respected.
2. Isolated CPU samples generated; timing above measured. Offline preparation checks: six passing on both Python environments.
3. User approves the sound. Bundled conditioning is not Mita's actor and may not suit the character. Reference-conditioned evaluation, if chosen, should use a permitted recording; the mixed game voice directory is not an automatically selected dataset.
4. Measure co-running performance with MiSide/LM Studio before choosing CPU/GPU deployment.
5. Integrate only the accepted engine and voice through a local worker, keeping the Unity plugin thin. Preserve cancellation and text-only recovery before adding fine subtitle/audio timing.

No game-actor voice cloning, STT, emotional-direction framework, lip sync, world actions, disk memory or autonomy implemented in this evaluation.
