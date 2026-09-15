"""Render the rollback SQL carried by catalog-length-balanced.jsonl."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def literal(value: str | None) -> str:
    if value is None or value == "":
        return "NULL"
    return "'" + value.replace("'", "''") + "'"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("normalized_jsonl", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    rows = [
        json.loads(line)
        for line in args.normalized_jsonl.read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]
    values = []
    for row in rows:
        values.append("(" + ", ".join([
            literal(row["readingTextId"]) + "::uuid",
            literal(row["questionId"]) + "::uuid",
            literal(row.get("originalOptionA")),
            literal(row.get("originalOptionB")),
            literal(row.get("originalOptionC")),
            literal(row.get("originalOptionD")),
            literal(row["correctAnswer"].strip().upper()),
            literal(row.get("originalExplanation")),
        ]) + ")")

    lines = [
        "-- Rollback for catalog-question-repair.sql; reviewed packs have a separate database snapshot.",
        "BEGIN;",
        "CREATE TEMP TABLE reading_question_content_rollback (reading_text_id uuid NOT NULL, id uuid PRIMARY KEY, option_a text NOT NULL, option_b text NOT NULL, option_c text NOT NULL, option_d text NOT NULL, correct_answer text NOT NULL, explanation text NULL);",
        "INSERT INTO reading_question_content_rollback (reading_text_id, id, option_a, option_b, option_c, option_d, correct_answer, explanation) VALUES",
        ",\n".join(values) + ";",
        "DO $rollback$",
        "DECLARE",
        "    relation text;",
        "BEGIN",
        "    relation := to_regclass('public.\"ReadingQuestions\"')::text;",
        "    IF relation IS NOT NULL THEN",
        "        EXECUTE 'UPDATE public.\"ReadingQuestions\" AS q SET \"OptionA\" = p.option_a, \"OptionB\" = p.option_b, \"OptionC\" = p.option_c, \"OptionD\" = p.option_d, \"CorrectAnswer\" = p.correct_answer, \"Explanation\" = p.explanation, \"UpdatedAt\" = CURRENT_TIMESTAMP, \"UpdatedBy\" = ' || quote_literal('content-repair-rollback-20260915') || ' FROM reading_question_content_rollback AS p WHERE q.\"Id\" = p.id AND q.\"ReadingTextId\" = p.reading_text_id AND q.\"IsDeleted\" = FALSE';",
        "    END IF;",
        "    relation := to_regclass('speed_reading.reading_questions')::text;",
        "    IF relation IS NOT NULL THEN",
        "        EXECUTE 'UPDATE speed_reading.reading_questions AS q SET option_a = p.option_a, option_b = p.option_b, option_c = p.option_c, option_d = p.option_d, correct_answer = p.correct_answer, explanation = p.explanation, updated_at = CURRENT_TIMESTAMP, updated_by = ' || quote_literal('content-repair-rollback-20260915') || ' FROM reading_question_content_rollback AS p WHERE q.id = p.id AND q.reading_text_id = p.reading_text_id AND q.is_deleted = FALSE';",
        "    END IF;",
        "    IF relation IS NULL AND to_regclass('public.\"ReadingQuestions\"') IS NULL THEN",
        "        RAISE EXCEPTION 'Reading question table was not found in public or speed_reading schema';",
        "    END IF;",
        "END $rollback$;",
        "COMMIT;",
    ]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"questionCount": len(rows)}))


if __name__ == "__main__":
    main()
