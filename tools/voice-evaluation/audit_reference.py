"""Audit installed voice clips and prepare one continuous reference, without downloads."""

import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
VOICE_ROOT = ROOT / "local/voice"
SOURCE_ROOT = ROOT / "MiSide_Dev/Data/LanguagesVoice/Russian-Dream Cast"
LOCALIZATION_ROOT = ROOT / "MiSide_Dev/Data/Languages/Russian"
OUTPUT = VOICE_ROOT / "reference-audit"
CANDIDATE_FOLDER = "LocationDialogue Location3"
CANDIDATE_NUMBER = 20


def localized_line(lines, number):
    """Return a physical-line hypothesis, not a verified audio transcript."""
    if number < 1 or number > len(lines):
        return {"text": "", "speaker_hint": None}
    speaker = None
    for line in lines[:number]:
        body, separator, comment = line.partition("//")
        if separator:
            label = comment.strip()
            # Only recognize explicit labels, not character names inside dialogue.
            if label == "Mita" or label.endswith(" - Mita"):
                speaker = "Mita"
            elif label == "Player" or label.endswith(" - Player"):
                speaker = "Player"
            else:
                speaker = None
    return {"text": lines[number - 1].partition("//")[0].strip(),
            "speaker_hint": speaker}


def validate_output(path):
    resolved = path.resolve()
    if not resolved.is_relative_to(VOICE_ROOT.resolve()):
        raise ValueError("Reference outputs must remain under D:/MitaAI/local/voice.")
    return resolved


def audit(output, prepare):
    import numpy as np
    import soundfile as sf

    output = validate_output(output)
    rows = []
    for source in SOURCE_ROOT.rglob("*.ogg"):
        info = sf.info(str(source))
        rows.append({"path": str(source), "duration_seconds": info.duration,
                     "sample_rate": info.samplerate, "channels": info.channels})
    if not rows:
        raise RuntimeError("No installed Russian voice recordings found.")
    rows.sort(key=lambda row: row["duration_seconds"], reverse=True)
    source = SOURCE_ROOT / CANDIDATE_FOLDER / f"{CANDIDATE_NUMBER}.ogg"
    localization = LOCALIZATION_ROOT / (CANDIDATE_FOLDER + ".txt")
    lines = localization.read_text(encoding="utf-8-sig").splitlines()
    hypothesis = localized_line(lines, CANDIDATE_NUMBER)
    if hypothesis["speaker_hint"] != "Mita" or not hypothesis["text"]:
        raise RuntimeError("Candidate no longer matches the expected Mita section.")
    source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    audio, rate = sf.read(str(source), dtype="float32", always_2d=True)
    mono = audio.mean(axis=1)
    if not mono.size or not np.isfinite(mono).all():
        raise RuntimeError("Candidate contains empty/non-finite audio.")
    peak = float(np.max(np.abs(mono)))
    if peak <= 0:
        raise RuntimeError("Candidate is silent.")
    candidate = {"source": str(source), "source_sha256": source_hash,
                 "localization": str(localization), "physical_line": CANDIDATE_NUMBER,
                 "localization_sha256": hashlib.sha256(localization.read_bytes()).hexdigest(),
                 "transcript_hypothesis": hypothesis["text"],
                 "speaker_hint": hypothesis["speaker_hint"],
                 "mapping_evidence": "Opening OGG numbering follows physical localized lines, including gaps for comments/blanks; runtime binding and spoken transcript not verified.",
                 "duration_seconds": len(mono) / rate, "sample_rate": rate,
                 "peak": peak, "rms": float(np.sqrt(np.mean(mono ** 2))),
                 "speaker_listening_confirmed": False,
                 "spoken_transcript_confirmed": False, "background_audio_checked": False}
    report = {"recordings_scanned": len(rows),
              "longest_seconds": rows[0]["duration_seconds"],
              "clips_at_least_5_seconds": sum(row["duration_seconds"] >= 5 for row in rows),
              "longest_clips": rows[:20], "candidate": candidate,
              "originals_modified": False, "game_integration": False,
              "new_models_downloaded": False}
    output.mkdir(parents=True, exist_ok=True)
    if prepare:
        reference = output / "mita_ru_continuous.wav"
        preview = output / "preview_mita_ru_continuous.wav"
        sf.write(str(reference), mono, rate, subtype="PCM_16")
        gain = 0.8 / peak
        sf.write(str(preview), mono * gain, rate, subtype="PCM_16")
        candidate.update({"reference": str(reference), "listening_preview": str(preview),
                          "preview_linear_gain": gain,
                          "transform": "One full continuous clip; mono conversion, PCM16 export. Preview uses constant gain only; no splicing, denoising, pitch or speed changes.",
                          "reference_sha256": hashlib.sha256(reference.read_bytes()).hexdigest()})
        if hashlib.sha256(source.read_bytes()).hexdigest() != source_hash:
            raise RuntimeError("Original source changed during preparation.")
    (output / "audit.json").write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({"recordings_scanned": len(rows), "longest_seconds": report["longest_seconds"],
                      "clips_at_least_5_seconds": report["clips_at_least_5_seconds"],
                      "candidate": candidate}, indent=2, ensure_ascii=False), flush=True)
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prepare", action="store_true", help="Export a continuous candidate and volume-balanced preview")
    parser.add_argument("--output-dir", type=Path, default=OUTPUT)
    parser.add_argument("--confirm-listening", action="store_true", help="Record the human's speaker/background-audio confirmation")
    parser.add_argument("--confirm-transcript", action="store_true", help="Record the human's exact spoken-transcript confirmation")
    args = parser.parse_args(argv)
    if args.confirm_listening or args.confirm_transcript:
        if args.prepare:
            parser.error("Prepare and listen first; confirmation must be a separate operation.")
        path = validate_output(args.output_dir) / "audit.json"
        report = json.loads(path.read_text(encoding="utf-8"))
        candidate = report["candidate"]
        source = Path(candidate["source"])
        reference = Path(candidate["reference"])
        for audio_path, key in ((source, "source_sha256"), (reference, "reference_sha256")):
            if hashlib.sha256(audio_path.read_bytes()).hexdigest() != candidate[key]:
                raise RuntimeError("Audio changed since the reference audition; confirm it again.")
        if args.confirm_listening:
            candidate["speaker_listening_confirmed"] = True
            candidate["background_audio_checked"] = True
        if args.confirm_transcript:
            candidate["spoken_transcript_confirmed"] = True
        candidate["confirmation_evidence"] = "Explicit user listening responses in this project chat, 7 October 2026; not automated acoustic verification."
        path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
        print("Recorded human listening confirmation; source and reference hashes still match.")
    else:
        audit(args.output_dir, args.prepare)


if __name__ == "__main__":
    main()
