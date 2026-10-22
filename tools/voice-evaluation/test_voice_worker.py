"""Offline protocol checks. Never constructs a real speech engine."""

import base64
import io
import json
import inspect
import unittest
from unittest.mock import patch

import voice_worker as worker
import benchmark_f5_steps as comparison


class ProtocolChecks(unittest.TestCase):
    def test_long_comparison_uses_longer_bounded_distinct_texts(self):
        jobs = comparison.schedule(comparison.LONG_CASES)
        self.assertEqual(len(jobs), 18)
        self.assertEqual(len({name for name, _ in comparison.LONG_CASES}), 3)
        for name, text in comparison.LONG_CASES:
            self.assertTrue(15 <= len(text.split()) <= 30)
            self.assertLess(len(text), 240)
            for steps in comparison.STEPS:
                self.assertEqual(sum(job[1] == name and job[3] == steps for job in jobs), 2)

    def test_step_comparison_is_bounded_and_balanced(self):
        jobs = comparison.schedule()
        self.assertEqual(len(jobs), 18)
        for steps in comparison.STEPS:
            for name, _ in comparison.CASES:
                self.assertEqual(sum(job[1] == name and job[3] == steps for job in jobs), 2)

    def test_step_comparison_rotates_first_setting(self):
        jobs = comparison.schedule()
        self.assertEqual([jobs[index][3] for index in (0, 3, 6)], [32, 24, 16])
        self.assertEqual([jobs[index][3] for index in (9, 12, 15)], [24, 16, 32])

    def test_production_steps_remain_32(self):
        self.assertEqual(inspect.signature(worker.F5Engine.synthesize).parameters["steps"].default, 32)

    def test_invalid_step_values_fail_before_inference(self):
        engine = object.__new__(worker.F5Engine)
        for steps in (0, 8, 17, 64, True, "16"):
            with self.subTest(steps=steps), self.assertRaises(ValueError):
                engine.synthesize("Привет", steps=steps)

    def test_valid_russian_request(self):
        self.assertEqual(worker.validate_request({"id": "a" * 32, "text": " Привет "}),
                         ("a" * 32, "Привет"))

    def test_invalid_inputs(self):
        for item in (None, [], {}, {"id": "../bad", "text": "Привет"},
                     {"id": "a" * 32, "text": "hello"}, {"id": "a" * 32, "text": " "},
                     {"id": "a" * 32, "text": "я" * 601}):
            with self.subTest(item=item), self.assertRaises(ValueError):
                worker.validate_request(item)

    def test_two_requests_reuse_engine_and_echo_ids(self):
        engine = unittest.mock.Mock()
        engine.synthesize.return_value = b"wave fixture"
        source = io.StringIO("".join(json.dumps({"id": c * 32, "text": "Привет"}) + "\n" for c in "ab"))
        output = io.StringIO()
        worker.serve(source, output, engine)
        messages = [json.loads(line) for line in output.getvalue().splitlines()]
        self.assertEqual(messages[0]["type"], "ready")
        self.assertEqual([m["id"] for m in messages[1:]], ["a" * 32, "b" * 32])
        self.assertEqual(base64.b64decode(messages[1]["wav"]), b"wave fixture")
        self.assertEqual(engine.synthesize.call_count, 2)

    def test_error_is_not_speech_or_private_trace(self):
        engine = unittest.mock.Mock()
        engine.synthesize.side_effect = RuntimeError("private text")
        output = io.StringIO()
        worker.serve(io.StringIO(json.dumps({"id": "a" * 32, "text": "Привет"}) + "\n"), output, engine)
        result = json.loads(output.getvalue().splitlines()[1])
        self.assertFalse(result["ok"])
        self.assertNotIn("wav", result)
        self.assertNotIn("private", output.getvalue())

    def test_error_codes_keep_worker_identifiers_only(self):
        self.assertEqual(worker.error_code(RuntimeError("cuda_unavailable")), "cuda_unavailable")
        self.assertEqual(worker.error_code(ValueError("russian_required")), "russian_required")
        for error in (RuntimeError("private text"), RuntimeError("Привет"), RuntimeError("CUDA out of memory"), KeyError("text")):
            with self.subTest(error=repr(error)):
                self.assertEqual(worker.error_code(error), type(error).__name__)

    def test_startup_fatal_reports_code(self):
        output = io.StringIO()
        with patch.object(worker, "configure_environment"), \
                patch.object(worker, "F5Engine", side_effect=RuntimeError("cuda_unavailable")), \
                patch.object(worker.sys, "stdout", output):
            self.assertEqual(worker.main(), 1)
        fatal = json.loads(output.getvalue().splitlines()[-1])
        self.assertEqual((fatal["type"], fatal["error"], fatal["code"]), ("fatal", "RuntimeError", "cuda_unavailable"))

    def test_request_failure_reports_code_without_text(self):
        engine = unittest.mock.Mock()
        engine.synthesize.side_effect = RuntimeError("invalid_audio")
        output = io.StringIO()
        worker.serve(io.StringIO(json.dumps({"id": "a" * 32, "text": "Привет"}) + "\n"), output, engine)
        result = json.loads(output.getvalue().splitlines()[1])
        self.assertEqual((result["ok"], result["code"]), (False, "invalid_audio"))

    def test_oversized_frame_terminates(self):
        with self.assertRaises(ValueError):
            worker.serve(io.StringIO("a" * 17000 + "\n"), io.StringIO(), None)

    def test_partial_frame_terminates(self):
        with self.assertRaises(ValueError):
            worker.serve(io.StringIO("{}"), io.StringIO(), None)

    def test_bad_json_does_not_call_engine(self):
        engine = unittest.mock.Mock()
        output = io.StringIO()
        worker.serve(io.StringIO("not-json\n"), output, engine)
        engine.synthesize.assert_not_called()
        self.assertFalse(json.loads(output.getvalue().splitlines()[1])["ok"])

    def test_invalid_text_retains_valid_request_id(self):
        output = io.StringIO()
        engine = unittest.mock.Mock()
        worker.serve(io.StringIO(json.dumps({"id": "a" * 32, "text": "hello"}) + "\n"), output, engine)
        result = json.loads(output.getvalue().splitlines()[1])
        self.assertEqual(result["id"], "a" * 32)
        self.assertFalse(result["ok"])
        engine.synthesize.assert_not_called()


if __name__ == "__main__":
    unittest.main()
