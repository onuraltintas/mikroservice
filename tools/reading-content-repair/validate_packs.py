"""Validate versioned reading-question repair packs against a catalog snapshot."""

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
            row = json.loads(line)
            result[row["textId"]] = row
    return result


def validate_pack(path: Path, catalog: dict[str, dict[str, Any]]) -> dict[str, Any]:
    pack = json.loads(path.read_text(encoding="utf-8"))
    text_id = pack.get("readingTextId")
    if text_id not in catalog:
        raise ValueError(f"{path}: readingTextId is not in the active catalog")
    source_questions = {q["questionId"]: q for q in catalog[text_id]["questions"]}
    seen: set[str] = set()
    longest = 0
    for question in pack.get("questions", []):
        question_id = question.get("questionId")
        if not question_id or question_id in seen:
            raise ValueError(f"{path}: duplicate or missing questionId")
        seen.add(question_id)
        if question_id not in source_questions:
            raise ValueError(f"{path}: questionId {question_id} is not attached to the text")
        answer = (question.get("correctAnswer") or "").strip().upper()
        if answer not in ANSWER_KEYS:
            raise ValueError(f"{path}: invalid answer key for {question_id}")
        if any(not (question.get(f"option{key}") or "").strip() for key in ANSWER_KEYS):
            raise ValueError(f"{path}: empty option for {question_id}")
        if not (question.get("explanation") or "").strip():
            raise ValueError(f"{path}: missing explanation for {question_id}")
        lengths = [len(question[f"option{key}"].strip()) for key in ANSWER_KEYS]
        correct_length = lengths[ANSWER_KEYS.index(answer)]
        if correct_length == max(lengths) and lengths.count(correct_length) == 1:
            longest += 1
    if not seen:
        raise ValueError(f"{path}: pack has no questions")
    if longest * 100 >= len(seen) * 70:
        raise ValueError(f"{path}: correct option is uniquely longest in {longest}/{len(seen)} questions")
    return {
        "file": str(path),
        "title": pack.get("title", ""),
        "readingTextId": text_id,
        "questionCount": len(seen),
        "answerDistribution": dict(Counter(
            (question["correctAnswer"] or "").strip().upper()
            for question in pack["questions"]
        )),
        "uniqueLongestCorrectCount": longest,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("catalog", type=Path)
    parser.add_argument("packs", type=Path)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()

    catalog = load_catalog(args.catalog)
    results = [
        validate_pack(path, catalog)
        for path in sorted(args.packs.glob("*.json"))
    ]
    report = {
        "catalogTextCount": len(catalog),
        "packCount": len(results),
        "packedQuestionCount": sum(item["questionCount"] for item in results),
        "packs": results,
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
