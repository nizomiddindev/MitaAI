"""Small offline checks; no speech dependencies or model files required."""

import contextlib
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import test_english as voice
import test_mita_voice as mita
import audit_reference as reference
import test_russian_f5 as f5
import benchmark_coexistence as coexistence


class PreflightChecks(unittest.TestCase):
    def test_selected_files_exclude_unused_decoder(self):
        self.assertNotIn("s3gen.safetensors", voice.FILES)
        self.assertEqual(sum(voice.FILES.values()), 1942099748)

    def test_pinned_revisions(self):
        for revision in (voice.MODEL_REVISION, voice.SOURCE_REVISION):
            self.assertRegex(revision, r"^[a-f0-9]{40}$")

    def test_missing_and_incomplete_file_detection(self):
        with tempfile.TemporaryDirectory() as folder:
            directory = Path(folder)
            self.assertEqual(set(voice.missing_files(directory)), set(voice.FILES))
            (directory / "conds.pt").write_bytes(b"incomplete")
            self.assertIn("conds.pt", voice.missing_files(directory))
            (directory / "conds.pt").write_bytes(b"\0" * voice.FILES["conds.pt"])
            self.assertNotIn("conds.pt", voice.missing_files(directory))

    def test_default_does_not_download_or_generate(self):
        with patch.object(voice, "download") as download, patch.object(voice, "generate") as generate:
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                self.assertEqual(voice.main([]), 0)
            self.assertFalse(json.loads(output.getvalue())["audio_generated"])
            download.assert_not_called()
            generate.assert_not_called()

    def test_generate_refuses_missing_weights(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaisesRegex(RuntimeError, "Model files not ready"):
                voice.generate(Path(folder), Path(folder) / "out", "cpu", 6)
            self.assertFalse((Path(folder) / "out").exists())

    def test_original_english_samples(self):
        self.assertEqual(len(voice.SAMPLES), 3)
        self.assertEqual(len({name for name, _ in voice.SAMPLES}), 3)
        self.assertTrue(all(text.strip() for _, text in voice.SAMPLES))


class MultilingualChecks(unittest.TestCase):
    def test_selective_download(self):
        self.assertEqual(sum(mita.FILES.values()), 3206924387)
        self.assertEqual(len(mita.FILES), 4)
        self.assertNotIn("t3_mtl23ls_v2.safetensors", mita.FILES)
        self.assertNotIn("s3gen_v3.pt", mita.FILES)

    def test_pairs_and_reference(self):
        self.assertEqual([row[1] for row in mita.SAMPLES], ["ru", "en", "ru", "en"])
        self.assertEqual(mita.SOURCE_NAMES, ("7.ogg", "8.ogg", "9.ogg"))
        self.assertIn("local", mita.REFERENCE.parts)
        self.assertRegex(mita.MODEL_REVISION, r"^[a-f0-9]{40}$")

    def test_default_is_readiness_only(self):
        with patch.object(mita, "download") as download, patch.object(mita, "generate") as generate, \
             patch.object(mita, "make_previews") as previews, \
             patch.object(mita, "prepare_reference") as prepare:
            with contextlib.redirect_stdout(io.StringIO()):
                mita.main([])
            download.assert_not_called()
            generate.assert_not_called()
            prepare.assert_not_called()
            previews.assert_not_called()

    def test_generation_refuses_incomplete_download(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaisesRegex(RuntimeError, "Model files not ready"):
                mita.generate(Path(folder), Path(folder) / "out", Path(folder) / "ref.wav", 6)
            self.assertFalse((Path(folder) / "out").exists())


class ReferenceChecks(unittest.TestCase):
    def test_physical_lines_keep_blanks_and_comments(self):
        lines = ["// Mita", "first", "", "// Answer 2 - Mita", "second"]
        self.assertEqual(reference.localized_line(lines, 5),
                         {"text": "second", "speaker_hint": "Mita"})
        self.assertEqual(reference.localized_line(lines, 3)["text"], "")

    def test_inline_speaker_and_unknown_sections(self):
        lines = ["// Mita", "question // Player", "answer // Mita", "// Unknown", "next"]
        self.assertEqual(reference.localized_line(lines, 2)["speaker_hint"], "Player")
        self.assertEqual(reference.localized_line(lines, 3)["speaker_hint"], "Mita")
        self.assertIsNone(reference.localized_line(lines, 5)["speaker_hint"])

    def test_invalid_line_is_not_a_transcript(self):
        for number in (-1, 0, 3):
            self.assertEqual(reference.localized_line(["// Mita", "hello"], number),
                             {"text": "", "speaker_hint": None})

    def test_output_stays_in_voice_lab(self):
        self.assertEqual(reference.validate_output(reference.OUTPUT), reference.OUTPUT.resolve())
        for path in (reference.ROOT / "MiSide_Dev", reference.VOICE_ROOT / "../outside"):
            with self.assertRaises(ValueError):
                reference.validate_output(path)


class CoexistenceChecks(unittest.TestCase):
    def test_only_loopback_without_credentials_is_allowed(self):
        self.assertEqual(coexistence.local_endpoint("http://127.0.0.1:1234/v1/chat/completions"),
                         "http://127.0.0.1:1234/v1/chat/completions")
        for url in ("https://example.com/v1/chat", "http://192.168.1.2:1234", "http://user:secret@localhost", 
                    "http://localhost?secret=key", "http://localhost/#fragment"):
            with self.assertRaises(ValueError):
                coexistence.local_endpoint(url)

    def test_successful_request_records_metadata_not_response_text(self):
        from types import SimpleNamespace
        import threading
        response = {"choices": [{"message": {"content": "fixture reply"}, "finish_reason": "stop"}],
                    "usage": {"completion_tokens": 3}}
        ready = threading.Event()
        with patch.object(coexistence, "build_opener", return_value=SimpleNamespace(
                open=lambda *args, **kwargs: io.StringIO(json.dumps(response)))):
            result = coexistence.request_llm("http://localhost:1234/v1/chat/completions", "fixture", ready)
        self.assertTrue(ready.is_set())
        self.assertTrue(result["success"])
        self.assertEqual(result["response_characters"], 13)
        self.assertNotIn("fixture reply", json.dumps(result))
        self.assertGreaterEqual(result["ended_monotonic"], result["started_monotonic"])

    def test_failed_request_is_not_reported_as_success(self):
        import threading
        with patch.object(coexistence, "build_opener", side_effect=OSError("fixture network error")):
            result = coexistence.request_llm("http://localhost:1234/v1/chat/completions", "fixture", threading.Event())
        self.assertFalse(result["success"])
        self.assertEqual(result["error_type"], "OSError")

    def test_game_pid_must_match_actual_game_executable(self):
        from types import SimpleNamespace
        with patch.object(coexistence.subprocess, "run", return_value=SimpleNamespace(
                stdout='"MiSideFull.exe","123","Console","1","0 K"')):
            self.assertTrue(coexistence.game_running(123))
            self.assertFalse(coexistence.game_running(456))
        with patch.object(coexistence.subprocess, "run", return_value=SimpleNamespace(
                stdout='"other.exe","123","Console","1","0 K"')):
            self.assertFalse(coexistence.game_running(123))

    def test_benchmark_model_must_remain_available(self):
        from types import SimpleNamespace
        with patch.object(coexistence, "build_opener", return_value=SimpleNamespace(
                open=lambda *args, **kwargs: io.StringIO(json.dumps({"data": [{"id": "fixture"}]})))):
            coexistence.require_model("http://localhost:1234/v1/chat/completions", "fixture")
            with self.assertRaisesRegex(RuntimeError, "unavailable"):
                coexistence.require_model("http://localhost:1234/v1/chat/completions", "missing")


class RussianF5Checks(unittest.TestCase):
    def test_gpu_and_cpu_default_outputs_are_separate(self):
        self.assertEqual(f5.evaluation_output("cpu", None), f5.OUTPUT.resolve())
        self.assertEqual(f5.evaluation_output("cuda", None), f5.GPU_OUTPUT.resolve())
        self.assertNotEqual(f5.GPU_OUTPUT, f5.OUTPUT)

    def test_gpu_output_cannot_escape_voice_lab(self):
        with self.assertRaises(ValueError):
            f5.evaluation_output("cuda", reference.ROOT / "MiSide_Dev")

    def test_gpu_readiness_does_not_generate_or_download(self):
        with patch.object(f5, "download") as download, patch.object(f5, "generate") as generate:
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                f5.main(["--device", "cuda"])
            self.assertEqual(json.loads(output.getvalue())["device"], "cuda")
            download.assert_not_called()
            generate.assert_not_called()

    def test_cuda_unavailable_is_not_silent_cpu_fallback(self):
        from types import SimpleNamespace
        fake_torch = SimpleNamespace(cuda=SimpleNamespace(is_available=lambda: False))
        with self.assertRaisesRegex(RuntimeError, "No CPU fallback"):
            f5.device_details(fake_torch, "cuda")
        self.assertEqual(f5.device_details(fake_torch, "cpu"), {"name": "CPU"})
        with self.assertRaises(ValueError):
            f5.device_details(fake_torch, "other")

    def test_repeat_names_are_unique_and_preserve_text(self):
        cases = f5.generation_cases(3)
        self.assertEqual(len(cases), 6)
        self.assertEqual(len({name for name, _, _ in cases}), 6)
        self.assertEqual(cases[:2], list(f5.SAMPLES))
        self.assertEqual(cases[3][1:], f5.SAMPLES[1][1:])
        for count in (0, 4):
            with self.assertRaises(ValueError):
                f5.generation_cases(count)

    def test_selective_download_and_revisions(self):
        self.assertEqual(sum(f5.FILES.values()) + sum(f5.VOCODER_FILES.values()), 1402816013)
        self.assertTrue(all(not name.endswith(".pt") for name in f5.FILES))
        for revision in (f5.MODEL_REVISION, f5.VOCODER_REVISION):
            self.assertRegex(revision, r"^[a-f0-9]{40}$")

    def test_default_cannot_download_or_synthesize(self):
        with patch.object(f5, "download") as download, patch.object(f5, "generate") as generate:
            with contextlib.redirect_stdout(io.StringIO()):
                f5.main([])
            download.assert_not_called()
            generate.assert_not_called()

    def test_confirmation_and_hash_gates(self):
        reference.VOICE_ROOT.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=reference.VOICE_ROOT) as folder:
            root = Path(folder)
            audio = root / "ref.wav"
            source = root / "source.ogg"
            audio.write_bytes(b"reference fixture")
            source.write_bytes(b"source fixture")
            candidate = {"reference": str(audio), "source": str(source),
                         "reference_sha256": hashlib.sha256(audio.read_bytes()).hexdigest(),
                         "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                         "speaker_listening_confirmed": True, "spoken_transcript_confirmed": True,
                         "background_audio_checked": True, "transcript_hypothesis": "confirmed text"}
            audit_path = root / "audit.json"

            def save():
                audit_path.write_text(json.dumps({"candidate": candidate}), encoding="utf-8")

            save()
            self.assertEqual(f5.validated_reference(audit_path)[0], audio.resolve())
            for key in ("speaker_listening_confirmed", "spoken_transcript_confirmed", "background_audio_checked"):
                candidate[key] = False
                save()
                with self.assertRaisesRegex(RuntimeError, "confirmation missing"):
                    f5.validated_reference(audit_path)
                candidate[key] = True
            candidate["transcript_hypothesis"] = " "
            save()
            with self.assertRaisesRegex(RuntimeError, "non-empty"):
                f5.validated_reference(audit_path)
            candidate["transcript_hypothesis"] = "confirmed text"
            save()
            audio.write_bytes(b"tampered")
            with self.assertRaisesRegex(RuntimeError, "Reference changed"):
                f5.validated_reference(audit_path)

    def test_stress_marks_do_not_change_original_sentence(self):
        for _, original, stressed in f5.SAMPLES:
            self.assertEqual(original, stressed.replace("+", ""))


if __name__ == "__main__":
    unittest.main()
