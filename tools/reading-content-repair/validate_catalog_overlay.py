"""Validate the complete reading-question repair overlay against a catalog."""

from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path
from typing import Any


ANSWER_KEYS = "ABCD"


def load_catalog(path: Path) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            text = json.loads(line)
            for question in text.get("questions", []):
                result[question["questionId"]] = {
                    "readingTextId": text["textId"],
                    **question,
                }
    return result


def load_overlay(pack_dir: Path, normalized_path: Path) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for path in sorted(pack_dir.glob("*.json")):
        pack = json.loads(path.read_text(encoding="utf-8"))
        for question in pack.get("questions", []):
            question_id = question["questionId"]
            if question_id in result:
                raise ValueError(f"duplicate overlay questionId: {question_id}")
            result[question_id] = {"readingTextId": pack["readingTextId"], **question, "source": path.name}
    for line in normalized_path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            question = json.loads(line)
            question_id = question["questionId"]
            if question_id in result:
                raise ValueError(f"duplicate overlay questionId: {question_id}")
            result[question_id] = question
    return result


def unique_longest(question: dict[str, Any]) -> bool:
    answer = (question.get("correctAnswer") or "").strip().upper()
    lengths = [len((question.get(f"option{key}") or "").strip()) for key in ANSWER_KEYS]
    correct_length = lengths[ANSWER_KEYS.index(answer)]
    return correct_length == max(lengths) and lengths.count(correct_length) == 1


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("catalog", type=Path)
    parser.add_argument("pack_dir", type=Path)
    parser.add_argument("normalized_jsonl", type=Path)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()

    catalog = load_catalog(args.catalog)
    overlay = load_overlay(args.pack_dir, args.normalized_jsonl)
    before = sum(unique_longest(question) for question in catalog.values())
    after = 0
    answer_distribution = Counter()
    for question_id, patch in overlay.items():
        if question_id not in catalog:
            raise ValueError(f"overlay question is not in catalog: {question_id}")
        if patch.get("readingTextId") != catalog[question_id]["readingTextId"]:
            raise ValueError(f"overlay text mismatch: {question_id}")
        if patch.get("correctAnswer") != catalog[question_id].get("correctAnswer"):
            raise ValueError(f"overlay answer key changed: {question_id}")
        if any(not (patch.get(f"option{key}") or "").strip() for key in ANSWER_KEYS):
            raise ValueError(f"empty overlay option: {question_id}")
        if not (patch.get("explanation") or "").strip():
            raise ValueError(f"empty overlay explanation: {question_id}")
        merged = {**catalog[question_id], **patch}
        after += unique_longest(merged)
        answer_distribution.update([merged["correctAnswer"]])

    report = {
        "catalogQuestionCount": len(catalog),
        "overlayQuestionCount": len(overlay),
        "manualPackQuestionCount": sum(
            len(json.loads(path.read_text(encoding="utf-8")).get("questions", []))
            for path in args.pack_dir.glob("*.json")
        ),
        "uniqueLongestBefore": before,
        "uniqueLongestAfterOverlay": after,
        "overlayAnswerDistribution": dict(answer_distribution),
        "coveragePercent": round(len(overlay) * 100 / len(catalog), 2),
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if after:
        raise SystemExit("overlay leaves uniquely-longest correct options")


if __name__ == "__main__":
    main()
