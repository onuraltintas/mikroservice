"""Render reviewed JSON packs and the catalog normalization JSONL as one SQL patch."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Iterable


def literal(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def load_manual_packs(directory: Path) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for path in sorted(directory.glob("*.json")):
        pack = json.loads(path.read_text(encoding="utf-8"))
        for question in pack.get("questions", []):
            result.append({
                "readingTextId": pack["readingTextId"],
                **question,
                "source": path.name,
            })
    return result


def load_normalized_jsonl(path: Path) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            row = json.loads(line)
            row["source"] = path.name
            result.append(row)
    return result


def load_questions(pack_dir: Path, normalized_path: Path) -> list[dict[str, Any]]:
    # Reviewed packs take precedence.  The normalizer excludes their ids, but
    # failing loudly here protects against an accidental duplicate release.
    reviewed = load_manual_packs(pack_dir)
    normalized = load_normalized_jsonl(normalized_path)
    questions = reviewed + normalized
    seen: set[str] = set()
    for question in questions:
        question_id = question["questionId"]
        if question_id in seen:
            raise ValueError(f"duplicate questionId across repair sources: {question_id}")
        seen.add(question_id)
        for key in "ABCD":
            if not (question.get(f"option{key}") or "").strip():
                raise ValueError(f"empty option {key}: {question_id}")
        if (question.get("correctAnswer") or "").strip().upper() not in "ABCD":
            raise ValueError(f"invalid answer key: {question_id}")
        if not (question.get("explanation") or "").strip():
            raise ValueError(f"missing explanation: {question_id}")
    return questions


def render(questions: Iterable[dict[str, Any]]) -> str:
    questions = list(questions)
    values: list[str] = []
    for question in questions:
        values.append(
            "(" + ", ".join([
                literal(question["readingTextId"]) + "::uuid",
                literal(question["questionId"]) + "::uuid",
                literal(question["optionA"]),
                literal(question["optionB"]),
                literal(question["optionC"]),
                literal(question["optionD"]),
                literal(question["correctAnswer"].strip().upper()),
                literal(question["explanation"]),
            ]) + ")"
        )
    lines = [
        "-- Generated from reviewed packs plus catalog-length-balanced.jsonl.",
        "-- Idempotent: updates only matching active question ids.",
        "BEGIN;",
        "CREATE TEMP TABLE reading_question_content_repair (reading_text_id uuid NOT NULL, id uuid PRIMARY KEY, option_a text NOT NULL, option_b text NOT NULL, option_c text NOT NULL, option_d text NOT NULL, correct_answer text NOT NULL, explanation text NOT NULL);",
        "INSERT INTO reading_question_content_repair (reading_text_id, id, option_a, option_b, option_c, option_d, correct_answer, explanation) VALUES",
        ",\n".join(values) + ";",
        "DO $quality$",
        "DECLARE",
        "    relation text;",
        "BEGIN",
        "    relation := to_regclass('public.\"ReadingQuestions\"')::text;",
        "    IF relation IS NOT NULL THEN",
        "        EXECUTE 'UPDATE public.\"ReadingQuestions\" AS q SET \"OptionA\" = p.option_a, \"OptionB\" = p.option_b, \"OptionC\" = p.option_c, \"OptionD\" = p.option_d, \"CorrectAnswer\" = p.correct_answer, \"Explanation\" = p.explanation, \"UpdatedAt\" = CURRENT_TIMESTAMP, \"UpdatedBy\" = ' || quote_literal('content-repair-20260915') || ' FROM reading_question_content_repair AS p WHERE q.\"Id\" = p.id AND q.\"ReadingTextId\" = p.reading_text_id AND q.\"IsDeleted\" = FALSE';",
        "    END IF;",
        "    relation := to_regclass('speed_reading.reading_questions')::text;",
        "    IF relation IS NOT NULL THEN",
        "        EXECUTE 'UPDATE speed_reading.reading_questions AS q SET option_a = p.option_a, option_b = p.option_b, option_c = p.option_c, option_d = p.option_d, correct_answer = p.correct_answer, explanation = p.explanation, updated_at = CURRENT_TIMESTAMP, updated_by = ' || quote_literal('content-repair-20260915') || ' FROM reading_question_content_repair AS p WHERE q.id = p.id AND q.reading_text_id = p.reading_text_id AND q.is_deleted = FALSE';",
        "    END IF;",
        "    IF relation IS NULL AND to_regclass('public.\"ReadingQuestions\"') IS NULL THEN",
        "        RAISE EXCEPTION 'Reading question table was not found in public or speed_reading schema';",
        "    END IF;",
        "END $quality$;",
        "COMMIT;",
    ]
    return "\n".join(lines) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("pack_dir", type=Path)
    parser.add_argument("normalized_jsonl", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    questions = load_questions(args.pack_dir, args.normalized_jsonl)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(render(questions), encoding="utf-8")
    print(json.dumps({
        "reviewedPackCount": len(list(args.pack_dir.glob("*.json"))),
        "questionCount": len(questions),
    }))


if __name__ == "__main__":
    main()
