# NeuroMita Installed Folder Inspection

Date: 14 October 2026. Root: `D:\PythonBuild-v2026.09.02.1\PythonBuild-v2026.09.02.1`.

## Scope

Read-only recursive file inventory, archive-source inspection, WAV headers/hashes, selected installation manifests/settings and diagnostic log lines. The embedded `NeuroMita.pyz` contains 635 entries and reports version 2026.09.02.1. Read it as a ZIP without executing its modules. Inspected voice references, Edge/RVC dependencies, installer/cache handling, runtime composition, initialization/warm-up, and the presence of memory/RAG/dialogue services. This is not a full audit of every third-party library, an audio-quality assessment or a runtime performance test.

No app execution, model loading/deserialization, downloads, installation, configuration changes, cleanup, copying voice assets or MitaAI code changes. Did not reproduce private chat history, API keys, character prompts or the copyrighted reference transcript. Only inspection documentation changed.

## What CrazyMita.wav Actually Is

`Models/CrazyMita.wav` is a reference recording: **41.563719 seconds, stereo PCM16, 44.1 kHz, 7,331,918 bytes**. Its paired `CrazyMita.txt` supplies the reference transcript. The user identifies the recording as joined game dialogue; the inspection establishes format/duration and source usage, not the historical training dataset or acoustic actor identity.

The bundled resolver `utils/__init__.py:106-153` calls this `clone_voice_filename`. Fish Speech's adapter (lines 819-841) supplies it and its transcript as inference reference conditioning. F5's adapter (lines 819-828) prefers the separate cut, falling back to this WAV only if that cut is missing. OmniVoice (lines 358-373) likewise prefers a complete cut/reference-text pair, then the long pair.

`Models/CrazyMita_Cuts/CrazyMita_default.wav` is **11.888617 seconds, mono PCM16, 44.1 kHz, 1,048,654 bytes**, with a separate 265-byte transcript. This is the preferred existing F5 reference. A longer recording is not automatically a better reference; no synthesis or listening comparison using it was performed.

In Edge-TTS + RVC mode, the adapter records reference paths on its parent but conversion uses the trained checkpoint and optional retrieval index, not this WAV as per-turn cloning input. The installed `tts-with-rvc` code calls Edge `Communicate` for base synthesis and loads the selected RVC model for conversion. Downloading the bundle does not start training. Whether these same recordings originally trained the checkpoint cannot be established from the installed package.

### Asset Inventory

| File | Bytes | Purpose |
| --- | ---: | --- |
| CrazyMita.pth | 55,193,209 | Trained PyTorch RVC checkpoint |
| CrazyMita.onnx | 110,944,084 | Conversion model for the ONNX route |
| CrazyMita.index | 194,874,819 | Retrieval index used to refine conversion |
| CrazyMita.wav | 7,331,918 | Longer reference audio |
| CrazyMita.txt | 1,078 | Paired reference transcript |
| CrazyMita_default.wav | 1,048,654 | Preferred short F5/Omni reference |
| CrazyMita_default.txt | 265 | Short reference transcript |

The checkpoint was not loaded; weights' size/extension and the adapter's usage establish their role, not quality or training provenance. Asset availability is not unrestricted redistribution permission.

### Recorded Audio Hashes

- Long reference: `fb8a742f79b7a03bacf3382a5ff0c97fe05d4cb1770f1e2f54fa61114aa1f03c`.
- Short cut: `79e66369e2aac829a7dcb525fa29b68bfbee7b1cd22301c3a5acce9cf5a3d5e6`.
- RVC checkpoint: `5e3b06547714ffd5e726f4713c652601ecf9c6a9eaf00c4cf784ec7382b75e44`.
- ONNX model: `ab241a89deedc5ba3d321f517e8fee10bfcbbf566001e8198f8602743ea6ce73`.
- Index: `357498e6c301b0ce2a02d2dc1cc093ff4e90879a3b4804d5cbcf058603418255`.

## Disk Usage And The C: Reduction

Total application inventory: **39,264 files, 8,062,134,571 logical bytes (7.508 GiB)** at this snapshot. Approximate main contributors:

| Location | Logical size |
| --- | ---: |
| D: app / Lib | 6.56 GiB |
| Lib/environment/bases | 5.501 GiB, included in Lib |
| Lib/environment/overlays | 0.542 GiB, included in Lib |
| Models | 0.343 GiB |
| Root HuBERT + RMVPE files | 0.345 GiB combined |
| Root ffmpeg.exe | 0.157 GiB |

Installed PyTorch is on **D:** under `Lib/environment/bases/torch-cu128-2.7.1-cu128-py312-win-amd64-029b747846/site-packages`. The engine-specific packages are in a separate overlay. Torch/Torchaudio 2.7.1+cu128, edge-tts 7.3.1, tts-with-rvc 0.1.10, fairseq-built 0.12.4 and scipy 1.12.0 metadata are present. Backend and overlay manifests both mark state ready; this is installation metadata, not proof that speaking works.

