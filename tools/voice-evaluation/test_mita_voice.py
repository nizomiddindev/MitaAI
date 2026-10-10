"""Standalone Russian-reference voice audition. No game code or original audio edits."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import random
import sys
import time
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
MODEL_ID = "ResembleAI/chatterbox"
MODEL_REVISION = "5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18"
FILES = {
    "ve.pt": 5698626,
    "t3_mtl23ls_v3.safetensors": 2143989928,
    "s3gen.pt": 1057165844,
    "grapheme_mtl_merged_expanded_v1.json": 69989,
}
VOICE_ROOT = ROOT / "local/voice"
MODEL_DIR = VOICE_ROOT / "chatterbox-multilingual-v3"
REFERENCE = VOICE_ROOT / "references/mita_ru_intro.wav"
OUTPUT = VOICE_ROOT / "mita-samples"
SOURCE_DIR = ROOT / "MiSide_Dev/Data/LanguagesVoice/Russian-Dream Cast/LocationDialogue Location3"
SOURCE_NAMES = ("7.ogg", "8.ogg", "9.ogg")
SAMPLES = (
    ("01_ru_calm", "ru", "А, вот и ты. Как прошёл твой день?", 0.5),
    ("02_en_calm", "en", "Oh, there you are. How was your day?", 0.0),
    ("03_ru_curious", "ru", "Подожди, ты тоже это слышал? Интересно, что это было?", 0.5),
    ("04_en_curious", "en", "Wait, did you hear that too? I wonder what it was.", 0.0),
)


def missing_files(directory):
    return [name for name, size in FILES.items()
            if not (directory / name).is_file() or (directory / name).stat().st_size != size]


def prepare_reference():
    import numpy as np
    import soundfile as sf

    parts = []
    metadata = []
    sample_rate = None
    for name in SOURCE_NAMES:
        source = SOURCE_DIR / name
        audio, rate = sf.read(source, dtype="float32", always_2d=True)
        if not audio.size or not np.isfinite(audio).all():
            raise RuntimeError("Invalid source audio: " + str(source))
        if sample_rate is not None and sample_rate != rate:
            raise RuntimeError("Reference clips have inconsistent sample rates.")
        sample_rate = rate
        if parts:
            parts.append(np.zeros(int(rate * 0.15), dtype=np.float32))
        parts.append(audio.mean(axis=1))
        metadata.append({"path": str(source), "sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                         "duration_seconds": len(audio) / rate})
    combined = np.concatenate(parts)
    REFERENCE.parent.mkdir(parents=True, exist_ok=True)
    sf.write(str(REFERENCE), combined, sample_rate, subtype="PCM_16")
    report = {"output": str(REFERENCE), "sample_rate": sample_rate,
              "duration_seconds": len(combined) / sample_rate, "sources": metadata,
              "speaker_evidence": "Location3 introduction has a Mita section label in Russian localization; Voice.txt credits the main Mita actor.",
              "clip_index_alignment": "Source names selected from the contiguous opening block, not a verified per-object runtime voice binding.",
              "listening_confirmed": False, "originals_modified": False,
              "transform": "mono conversion, 150 ms silence between full clips; no pitch change or denoising"}
    REFERENCE.with_suffix(".json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2), flush=True)


def download(directory):
    from huggingface_hub import snapshot_download

    snapshot_download(repo_id=MODEL_ID, revision=MODEL_REVISION, allow_patterns=list(FILES),
                      local_dir=str(directory), max_workers=1)
    if missing_files(directory):
        raise RuntimeError("Model download incomplete: " + ", ".join(missing_files(directory)))


def make_previews(output):
    import numpy as np
    import soundfile as sf

    report_path = output / "results.json"
    report = json.loads(report_path.read_text(encoding="utf-8"))
    for sample in report["samples"]:
        audio, rate = sf.read(sample["file"], dtype="float32")
        peak = float(np.max(np.abs(audio)))
        if not peak or not np.isfinite(audio).all():
            raise RuntimeError("Invalid audition audio: " + sample["file"])
        gain = 0.8 / peak
        preview = output / ("preview_" + Path(sample["file"]).name)
        sf.write(str(preview), audio * gain, rate, subtype="PCM_16")
        sample.update({"listening_preview": str(preview), "preview_linear_gain": gain,
                       "raw_peak": peak, "preview_target_peak": 0.8})
    report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print("Saved volume-balanced previews; no pitch changes or original audio edits.", flush=True)


def generate(directory, output, reference, threads):
    if missing_files(directory):
        raise RuntimeError("Model files not ready: " + ", ".join(missing_files(directory)))
    if not reference.is_file():
        raise RuntimeError("Reference missing; run --prepare-reference first.")
    os.environ["HF_HUB_OFFLINE"] = "1"
    os.environ["TRANSFORMERS_OFFLINE"] = "1"
    import numpy as np
    import soundfile as sf
    import torch
    from chatterbox.mtl_tts import ChatterboxMultilingualTTS
    from chatterbox.models.tokenizers.tokenizer import ChineseCangjieConverter

    torch.set_num_threads(threads)
    random.seed(42)
    np.random.seed(42)
    torch.manual_seed(42)
    started = time.perf_counter()
    # English/Russian do not use Chinese segmentation. Avoid unrelated auto-downloads.
    with patch.object(ChineseCangjieConverter, "_load_cangjie_mapping", return_value=None), \
         patch.object(ChineseCangjieConverter, "_init_segmenter", return_value=None):
        model = ChatterboxMultilingualTTS.from_local(str(directory), device="cpu", t3_model="v3")
    load_seconds = time.perf_counter() - started
    started = time.perf_counter()
    model.prepare_conditionals(str(reference), exaggeration=0.5)
    reference_seconds = time.perf_counter() - started
    output.mkdir(parents=True, exist_ok=True)
    report = {"model": MODEL_ID, "model_revision": MODEL_REVISION, "variant": "multilingual-v3",
              "voice_reference": str(reference), "voice_reference_sha256": hashlib.sha256(reference.read_bytes()).hexdigest(),
              "device": "cpu", "cpu_threads": threads, "load_seconds": load_seconds,
              "reference_preparation_seconds": reference_seconds,
              "chinese_initialization_skipped": True, "listening_approved": False,
              "game_integration_tested": False, "samples": []}
    for name, language, text, cfg in SAMPLES:
        torch.manual_seed(42)
        started = time.perf_counter()
        with torch.inference_mode():
            wav = model.generate(text, language_id=language, exaggeration=0.5, cfg_weight=cfg)
        elapsed = time.perf_counter() - started
        audio = wav.detach().cpu().numpy().reshape(-1)
        if not audio.size or not np.isfinite(audio).all() or not np.any(audio):
            raise RuntimeError("Empty, silent or non-finite audio: " + name)
        path = output / (name + ".wav")
        sf.write(str(path), audio, model.sr, subtype="PCM_16")
        result = {"file": str(path), "text": text, "language": language, "cfg_weight": cfg,
                  "sample_rate": model.sr, "audio_seconds": audio.size / model.sr,
                  "generation_seconds": elapsed, "real_time_factor": elapsed / (audio.size / model.sr)}
        report["samples"].append(result)
        (output / "results.json").write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
        print(json.dumps(result, ensure_ascii=False), flush=True)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prepare-reference", action="store_true")
    parser.add_argument("--download", action="store_true")
    parser.add_argument("--generate", action="store_true")
    parser.add_argument("--previews", action="store_true", help="Create listening previews from already generated samples")
    parser.add_argument("--model-dir", type=Path, default=MODEL_DIR)
    parser.add_argument("--output-dir", type=Path, default=OUTPUT)
    parser.add_argument("--reference", type=Path, default=REFERENCE)
    parser.add_argument("--threads", type=int, choices=range(1, 13), default=6)
    args = parser.parse_args(argv)
    print(json.dumps({"model": MODEL_ID, "revision": MODEL_REVISION, "variant": "multilingual-v3",
                      "selected_download_bytes": sum(FILES.values()),
                      "missing_files": missing_files(args.model_dir), "reference": str(args.reference)}, indent=2), flush=True)
    if args.prepare_reference:
        prepare_reference()
    if args.download:
        download(args.model_dir)
    if args.generate:
        generate(args.model_dir, args.output_dir, args.reference, args.threads)
    if args.previews:
        make_previews(args.output_dir)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("Mita voice audition failed: " + str(error), file=sys.stderr)
        sys.exit(1)
