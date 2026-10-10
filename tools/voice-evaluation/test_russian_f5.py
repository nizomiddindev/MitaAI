"""Standalone F5 Russian audition. Default is offline preflight; never edits the game."""

import argparse
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import sys
import time
from unittest.mock import patch

from audit_reference import validate_output

ROOT = Path(__file__).resolve().parents[2]
VOICE_ROOT = ROOT / "local/voice"
MODEL_ID = "Misha24-10/F5-TTS_RUSSIAN"
MODEL_REVISION = "ea166adeae4c80ec5ee423a671e2bdb83906cf84"
MODEL_DIR = VOICE_ROOT / "f5-russian-v2"
FILES = {"F5TTS_v1_Base_v2/model_last_inference.safetensors": 1348435761,
         "F5TTS_v1_Base/vocab.txt": 13800}
VOCODER_ID = "charactr/vocos-mel-24khz"
VOCODER_REVISION = "0feb3fdd929bcd6649e0e7c5a688cf7dd012ef21"
VOCODER_DIR = VOICE_ROOT / "vocos-mel-24khz"
VOCODER_FILES = {"pytorch_model.bin": 54365991, "config.yaml": 461}
ENGINE_VERSION = "1.1.4"
AUDIT = VOICE_ROOT / "reference-audit/audit.json"
OUTPUT = VOICE_ROOT / "f5-russian-samples"
GPU_OUTPUT = VOICE_ROOT / "f5-russian-gpu-samples"
SAMPLES = (
    ("01_ru_calm", "А, вот и ты. Как прошёл твой день?", "А, вот и ты. Как прош+ёл твой день?"),
    ("02_ru_curious", "Подожди, я что-то слышала. Ты тоже?", "Подожд+и, я чт+о-то сл+ышала. Ты т+оже?"),
)


def missing_files(directory, expected):
    return [name for name, size in expected.items()
            if not (directory / name).is_file() or (directory / name).stat().st_size != size]


def validated_reference(audit_path):
    report = json.loads(audit_path.read_text(encoding="utf-8"))
    candidate = report["candidate"]
    for key in ("speaker_listening_confirmed", "spoken_transcript_confirmed", "background_audio_checked"):
        if candidate.get(key) is not True:
            raise RuntimeError("Reference confirmation missing: " + key)
    if not candidate.get("transcript_hypothesis", "").strip():
        raise RuntimeError("Reference transcript must be non-empty; automatic ASR is disabled.")
    reference = validate_output(Path(candidate["reference"]))
    if hashlib.sha256(reference.read_bytes()).hexdigest() != candidate["reference_sha256"]:
        raise RuntimeError("Reference changed after listening confirmation.")
    source = Path(candidate["source"])
    if hashlib.sha256(source.read_bytes()).hexdigest() != candidate["source_sha256"]:
        raise RuntimeError("Original game recording changed after listening confirmation.")
    return reference, candidate


def download():
    from huggingface_hub import snapshot_download

    for model, revision, directory, expected in (
        (MODEL_ID, MODEL_REVISION, MODEL_DIR, FILES),
        (VOCODER_ID, VOCODER_REVISION, VOCODER_DIR, VOCODER_FILES),
    ):
        snapshot_download(repo_id=model, revision=revision, allow_patterns=list(expected),
                          local_dir=str(directory), max_workers=1)
        if missing_files(directory, expected):
            raise RuntimeError("Incomplete download: " + model)


def evaluation_output(device, output):
    return validate_output(output if output is not None else GPU_OUTPUT if device == "cuda" else OUTPUT)


def generation_cases(repeats):
    if not 1 <= repeats <= 3:
        raise ValueError("Choose one to three audition repetitions.")
    return [(name if repeat == 1 else f"{name}_repeat{repeat:02}", text, model_text)
            for repeat in range(1, repeats + 1) for name, text, model_text in SAMPLES]


