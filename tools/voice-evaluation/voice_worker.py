"""Owned stdio speech worker. Uses existing F5 files offline; no installer/server."""

import base64
import contextlib
import io
import json
import os
from pathlib import Path
import re
import sys
import time
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
VOICE_ROOT = ROOT / "local/voice"
MAX_TEXT = 600


def configure_environment():
    paths = {
        "UV_CACHE_DIR": "cache/uv", "PIP_CACHE_DIR": "cache/pip",
        "HF_HOME": "cache/huggingface", "HF_HUB_CACHE": "cache/huggingface/hub",
        "HF_ASSETS_CACHE": "cache/huggingface/assets", "HF_DATASETS_CACHE": "cache/huggingface/datasets",
        "TORCH_HOME": "cache/torch", "CUDA_CACHE_PATH": "cache/cuda",
        "NUMBA_CACHE_DIR": "cache/numba", "XDG_CACHE_HOME": "cache",
        "MPLCONFIGDIR": "cache/matplotlib", "CACHED_PATH_CACHE_ROOT": "cache/cached-path",
        "WANDB_DIR": "cache/wandb", "WANDB_CACHE_DIR": "cache/wandb",
        "TEMP": "tmp", "TMP": "tmp", "PYTHONPYCACHEPREFIX": "cache/python-bytecode",
    }
    for key, relative in paths.items():
        path = VOICE_ROOT / relative
        path.mkdir(parents=True, exist_ok=True)
        os.environ[key] = str(path)
    os.environ.update(HF_HUB_OFFLINE="1", TRANSFORMERS_OFFLINE="1", WANDB_MODE="disabled",
                      HF_HUB_DISABLE_XET="1", PYTHONNOUSERSITE="1")


def validate_request(request):
    if not isinstance(request, dict):
        raise ValueError("invalid_request")
    identifier = request.get("id", "")
    text = request.get("text", "")
    if not isinstance(identifier, str) or re.fullmatch(r"[a-f0-9]{32}", identifier) is None:
        raise ValueError("invalid_id")
    if not isinstance(text, str) or not text.strip() or len(text) > MAX_TEXT:
        raise ValueError("invalid_text")
    if not re.search(r"[\u0400-\u04ff]", text):
        raise ValueError("russian_required")
    return identifier, text.strip()


class F5Engine:
    def __init__(self):
        import importlib.metadata
        import test_russian_f5 as audition
        if audition.missing_files(audition.MODEL_DIR, audition.FILES) or audition.missing_files(
                audition.VOCODER_DIR, audition.VOCODER_FILES):
            raise RuntimeError("missing_local_model")
        if importlib.metadata.version("f5-tts") != audition.ENGINE_VERSION:
            raise RuntimeError("engine_version_mismatch")
        self.reference, self.candidate = audition.validated_reference(audition.AUDIT)
        import numpy as np
        import soundfile as sf
        import torch
        from f5_tts.api import F5TTS
        import f5_tts.infer.utils_infer as utils
        if not torch.cuda.is_available():
            raise RuntimeError("cuda_unavailable")
        torch.set_num_threads(6)
        self.np, self.sf, self.torch, self.utils = np, sf, torch, utils
        self.model = F5TTS(
            model="F5TTS_v1_Base",
            ckpt_file=str(audition.MODEL_DIR / next(iter(audition.FILES))),
            vocab_file=str(audition.MODEL_DIR / "F5TTS_v1_Base/vocab.txt"),
            vocoder_local_path=str(audition.VOCODER_DIR), device="cuda")
        if next(self.model.ema_model.parameters()).device.type != "cuda" or next(
                self.model.vocoder.parameters()).device.type != "cuda":
            raise RuntimeError("wrong_device")

    def synthesize(self, text, *, steps=32):
        if type(steps) is not int or steps not in (16, 24, 32):
            raise ValueError("invalid_steps")
        with patch.object(self.utils, "transcribe", side_effect=RuntimeError("asr_disabled")), \
                self.torch.inference_mode():
            audio, rate, _ = self.model.infer(
                ref_file=str(self.reference), ref_text=self.candidate["transcript_hypothesis"],
                gen_text=text, nfe_step=steps, cfg_strength=2.0, speed=1.0, seed=42)
        self.torch.cuda.synchronize()
        audio = self.np.asarray(audio, dtype=self.np.float32).reshape(-1)
        if rate != 24000 or not 0 < len(audio) <= rate * 60 or not self.np.isfinite(audio).all():
            raise RuntimeError("invalid_audio")
        peak = float(self.np.max(self.np.abs(audio)))
        if not 0 < peak < 1:
            raise RuntimeError("silent_or_clipped_audio")
        # Same constant gain used for the approved audition, without changing pitch/time.
        stream = io.BytesIO()
        self.sf.write(stream, audio * (0.8 / peak), rate, format="WAV", subtype="PCM_16")
        return stream.getvalue()


def error_code(error):
    """Worker-owned codes such as cuda_unavailable are safe to report; other engine text never is."""
    message = str(error)
    return message if re.fullmatch(r"[a-z][a-z0-9_]{0,63}", message) else type(error).__name__


def emit(output, payload):
    output.write(json.dumps(payload, ensure_ascii=True, separators=(",", ":")) + "\n")
    output.flush()


def serve(source, output, engine):
    emit(output, {"type": "ready", "engine": "f5-russian-v2", "protocol": 1})
    while True:
        line = source.readline(16385)
        if not line:
            return
        # Oversized framing ends the owned process rather than accepting fragments.
        if len(line) > 16384 or not line.endswith("\n"):
            raise ValueError("invalid_frame")
        identifier = ""
        try:
            request = json.loads(line)
            if isinstance(request, dict) and isinstance(request.get("id"), str) and re.fullmatch(r"[a-f0-9]{32}", request["id"]):
                identifier = request["id"]
            identifier, text = validate_request(request)
            started = time.perf_counter()
            with contextlib.redirect_stdout(sys.stderr):
                wav = engine.synthesize(text)
            emit(output, {"type": "audio", "id": identifier, "ok": True,
                          "wav": base64.b64encode(wav).decode("ascii"),
                          "generationMs": round((time.perf_counter() - started) * 1000)})
        except Exception as error:
            emit(output, {"type": "audio", "id": identifier, "ok": False,
                          "error": type(error).__name__, "code": error_code(error)})


def main():
    configure_environment()
    output = sys.stdout
    try:
        with contextlib.redirect_stdout(sys.stderr):
            engine = F5Engine()
            engine.synthesize("Подожд+и, я чт+о-то сл+ышала. Ты т+оже?")
        serve(sys.stdin, output, engine)
        return 0
    except Exception as error:
        emit(output, {"type": "fatal", "error": type(error).__name__, "code": error_code(error)})
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
