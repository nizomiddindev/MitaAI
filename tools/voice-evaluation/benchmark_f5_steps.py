"""Offline, bounded step comparison; no game configuration or worker protocol changes."""

import argparse
import hashlib
import io
import json
from pathlib import Path
import statistics
import subprocess
import sys
import time

import voice_worker as worker
from audit_reference import validate_output

STEPS = (32, 24, 16)
CASES = (
    ("greeting", "Привет."),
    ("short", "Да, я здесь."),
    ("curious", "Подожд+и, я чт+о-то сл+ышала. Ты т+оже?"),
)
LONG_CASES = (
    ("long_casual", "Я как раз хотела немного отдохнуть. Можем посидеть здесь пару минут, а потом вместе решить, чем заняться дальше."),
    ("long_question", "Ты тоже слышал этот звук за дверью? Мне показалось, что кто-то прошёл по коридору, но сейчас там совсем тихо."),
    ("long_expressive", "О, ты всё-таки вернулся! Я уже успела закончить свои дела. Расскажи, что интересного ты заметил, пока гулял по дому?"),
)


def schedule(cases=CASES):
    # Rotate settings to reduce simple run-order bias. Two repeats remain a small sample.
    return [(repeat + 1, name, text, steps)
            for repeat in range(2)
            for index, (name, text) in enumerate(cases)
            for steps in STEPS[(index + repeat) % 3:] + STEPS[:(index + repeat) % 3]]


def gpu_snapshot():
    try:
        return subprocess.check_output(
            ["nvidia-smi", "--query-gpu=name,memory.used,memory.total,utilization.gpu",
             "--format=csv,noheader"], text=True, timeout=10).strip()
    except (OSError, subprocess.SubprocessError):
        return "unavailable"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--case-set", choices=("short", "long"), default="short")
    args = parser.parse_args()
    worker.configure_environment()
    output = validate_output(args.output_dir)
    output.mkdir(parents=True, exist_ok=False)
    cases = LONG_CASES if args.case_set == "long" else CASES
    report = {"settings": {"steps": STEPS, "seed": 42, "speed": 1.0, "cfg": 2.0,
                           "repeats": 2, "engine": "f5-russian-v2", "device": "cuda",
                           "case_set": args.case_set},
              "game_modified": False, "listening_approved": False, "samples": [],
              "gpu_before": gpu_snapshot(),
              "limitations": "Voice-only warm timings; not full-turn, active LLM or FPS measurements."}

    def save():
        (output / "results.json").write_text(
            json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")

    save()
    started = time.perf_counter()
    engine = worker.F5Engine()
    report["load_seconds_including_imports"] = time.perf_counter() - started
    report["reference_sha256"] = engine.candidate["reference_sha256"]
    report["gpu_after_load"] = gpu_snapshot()
    print("Model loaded; warming each step setting.", flush=True)
    for steps in STEPS:
        engine.synthesize(CASES[-1][1], steps=steps)
    engine.torch.cuda.reset_peak_memory_stats()
    for repeat, name, text, steps in schedule(cases):
        engine.torch.cuda.synchronize()
        started = time.perf_counter()
        wav = engine.synthesize(text, steps=steps)
        elapsed = time.perf_counter() - started
        audio, rate = engine.sf.read(io.BytesIO(wav), dtype="float32")
        if (rate != 24000 or audio.ndim != 1 or not len(audio)
                or not engine.np.isfinite(audio).all()
                or not 0 < float(engine.np.max(engine.np.abs(audio))) < 1):
            raise RuntimeError("invalid_comparison_audio")
        path = output / f"preview_f5_ru_gpu_steps{steps}_{name}_r{repeat}.wav"
        path.write_bytes(wav)
        item = {"case": name, "text": text, "steps": steps, "repeat": repeat,
                "file": str(path), "generation_seconds": elapsed,
                "audio_seconds": len(audio) / rate, "sha256": hashlib.sha256(wav).hexdigest()}
        report["samples"].append(item)
        save()
        print(json.dumps(item, ensure_ascii=True), flush=True)
    report["summary"] = {
        str(steps): {name: statistics.median(item["generation_seconds"] for item in report["samples"]
                                            if item["steps"] == steps and item["case"] == name)
                    for name, _ in cases} for steps in STEPS}
    report["repeat_hashes_match"] = all(
        len({item["sha256"] for item in report["samples"]
             if item["steps"] == steps and item["case"] == name}) == 1
        for steps in STEPS for name, _ in cases)
    report["cuda_peak_allocated_bytes"] = engine.torch.cuda.max_memory_allocated()
    report["cuda_peak_reserved_bytes"] = engine.torch.cuda.max_memory_reserved()
    report["gpu_after"] = gpu_snapshot()
    save()
    print(json.dumps(report["summary"]), flush=True)


if __name__ == "__main__":
    main()
