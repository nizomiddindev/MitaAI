"""Standalone Nano evaluation. No network or model loading without an explicit flag."""

import argparse
import importlib.util
import json
import os
from pathlib import Path
import random
import sys
import time


MODEL_ID = "ResembleAI/chatterbox-nano"
MODEL_REVISION = "71ccd1d0081b430592cea481f4307e764e07bc64"
SOURCE_REVISION = "5de7a54aa4e5e2baadb0182dde554908b48b85c2"
FILES = {
    "conds.pt": 169454,
    "s3gen_meanflow.safetensors": 1064875036,
    "t3_nano_v1.safetensors": 869899204,
    "ve.safetensors": 5695784,
    "merges.txt": 456318,
    "vocab.json": 999186,
    "added_tokens.json": 418,
    "special_tokens_map.json": 470,
    "tokenizer_config.json": 3878,
}
SAMPLES = (
    ("01_calm", "Oh, there you are. I was wondering where you went."),
    ("02_curious", "Wait, did you hear that too? What do you think it was?"),
    ("03_playful", "[chuckle] You checked that door three times. It is still a door."),
)
ROOT = Path(__file__).resolve().parents[2]


def missing_files(directory):
    return [name for name, size in FILES.items()
            if not (directory / name).is_file() or (directory / name).stat().st_size != size]


def preflight(directory):
    return {
        "model": MODEL_ID,
        "model_revision": MODEL_REVISION,
        "source_revision": SOURCE_REVISION,
        "language": "English",
        "model_directory": str(directory),
        "selected_download_bytes": sum(FILES.values()),
        "excluded_unused_file": "s3gen.safetensors",
        "missing_or_wrong_size_files": missing_files(directory),
        "python": sys.version.split()[0],
        "dependencies_present": {
            name: importlib.util.find_spec(name) is not None
            for name in ("torch", "chatterbox", "huggingface_hub", "soundfile")
        },
        "audio_generated": False,
    }


def download(directory):
    from huggingface_hub import snapshot_download

    snapshot_download(repo_id=MODEL_ID, revision=MODEL_REVISION,
                      allow_patterns=list(FILES), local_dir=str(directory), max_workers=1)
    missing = missing_files(directory)
    if missing:
        raise RuntimeError("Download incomplete or wrong file sizes: " + ", ".join(missing))


def generate(directory, output, device, threads):
    missing = missing_files(directory)
    if missing:
        raise RuntimeError("Model files not ready: " + ", ".join(missing))

    # Keep loading offline: the pinned, selective download is a separate operation.
    os.environ["HF_HUB_OFFLINE"] = "1"
    os.environ["TRANSFORMERS_OFFLINE"] = "1"
    import numpy as np
    import soundfile as sf
    import torch
    from chatterbox.tts_turbo import ChatterboxTurboTTS

    if device == "cuda" and not torch.cuda.is_available():
        raise RuntimeError("CUDA was requested but this Python environment cannot use it.")
    torch.set_num_threads(threads)
    random.seed(42)
    np.random.seed(42)
    torch.manual_seed(42)
    started = time.perf_counter()
    model = ChatterboxTurboTTS.from_local(str(directory), device=device, nano=True)
    load_seconds = time.perf_counter() - started
    if model.conds is None:
        raise RuntimeError("Built-in voice conditioning was not loaded. No game clip is used.")

    output.mkdir(parents=True, exist_ok=True)
    results = []
    for name, text in SAMPLES:
        started = time.perf_counter()
        with torch.inference_mode():
            wav = model.generate(text)
        if device == "cuda":
            torch.cuda.synchronize()
        elapsed = time.perf_counter() - started
        audio = wav.detach().cpu().numpy().reshape(-1)
        if not audio.size or not np.isfinite(audio).all() or not np.any(audio):
            raise RuntimeError("Empty, silent or non-finite audio for " + name)
        duration = audio.size / model.sr
        path = output / (name + ".wav")
        sf.write(str(path), audio, model.sr, subtype="PCM_16")
        results.append({"file": str(path), "text": text, "audio_seconds": duration,
                        "generation_seconds": elapsed,
                        "real_time_factor": elapsed / duration})
        print(json.dumps(results[-1]), flush=True)

    report = {"model": MODEL_ID, "model_revision": MODEL_REVISION,
              "source_revision_expected": SOURCE_REVISION,
              "device": device, "cpu_threads": threads, "load_seconds": load_seconds,
              "voice": "bundled conditioning; not Mita's actor",
              "sample_rate": model.sr, "samples": results,
              "listening_approved": False, "game_integration_tested": False}
    (output / "results.json").write_text(json.dumps(report, indent=2), encoding="utf-8")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", type=Path, default=ROOT / "local/voice/chatterbox-nano")
    parser.add_argument("--output-dir", type=Path, default=ROOT / "local/voice/samples")
    parser.add_argument("--download", action="store_true", help="Explicitly download ~1.94 GB of model files")
    parser.add_argument("--generate", action="store_true", help="Load local model and save three WAV samples")
    parser.add_argument("--device", choices=("cpu", "cuda"), default="cpu")
    parser.add_argument("--threads", type=int, choices=range(1, 13), default=6)
    args = parser.parse_args(argv)
    print(json.dumps(preflight(args.model_dir), indent=2), flush=True)
    if args.download:
        download(args.model_dir)
    if args.generate:
        generate(args.model_dir, args.output_dir, args.device, args.threads)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        print("Voice evaluation failed: " + str(error), file=sys.stderr)
        sys.exit(1)