def device_details(torch, device):
    if device == "cpu":
        return {"name": "CPU"}
    if device != "cuda":
        raise ValueError("Only CPU and CUDA are supported by this audition.")
    if not torch.cuda.is_available():
        raise RuntimeError("CUDA requested but unavailable; use the GPU environment. No CPU fallback.")
    properties = torch.cuda.get_device_properties(0)
    torch.cuda.reset_peak_memory_stats()
    return {"name": properties.name, "total_memory_bytes": properties.total_memory,
            "capability": list(torch.cuda.get_device_capability(0)),
            "cuda_runtime": torch.version.cuda}


def generate(output, threads, steps, device="cpu", repeats=1, concurrent_request=None):
    missing = missing_files(MODEL_DIR, FILES) + missing_files(VOCODER_DIR, VOCODER_FILES)
    if missing:
        raise RuntimeError("Model files not ready: " + ", ".join(missing))
    output = validate_output(output)
    reference, candidate = validated_reference(AUDIT)
    if importlib.metadata.version("f5-tts") != ENGINE_VERSION:
        raise RuntimeError("Use the pinned isolated F5 1.1.4 inference environment.")
    os.environ["HF_HUB_OFFLINE"] = "1"
    os.environ["TRANSFORMERS_OFFLINE"] = "1"
    import_started = time.perf_counter()
    import numpy as np
    import soundfile as sf
    import torch
    from f5_tts.api import F5TTS
    import f5_tts.infer.utils_infer as utils
    import_seconds = time.perf_counter() - import_started

    torch.set_num_threads(threads)
    details = device_details(torch, device)
    if device == "cuda":
        torch.cuda.synchronize()
    started = time.perf_counter()
    model = F5TTS(model="F5TTS_v1_Base", ckpt_file=str(MODEL_DIR / next(iter(FILES))),
                  vocab_file=str(MODEL_DIR / "F5TTS_v1_Base/vocab.txt"),
                  vocoder_local_path=str(VOCODER_DIR), device=device)
    if device == "cuda":
        torch.cuda.synchronize()
    load_seconds = time.perf_counter() - started
    model_parameter = next(model.ema_model.parameters())
    if model_parameter.device.type != device or next(model.vocoder.parameters()).device.type != device:
        raise RuntimeError("Model or vocoder is not on the requested device.")
    output.mkdir(parents=True, exist_ok=True)
    report = {"model": MODEL_ID, "model_revision": MODEL_REVISION, "variant": "v2",
              "engine_version": ENGINE_VERSION, "vocoder_revision": VOCODER_REVISION,
              "model_license": "CC-BY-NC-4.0", "device": device, "device_details": details,
              "model_device": str(model_parameter.device), "model_dtype": str(model_parameter.dtype),
              "cpu_threads": threads, "repeats": repeats, "import_seconds": import_seconds,
              "steps": steps, "cfg_strength": 2.0, "speed": 1.0,
              "load_seconds": load_seconds, "reference": str(reference),
              "reference_sha256": candidate["reference_sha256"],
              "reference_transcript": candidate["transcript_hypothesis"],
              "reference_user_confirmed": True, "generated_voice_listening_approved": False,
              "game_integration_tested": False, "automatic_asr_disabled": True, "samples": []}
    report["runtime"] = {"python": sys.executable, "torch": torch.__version__,
                         "packages": {name: importlib.metadata.version(name) for name in
                                      ("f5-tts", "transformers", "vocos", "numpy", "soundfile", "datasets", "wandb")},
                         "paths": {key: os.environ.get(key) for key in
                                   ("TEMP", "HF_HOME", "MPLCONFIGDIR", "CACHED_PATH_CACHE_ROOT", "WANDB_DIR", "PYTHONPYCACHEPREFIX", "CUDA_CACHE_PATH")}}
    print(json.dumps({"device": device, "device_details": details, "model_device": str(model_parameter.device),
                      "model_dtype": str(model_parameter.dtype), "import_seconds": import_seconds,
                      "load_seconds": load_seconds}, indent=2), flush=True)
    for index, (name, text, model_text) in enumerate(generation_cases(repeats)):
        pending_request = concurrent_request(index) if concurrent_request is not None else None
        if device == "cuda":
            torch.cuda.synchronize()
        started = time.perf_counter()
        # A confirmed transcript avoids ASR; fail closed if upstream tries it anyway.
        with patch.object(utils, "transcribe", side_effect=RuntimeError("Unexpected automatic ASR request")), \
             torch.inference_mode():
            wav, rate, _ = model.infer(ref_file=str(reference), ref_text=candidate["transcript_hypothesis"],
                                      gen_text=model_text, nfe_step=steps, cfg_strength=2.0,
                                      speed=1.0, seed=42)
        if device == "cuda":
            torch.cuda.synchronize()
        ended = time.perf_counter()
        elapsed = ended - started
        audio = np.asarray(wav, dtype=np.float32).reshape(-1)
        if not audio.size or not np.isfinite(audio).all():
            raise RuntimeError("Empty/non-finite generated audio: " + name)
        peak = float(np.max(np.abs(audio)))
        if peak <= 0 or peak >= 1:
            raise RuntimeError("Silent or clipping generated audio: " + name)
        path = output / (name + ".wav")
        preview = output / ("preview_f5_russian_v2_" + ("gpu_" if device == "cuda" else "") + name + ".wav")
        sf.write(str(path), audio, rate, subtype="PCM_16")
        sf.write(str(preview), audio * (0.8 / peak), rate, subtype="PCM_16")
        sample = {"file": str(path), "listening_preview": str(preview), "text": text,
                  "model_text_with_stress": model_text, "sample_rate": rate,
                  "audio_seconds": len(audio) / rate, "generation_seconds": elapsed,
                  "cold_generation": index == 0,
                  "real_time_factor": elapsed / (len(audio) / rate), "raw_peak": peak,
                  "preview_linear_gain": 0.8 / peak}
        if pending_request is not None:
            request = pending_request.result(timeout=50)
            sample["concurrent_llm"] = request
            sample["llm_request_overlap_seconds"] = max(
                0.0, min(ended, request["ended_monotonic"]) - max(started, request["started_monotonic"]))
        report["samples"].append(sample)
        if device == "cuda":
            report["cuda_peak_allocated_bytes"] = torch.cuda.max_memory_allocated()
            report["cuda_peak_reserved_bytes"] = torch.cuda.max_memory_reserved()
            free, total = torch.cuda.mem_get_info()
            report["cuda_free_after_sample_bytes"] = free
            report["cuda_total_bytes"] = total
        (output / "results.json").write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
        print(json.dumps(sample, ensure_ascii=False), flush=True)
    validated_reference(AUDIT)
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--download", action="store_true")
    parser.add_argument("--generate", action="store_true")
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--device", choices=("cpu", "cuda"), default="cpu")
    parser.add_argument("--repeats", type=int, choices=(1, 2, 3), default=1)
    parser.add_argument("--threads", type=int, choices=range(1, 13), default=6)
    parser.add_argument("--steps", type=int, choices=(16, 24, 32), default=32)
    args = parser.parse_args(argv)
    output = evaluation_output(args.device, args.output_dir)
    print(json.dumps({"model": MODEL_ID, "revision": MODEL_REVISION, "engine_version": ENGINE_VERSION,
                      "selected_download_bytes": sum(FILES.values()) + sum(VOCODER_FILES.values()),
                      "missing_model_files": missing_files(MODEL_DIR, FILES),
                      "missing_vocoder_files": missing_files(VOCODER_DIR, VOCODER_FILES),
                      "device": args.device, "output_dir": str(output),
                      "game_modified": False}, indent=2), flush=True)
    if args.download:
        download()
    if args.generate:
        generate(output, args.threads, args.steps, args.device, args.repeats)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("F5 Russian audition failed: " + str(error), file=sys.stderr)
        sys.exit(1)
