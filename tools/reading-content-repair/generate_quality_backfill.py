"""Generate a reviewable quality backfill for the active reading catalog.

The source catalog is an explicitly exported, line-delimited JSON snapshot.  The
script only generates a patch; it never connects to or mutates a database.  The
patch can therefore be reviewed, backed up, and applied by a separate migration
step after its metrics have been checked.
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from pathlib import Path
from typing import Any


ANSWER_KEYS = "ABCD"
OPTION_FIELDS = tuple(f"option{key}" for key in ANSWER_KEYS)

# Words and short phrases that frequently make a generated correct option
# longer without adding information needed to answer the question.
FILLER_WORDS = {
    "aslında",
    "artık",
    "bile",
    "birçok",
    "büyük",
    "en",
    "genellikle",
    "gerçekten",
    "hatta",
    "ise",
    "özellikle",
    "sadece",
    "son derece",
    "tamamen",
    "çok",
    "yalnızca",
}

CONNECTOR_WORDS = {
    "ama",
    "ancak",
    "çünkü",
    "fakat",
    "ile",
    "ve",
    "veya",
    "ya",
    "ya da",
}


def clean(value: str | None) -> str:
    return re.sub(r"\s+", " ", (value or "").strip())


def word_count(value: str) -> int:
    return len(re.findall(r"\S+", value))


def split_words(value: str) -> list[str]:
    return clean(value).split(" ") if clean(value) else []


def remove_parenthetical(value: str) -> str:
    return clean(re.sub(r"\s*\([^)]*\)", "", value))


def remove_filler_words(value: str) -> str:
    result = clean(value)
    for phrase in sorted(FILLER_WORDS, key=len, reverse=True):
        result = re.sub(rf"(?<!\w){re.escape(phrase)}(?!\w)", "", result, flags=re.IGNORECASE)
    result = re.sub(r"\s+([,.;:!?])", r"\1", result)
    result = re.sub(r"([,.;:!?])\s+([,.;:!?])", r"\1", result)
    return clean(result).strip(" ,;:")


def clause_candidates(value: str) -> list[str]:
    value = remove_parenthetical(value)
    parts = [clean(part).strip(" ,;:") for part in re.split(r"[,;:]", value)]
    parts = [part for part in parts if word_count(part) >= 3]
    return [value, *parts]


def compact_option(value: str, maximum_characters: int) -> str:
    """Shorten an option while preserving a complete, readable phrase.

    This is intentionally conservative.  It removes parenthetical material,
    filler modifiers, and complete comma-separated adjuncts before falling back
    to a word-boundary prefix.  If no readable candidate fits, the original
    option is retained and the item remains in the review report.
    """

    original = clean(value)
    if len(original) <= maximum_characters:
        return original

    candidates: list[str] = []
    for candidate in clause_candidates(original):
        candidate = remove_filler_words(candidate)
        if candidate and candidate not in candidates:
            candidates.append(candidate)

    for candidate in sorted(candidates, key=len):
        if len(candidate) <= maximum_characters and word_count(candidate) >= 3:
            return candidate.rstrip(".,;:") + ("." if original.endswith(".") else "")

    # Keep a complete prefix and remove dangling conjunctions.  Options may be
    # fragments, so a final period is only added when the source had one.
    words = split_words(remove_filler_words(original))
    punctuation = "." if original.endswith(".") else ""
    for length in range(len(words), 2, -1):
        candidate = " ".join(words[:length]).rstrip(" ,;:")
        if candidate.split(" ")[-1].casefold() in CONNECTOR_WORDS:
            continue
        if len(candidate) <= maximum_characters:
            return candidate + punctuation

    return original


def correct_is_unique_longest(question: dict[str, Any]) -> bool:
    answer = clean(question.get("correctAnswer")).upper()
    if answer not in ANSWER_KEYS:
        return False
    lengths = [len(clean(question.get(f"option{key}"))) for key in ANSWER_KEYS]
    correct_length = lengths[ANSWER_KEYS.index(answer)]
    return correct_length == max(lengths) and lengths.count(correct_length) == 1


def explanation_for(question: dict[str, Any]) -> str:
    answer = clean(question.get("correctAnswer")).upper()
    option = clean(question.get(f"option{answer}"))
    question_type = int(question.get("type") or 1)
    if question_type == 3:
        return f"Metnin ana fikri ve olayların sonucu “{option}” yorumunu destekler."
    if question_type == 2:
        return f"Metindeki bilgiler birlikte değerlendirildiğinde doğru çıkarım “{option}” olur."
    return f"Metin, “{option}” bilgisini doğrudan verir."


def load_rows(path: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        if not line.strip():
            continue
        try:
            row = json.loads(line)
        except json.JSONDecodeError as exc:
            raise ValueError(f"Invalid JSON on line {line_number}: {exc}") from exc
        if not row.get("textId") or not row.get("questions"):
            raise ValueError(f"Catalog row {line_number} has no textId or questions")
        rows.append(row)
    return rows


def generate(
    rows: list[dict[str, Any]],
    compact_options: bool = False,
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    updates: list[dict[str, Any]] = []
    before_flagged = 0
    after_flagged = 0
    explanations_added = 0
    options_compacted = 0
    unrepairable_length_flags = 0

    for row in rows:
        for question in row["questions"]:
            answer = clean(question.get("correctAnswer")).upper()
            if answer not in ANSWER_KEYS:
                continue
            is_unique_longest = correct_is_unique_longest(question)
            if is_unique_longest:
                before_flagged += 1
            update: dict[str, Any] = {
                "readingTextId": row["textId"],
                "title": row.get("title", ""),
                "questionId": question["questionId"],
            }
            if not clean(question.get("explanation")):
                update["explanation"] = explanation_for(question)
                explanations_added += 1

            if compact_options and is_unique_longest:
                other_lengths = [
                    len(clean(question.get(f"option{key}")))
                    for key in ANSWER_KEYS
                    if key != answer
                ]
                maximum = max(other_lengths)
                compacted = compact_option(question[f"option{answer}"], maximum)
                if compacted != clean(question[f"option{answer}"]):
                    update[f"option{answer}"] = compacted
                    options_compacted += 1
                    candidate = dict(question)
                    candidate[f"option{answer}"] = compacted
                    if correct_is_unique_longest(candidate):
                        unrepairable_length_flags += 1
                    else:
                        after_flagged += 0
                else:
                    unrepairable_length_flags += 1
            if len(update) > 3:
                updates.append(update)

    # Recalculate the flag count from the generated values so the report is
    # meaningful even if a future strategy changes more than one option.
    updates_by_id = {item["questionId"]: item for item in updates}
    for row in rows:
        for question in row["questions"]:
            candidate = dict(question)
            update = updates_by_id.get(question["questionId"])
            if update:
                candidate.update(update)
            if correct_is_unique_longest(candidate):
                after_flagged += 1

    report = {
        "sourceTextCount": len(rows),
        "sourceQuestionCount": sum(len(row["questions"]) for row in rows),
        "updates": len(updates),
        "explanationsAdded": explanations_added,
        "optionsCompacted": options_compacted,
        "uniqueLongestBefore": before_flagged,
        "uniqueLongestAfter": after_flagged,
        "unrepairableLengthFlags": unrepairable_length_flags,
        "answerDistribution": dict(
            Counter(
                clean(question.get("correctAnswer")).upper()
                for row in rows
                for question in row["questions"]
                if clean(question.get("correctAnswer")).upper() in ANSWER_KEYS
            )
        ),
    }
    return updates, report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument(
        "--compact-options",
        action="store_true",
        help="Use the conservative option compactor; review every generated option before publishing.",
    )
    args = parser.parse_args()

    updates, report = generate(load_rows(args.source), compact_options=args.compact_options)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        "".join(json.dumps(item, ensure_ascii=False, separators=(",", ":")) + "\n" for item in updates),
        encoding="utf-8",
    )
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
