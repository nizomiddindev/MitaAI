"""Bounded local LLM/F5 overlap test; no game hooks, downloads or config changes."""

import argparse
from concurrent.futures import ThreadPoolExecutor
import csv
import io
import json
from pathlib import Path
import subprocess
import threading
import time
from urllib.parse import urlparse
from urllib.request import Request, ProxyHandler, build_opener

import test_russian_f5 as f5


def local_endpoint(value):
    parsed = urlparse(value)
    if parsed.scheme != "http" or parsed.hostname not in ("127.0.0.1", "localhost"):
        raise ValueError("This test only contacts a local HTTP AI server.")
    if parsed.username or parsed.password or parsed.query or parsed.fragment:
        raise ValueError("Endpoint must not contain credentials, query or fragment.")
    return value


def request_llm(endpoint, model, ready):
    started = time.perf_counter()
    result = {"started_monotonic": started, "success": False, "model": model}
    ready.set()
    body = {"model": model, "messages": [
        {"role": "system", "content": "Return only a numbered list."},
        {"role": "user", "content": "List thirty everyday household objects with a short neutral description each."}],
        "max_tokens": 128, "temperature": 0.4, "stream": False}
    try:
        request = Request(local_endpoint(endpoint), data=json.dumps(body).encode(),
                          headers={"Content-Type": "application/json"})
        with build_opener(ProxyHandler({})).open(request, timeout=40) as response:
            data = json.load(response)
        text = data["choices"][0]["message"]["content"]
        result.update(success=isinstance(text, str) and bool(text.strip()),
                      response_characters=len(text) if isinstance(text, str) else 0,
                      usage=data.get("usage"), finish_reason=data["choices"][0].get("finish_reason"))
    except Exception as error:
        result["error_type"] = type(error).__name__
    result["ended_monotonic"] = time.perf_counter()
    result["seconds"] = result["ended_monotonic"] - started
    return result


def require_model(endpoint, model):
    url = urlparse(local_endpoint(endpoint))._replace(path="/v1/models").geturl()
    with build_opener(ProxyHandler({})).open(url, timeout=5) as response:
        models = json.load(response)
    if model not in {item.get("id") for item in models.get("data", [])}:
        raise RuntimeError("The benchmark's loaded local model is unavailable.")


def game_running(pid):
    result = subprocess.run(["tasklist.exe", "/FI", f"PID eq {pid}", "/FO", "CSV", "/NH"],
                            capture_output=True, text=True, timeout=5,
                            creationflags=subprocess.CREATE_NO_WINDOW)
    return any(row and row[0].lower() == "misidefull.exe" and row[1] == str(pid)
               for row in csv.reader(io.StringIO(result.stdout)))


class GpuSampler:
    def __init__(self):
        self.stop = threading.Event()
        self.rows = []
        self.thread = threading.Thread(target=self.sample, daemon=True)

    def sample(self):
        while not self.stop.is_set():
            try:
                output = subprocess.run(["nvidia-smi", "--query-gpu=memory.used,utilization.gpu",
                                         "--format=csv,noheader,nounits"], capture_output=True,
                                        text=True, timeout=5, creationflags=subprocess.CREATE_NO_WINDOW)
                for row in csv.reader(io.StringIO(output.stdout)):
                    self.rows.append({"monotonic": time.perf_counter(), "memory_used_mib": int(row[0]),
                                      "utilization_percent": int(row[1])})
            except (OSError, ValueError, subprocess.TimeoutExpired):
                pass
            self.stop.wait(1)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--endpoint", type=local_endpoint, default="http://127.0.0.1:1234/v1/chat/completions")
    parser.add_argument("--model", required=True)
    parser.add_argument("--game-pid", type=int, required=True)
    parser.add_argument("--gameplay-confirmed", action="store_true", required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args(argv)
    output = f5.validate_output(args.output_dir)
    if not game_running(args.game_pid):
        raise RuntimeError("MiSide must be running for this test.")
    require_model(args.endpoint, args.model)
    sampler = GpuSampler()
    sampler.thread.start()
    try:
        with ThreadPoolExecutor(max_workers=1) as pool:
            def concurrent_request(index):
                if not game_running(args.game_pid):
                    raise RuntimeError("MiSide exited during the test.")
                require_model(args.endpoint, args.model)
                if index < len(f5.SAMPLES):
                    return None
                ready = threading.Event()
                future = pool.submit(request_llm, args.endpoint, args.model, ready)
                if not ready.wait(5):
                    raise RuntimeError("Local AI request failed to start.")
                return future

            report = f5.generate(output, 6, 32, "cuda", 2, concurrent_request)
    finally:
        sampler.stop.set()
        sampler.thread.join(6)
    for index, sample in enumerate(report["samples"]):
        sample["load_case"] = "llm-resident-idle" if index < len(f5.SAMPLES) else "llm-request-active"
    report["coexistence"] = {"game_pid": args.game_pid, "game_running_at_end": game_running(args.game_pid),
                             "gameplay_unpaused_user_confirmed": args.gameplay_confirmed,
                             "game_renderer_adapter": "User reports Radeon 760M; not independently measured",
                             "endpoint": args.endpoint, "llm_model": args.model,
                             "llm_context_tokens": 2048, "llm_max_output_tokens": 128,
                             "gpu_samples": sampler.rows, "game_fps_measured": False}
    path = output / "results.json"
    path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    active = [s for s in report["samples"] if "concurrent_llm" in s]
    if len(active) != 2 or not all(s["concurrent_llm"]["success"] and
                                  s["llm_request_overlap_seconds"] > 0 for s in active):
        raise RuntimeError("Two successful overlapping AI requests were not verified; inspect results.")
    if not report["coexistence"]["game_running_at_end"]:
        raise RuntimeError("Game exited; coexistence gate is not passed.")
    print(json.dumps({"results": str(path), "verified_overlapping_requests": len(active),
                      "sampled_gpu_peak_mib": max((r["memory_used_mib"] for r in sampler.rows), default=None)}, indent=2))


if __name__ == "__main__":
    main()
