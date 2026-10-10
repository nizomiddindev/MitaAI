# Russian Voice Reference Audit

Date: 7 October 2026. Standalone evaluation only; not the requested full project roadmap.

Current outcome: reference speaker/transcript confirmed by the user; F5 Russian v2 synthesis completed, but generated voice is not approved and CPU latency is unsuitable for gameplay. Detailed results below.

## Listening Verdict

The user rejected both demonstrated Chatterbox Multilingual V3 auditions. Russian pronunciation sounded English-accented and actor similarity was unsatisfactory. File validity/timing checks from 6 October remain valid, but they do not establish perceptual quality. Nano English remains an acceptable fallback. No voice is integrated in MiSide.

## Local Reference Evidence

- Audited 2,669 OGGs using the installed SoundFile reader. Longest 5.653855 s: Location1/36.ogg. Only that clip exceeds 5 s. Most game lines are short; the pack cannot supply a single 8-15-second clip.
- Candidate: `D:\MitaAI\MiSide_Dev\Data\LanguagesVoice\Russian-Dream Cast\LocationDialogue Location3\20.ogg`, one continuous 4.492188-second recording, 48 kHz.
- Source SHA-256: `6478eb278fb9bfbb7aab3b53dad22927ad1b0c97e822f160ae0f7b98084f425c`, unchanged after preparation. Source peak 0.082948, RMS 0.016095. Signal statistics alone cannot prove a clean voice or absence of music.
- The opening localization comments mark Mita, and numbered OGGs follow a physical-line pattern with gaps for comment/blank lines. Candidate line 20 has an explicit Mita section hint. This is a **transcript hypothesis**, not a verified audio-to-text alignment. Voice.txt credits Indominus Rex for main Mita, but credits alone do not establish this clip's speaker identity.
- Exported mono PCM16 reference and a volume-balanced preview (peak target 0.8, constant gain approximately 9.645). No stitching, denoising, pitch/time changes. Outputs and detailed metadata: `D:\MitaAI\local\voice\reference-audit`.
- User confirmed desired voice and clarity, then separately confirmed the proposed transcript exactly. Recorded these human confirmations only after source/reference hashes were rechecked. This is not automated acoustic analysis or runtime binding verification.

## Distinct Next Audition

Candidate: [F5-TTS_RUSSIAN model card](https://huggingface.co/Misha24-10/F5-TTS_RUSSIAN). Author describes Russian-focused fine-tuning and stress control; v2 adds further training and data filtering. These are author claims, not this project's listening results. Use short original Russian text first, not cross-language English.

The [official F5 inference API](https://github.com/SWivid/F5-TTS/blob/main/src/f5_tts/api.py) accepts reference audio and reference text. Providing accurate reference text avoids the automatic ASR path. Explicit local checkpoint, vocabulary and vocoder paths should avoid unrelated/default weight downloads. Do not use blank reference text or remove offline guards to bypass missing assets.

Selected files verified against Hugging Face metadata and downloaded after reference confirmation (about 7m44s):

| Repository / pinned revision | File | Bytes |
| --- | --- | ---: |
| Misha24-10/F5-TTS_RUSSIAN / ea166adeae4c80ec5ee423a671e2bdb83906cf84 | F5TTS_v1_Base_v2/model_last_inference.safetensors | 1,348,435,761 |
| Same | F5TTS_v1_Base/vocab.txt | 13,800 |
| charactr/vocos-mel-24khz / 0feb3fdd929bcd6649e0e7c5a688cf7dd012ef21 | pytorch_model.bin | 54,365,991 |
| Same | config.yaml | 461 |
| **Total** | Excludes inference software | **1,402,816,013** |

F5 Russian weights are CC-BY-NC-4.0. Open-source inference code does not make the weights unrestricted for commercial distribution. No game recordings/models are being packaged for distribution in this audition.

Dependency handling: installed F5 1.1.4 in a separate `D:\MitaAI\local\voice\.venv-f5`, reusing cached CPU Torch/Torchaudio 2.6.0. This version avoids the newer TorchCodec dependency. CLI requirements are pinned in `tools/voice-evaluation/requirements-f5-inference.txt`; no Chatterbox packages changed. Initial import failed because F5 also imports its trainer; added wandb 0.19.10, accelerate 1.4.0 and datasets 3.3.2 without executing training. `WANDB_MODE=disabled`; all related cache/temp paths directed into the voice lab. No Gradio/bitsandbytes, automatic ASR/stress models or CUDA. This is not a complete upstream training/demo installation.

F5 wheel SHA-256 matched PyPI: `6F68495A74A0010746A75EE010D87983918C19E58F8A7C26168F354707E25B38`. Runtime versions are recorded in `local/voice/f5-russian-samples/results.json`.

## Generated Audition Evidence

- Six CPU threads, 32 steps, CFG 2, speed 1, seed 42; F5 v1 architecture with Russian v2 weights and their vocabulary. Reference is one continuous clip with the human-confirmed transcript. Model load 4.88198 s, excluding Python imports.
- Calm original sentence: 3.200 s audio generated in 124.72484 s, RTF 38.98. Curious original sentence: 3.53067 s audio in 124.57985 s, RTF 35.29. Manual stress markers supplied in generation text, recorded separately from readable text. These are far too slow for real-time conversation.
- Raw WAVs verified finite, non-silent, unclipped, mono PCM16 at 24 kHz. Constant-gain previews have identical duration and peak approximately 0.80002 (PCM16 quantization), no pitch/time modification. Original/reference hashes still match. All 18 preparation checks pass. No speech process left running.
- Nonfatal pydub FFmpeg warning: waveform input/output worked without downloading FFmpeg. Upstream adds sentence punctuation to the transcript during preprocessing; no engine files modified just to suppress that output.
- First listening verdict: user says the sample sounds like the old bad-accent preview. Identical basenames caused ambiguity. Old Chatterbox preview is 2.76 s / SHA-256 `e191736b9bec8a3edaca3d511176425dc62bfc27ba2e91d33b78e93e19e52fcd`; F5 is 3.20 s / `038505076640cb95ff63f929ff01160650401cc0eafe1d00905a4a9cf1fd0118`. They are different recordings, but that does not show improved accent/similarity or prove UI cache misrouting.
- Added distinct F5 preview names, without regeneration: `D:\MitaAI\local\voice\f5-russian-samples\preview_f5_russian_v2_01_ru_calm.wav` and `preview_f5_russian_v2_02_ru_curious.wav`. Copies are byte-identical to the respective F5 previews; future synthesis uses the distinct prefix. Generated voice remains **unapproved**.

## Verification And Next Gate

- Audit, confirmation and F5 synthesis ran successfully; all 18 checks pass (ten previous, four audit and four F5).
- Downloads/setup stayed in the D: voice lab. No plugin/game/UI/config/saves changes; no TTS integration.
- Next gate is an unambiguous quality verdict on uniquely labelled audio. If still wrong, stop treating F5 as a viable voice; do not repeatedly swap/download models without a justified approach. If accepted, discuss GPU download/resource requirements separately before speed work. No guarantee of exact actor reproduction; CPU performance demonstrably fails real-time readiness.
- Preserve rejected audition files as evidence. Full technical roadmap remains deferred at the user's request.