However, **`C:\Users\nvidi\AppData\Local\uv\cache` contains about 6.16 GiB**, of which about 6.07 GiB has modification timestamps on 14 October. Its archive cache contains Torch 2.7.1+cu128 and large CUDA DLLs written at the installation time: torch_cuda.dll alone is 1.31 billion bytes. These observations strongly associate much of the C: usage with NeuroMita's package cache. Installed D: dependencies and retained C: cached package contents are separate locations.

Other checked locations: pip cache ~0.16 GiB and user Temp ~0.24 GiB. The usual `.cache/huggingface`, `.cache/torch` and Local/Roaming NeuroMita paths were absent at inspection. C: reported 48,099,778,560 free bytes (~44.8 GiB), consistent with the user's current reading. No previous full-drive snapshot exists: the entire reported reduction cannot be attributed exactly. File totals are logical sizes, not guaranteed reclaimable physical allocation.

The bundled installer uses uv and permits its cache by default; no UV_CACHE_DIR redirection was found in the inspected application archive/run bootstrap. MitaAI's voice environment already explicitly redirects uv/pip/HF/Torch/temp paths to D:, so this leak should not be copied into our design. No cache was removed or relocated. A later separately approved cleanup should wait for installation to finish and use package-manager cache handling, not delete the installed D: runtime.

## Current Readiness And Log Findings

`Settings/settings.json` currently selects local Russian speech with `NM_CURRENT_VOICEOVER=edge_tts_rvc_cuda`; `LOCAL_VOICE_LOAD_LAST` is false. Do not interpret other UI-section flags as proof that speech is disabled without tracing their consumers. Registry/manifests establish an installed engine environment, not runtime initialization.

The saved log records an earlier 16:05:25 failure: local voice model `low` was not initialized. That occurred before later installation and the current saved engine choice, so it is not proof that the current engine will fail. No subsequent successful warm-up/synthesis measurement was found in the inspected log.

At 16:13:53 installation warns that the RVC stack targets Python 3.11 and may be unstable with embedded Python 3.12.9. This is a known warning, not a demonstrated runtime incompatibility here. Do not switch Python or reinstall speculatively.

Earlier repeated FFmpeg startup checks attempted to replace an installation thread that had not stopped within two seconds, raising uncaught task-supervisor exceptions. FFmpeg is now present. These historical exceptions do not establish that present voice initialization fails for the same reason.

The source explicitly requires initialization and, by default, a successful generated warm-up file before accepting readiness. The next bounded action is select/init the installed engine and produce a short line, collecting the resulting log if it fails. No fix was applied during this inspection.

## What MitaAI Can Learn Or Potentially Replace

| Candidate | Judgment |
| --- | --- |
| Edge preset voice alone | Worth auditioning: avoids local base synthesis and needs no conversion if sound is suitable. Online/privacy/reliability tradeoff remains. Not benchmarked here. |
| Edge + trained RVC | Worth comparing after NeuroMita initialization succeeds. May provide preferable timbre; no proof of faster latency or less VRAM than our F5. |
| Existing shorter F5 reference | Possible audition input later, subject to scope/rights review. Does not require retraining; no automatic similarity or latency improvement. |
| Persistent speech service and warm-up | Adopt the pattern when integration is approved. MitaAI currently has evaluation scripts, not an in-game speech worker. |
| Separate engine and character asset installation | Useful future launcher pattern; no reason to replace the working injected UI. |
| Shared backend plus engine overlays | Reduces repeated backend installations within their app. Our voice lab has retained multiple isolated audition environments; that is deliberate safety during evaluation, not proof a larger runtime framework is needed now. |
| Memory/RAG/DSL systems | Present in the application source, but not locally validated or required for the current voice gate. Do not transplant the entire framework into the Unity plugin. |

Keep our real-game injection, text dialogue, history, lifecycle ownership and story priority. NeuroMita's standalone architecture is not a drop-in substitute for those parts. Exact Mita cloning is optional per the user; sound and measured delay decide the final voice engine. F5/Nano remain working audition candidates until another route passes listening and performance gates.

Code reuse requires attribution/license review; voice assets and underlying game materials have separate rights. The published [NeuroMita license](https://github.com/VinerX/NeuroMita/blob/main/Licence.md) is not a blanket license to its game-derived voice assets. No source or assets imported into MitaAI.

## Next Steps

1. Initialize the installed Edge/RVC engine, test one short line and inspect fresh logs if it fails.
2. Measure cold versus warm time after text becomes available, separate from LLM waiting time; record sound preference and concurrent GPU use.
3. Decide whether Edge alone, Edge/RVC or F5 best fits the user. No claim of replacement superiority yet.
4. Address the C: cache separately with explicit cleanup/relocation scope; no deletion approved by this inspection request.

Full professional project roadmap remains deferred. This is an inspection report, not integration approval or a new implementation stage.
