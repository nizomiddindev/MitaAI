# NeuroMita Voice Research

Date: 7 October 2026. Research/discussion only; no new engine installation or game integration.

## Scope And Evidence

Reviewed public NeuroMita source at commit `bff1361660c2d18eace4b7ac0af2d76072b59d97`. Findings below describe source capabilities, not reproduced runtime performance, voice similarity, asset rights or compatibility with this laptop. Source review does not establish why its author moved from the archived injected mod to standalone Unity.

## Observed Architecture

- Selectable engines include F5 Russian, Cross-Lingual F5, Fish Speech, OmniVoice, Edge-TTS and Silero, with several optional RVC combinations. Russian F5 uses `Misha24-10/F5-TTS_RUSSIAN`'s earlier v1 inference checkpoint; MitaAI already has the same model family's v2 weights. F5 exposes CUDA/CPU selection; its AMD/Intel choices fall back to CPU, unlike its separate RVC device options.
- RVC converts generated speech's timbre after base synthesis. It requires separate character conversion assets; a short reference clip is not itself a trained RVC model. Conversion adds processing and cannot guarantee exact actor likeness or correct upstream pronunciation.
- A generic TTS service lists/selects/initializes models, warms them up and generates audio without coupling callers to one engine. The F5 pipeline retains initialized model/vocoder instances rather than reloading them for every line.
- An internal GPU scheduler coordinates this application's own tasks. It does not control external LM Studio processes or guarantee that independent applications fit in VRAM.
- Installation is component-selective: model manager, dependency/file checks, backend/device settings, progress and separately downloadable character voice assets. Its size/VRAM catalog values are author estimates, not measurements for MitaAI.
- Edge-TTS uses an online Microsoft service; local RVC does not make that whole pipeline offline. Silero/Piper provide potential local pretrained-voice alternatives. Their sound still requires audition and their model/voice licenses require individual review. NeuroMita character bundle distribution is not proof of actor permission or unrestricted reuse.

## Recommendation, Not Implementation

Prioritize the user's reported Radeon-game / RTX-voice setup. Make the accepted Russian F5 voice run on CUDA using existing weights, then measure warmed generation latency and peak VRAM. Repeat alongside MiSide and the chosen LLM once; avoid a broad hardware-compatibility project. Reuse the existing Nano English fallback, with further English auditions after GPU readiness.

Later profiles can be lightweight (pretrained Silero or Piper), main (GPU F5 Russian / Nano English), and advanced (optional RVC or another genuinely better auditioned engine). These names express resource budgets, not guaranteed quality rankings. Start with one working backend; do not install all candidates now. A persistent separate voice worker, one bounded queue and request/scene/target ownership fit the thin Unity plugin. Keep text usable when speech fails or is interrupted by the story.

Extend the existing Windows launcher later instead of building a competing installer now. Potential scope: voice/language/device selection, disclosed download sizes, installation folder/free-space checks, resumable downloads, progress, readiness checks and test voice. All speech files/cache/temp remain under `D:\MitaAI\local\voice`. Model licensing, actor-reference permissions and redistribution must be checked before a public voice pack.

## Current Gate

User provisionally accepts `preview_f5_russian_v2_02_ru_curious.wav`'s sound, not the current CPU latency and not game integration. Exact Mita similarity is deferred. No GPU runtime download or benchmark has occurred. Next proposed work requires a size disclosure and approval; no paid server is necessary for this local route. The comprehensive project roadmap remains deferred.

## Primary Sources

- [NeuroMita F5 adapter](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/handlers/voice_models/f5_tts_model.py)
- [Edge/Silero and RVC adapter](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/handlers/voice_models/edge_tts_rvc_model.py)
- [TTS service](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/handlers/ai_engine/services/tts_service.py)
- [F5 pipeline](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/handlers/voice_models/pipelines/f5_pipeline.py)
- [GPU scheduler](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/handlers/ai_engine/gpu_scheduler.py)
- [Model manager installation guide](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/docs/LocalVoiceInstallationEn.md)
- [Character assets](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/installables/voice_assets.py)
- [Asset catalog](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/src/installables/catalog_manifest.py)
- [Silero project](https://github.com/snakers4/silero-models)
- [Piper voices and per-voice licenses](https://github.com/OHF-Voice/piper1-gpl/blob/main/docs/VOICES.md)
- [Edge-TTS project](https://github.com/rany2/edge-tts)
