# Voice Evaluation Resume Readiness

Date: 20 October 2026. Resume inspection and bounded dependency-import test only.

## Recovered State

Read AGENTS, current HANDOFF, voice README/environment helper and relevant code in the installed NeuroMita archive. Git status contains the earlier handoff edits and two untracked 14 October research reports; do not discard them. No newer MitaAI implementation was identified in the inspected status/history. Game baseline remains v0.5.2, live text dialogue; no integrated TTS.

The immediate unfinished comparison is NeuroMita's installed Edge-TTS + RVC, not REA installation or map reconstruction. F5/Nano remain existing candidates, exact Mita cloning is optional, and no large downloads or integration were authorized by resumption.

## Verification Performed

- Reran existing `tools/voice-evaluation/test_preflight.py` using MitaAI's existing GPU environment: **28 tests pass**. No synthesis/model download.
- Read updated NeuroMita diagnostic lines, not private conversation content or API presets. Last log modification is 14 October 16:56. In addition to the earlier legacy `low` error, selected `edge_tts_rvc_cuda` requests at 16:23-16:24 also failed because the model was not initialized. No successful voice initialization/synthesis found in the inspected log.
- Later historical log also contains a local game-link bind failure on port 12345. That is a separate issue, not proof of a current listener or the voice-import cause. No processes were stopped or ports reconfigured.
- Used its existing embedded Python executable for a separate dependency-import probe with explicit paths to the installed RVC overlay, shared Torch base and core packages. Did not launch the app/bootstrap/installer. Used `-I -B`, MitaAI's D:-contained cache/temp environment and offline HF/Transformers settings; no model constructor, checkpoint deserialization, generated speech, API request or dependency change.

Runtime observed: **Python 3.12.9**, **Torch 2.7.1+cu128**, CUDA available, **NVIDIA GeForce RTX 3050 6GB Laptop GPU**. Torch import/device check took approximately **20.68 seconds** in this process; not a synthesis or whole-app startup measurement.

## Newly Reproduced Blocker

Resolving `from tts_with_rvc import TTS_RVC` fails along this chain:

`tts_with_rvc.inference -> vc_infer -> fairseq -> fairseq.dataclass.utils -> hydra.experimental`

Final exception:

```text
ImportError: cannot import name 'compose' from 'hydra.experimental'
```

The installed RVC overlay declares Hydra Core 1.4.0rc1. Its `hydra/experimental/__init__.py` does not expose the requested function. The embedded NeuroMita loader also resolves the package's `TTS_RVC` symbol; no Hydra experimental compatibility adaptation was found in the inspected archive source. This is concrete import incompatibility in the tested composition, not evidence of a weak GPU or a failed voice model.

It does not retroactively prove the cause of the earlier UI errors: those requests were rejected as uninitialized before successful runtime construction. Nor does it prove this is the only issue; further imports/model initialization may reveal others. Do not declare the whole app repaired after changing one dependency.

## Next Bounded Work

For the NeuroMita comparison, resolve the RVC dependency compatibility in a reversible isolated scope, then verify import, explicit initialization and a short voice sample before timing warmed requests. Do not upgrade global Python or MitaAI's working F5 environment, blindly reinstall all components, change game/UI/configuration, or delete the C: cache as an attempted import fix. Any new large download still requires size disclosure and internet confirmation.

Alternatively, resume MitaAI's existing F5 lower-step evaluation without depending on NeuroMita. This is a route choice, not evidence that NeuroMita or F5 wins on sound/latency.

## Changes And Limits

Only this report and HANDOFF were changed. No source/runtime/package/settings/game modifications, deployment, cleanup, personal-memory entry or lasting process/server. Voice comparison and integration remain unfinished. Full professional roadmap remains deferred.
