"""Create a reversible, length-balanced repair pack for reading questions.

The source catalog is kept intact.  Only questions where the correct answer is
the uniquely longest option are emitted.  One distractor receives a short,
contextual clarification so that answer length cannot act as a reliable key.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


ANSWER_KEYS = "ABCD"
SHORT_SUFFIXES = (
    "(metin bağlamında)",
    "(sorudaki kavram bağlamında)",
    "(metindeki ilgili bölümle birlikte)",
    "(soru kökündeki bilgiyle birlikte)",
)
MEDIUM_SUFFIXES = (
    "Metin bu görüşü olayın temel nedeni olarak sunar.",
    "Metindeki örnekler bu yorumu sonucu belirleyen ana etken olarak gösterir.",
    "Bu açıklama, soruda anlatılan durumun başlıca nedeni kabul edilir.",
    "Metin, bu görüşün sonuçları doğrudan açıkladığını belirtir.",
)
LONG_SUFFIXES = (
    "Metin bu görüşü olayın temel nedeni ve sorunun ana açıklaması olarak sunar.",
    "Metindeki örnekler bu yorumu sonuçları belirleyen başlıca etken olarak destekler.",
    "Bu açıklama, soruda anlatılan durumun nedenini ve sonucunu birlikte açıklar.",
)
XL_SUFFIXES = (
    "Metin, bu görüşü anlatılan olayın nedenlerini ve sonuçlarını açıklayan temel çerçeve olarak sunar; ilgili örnekler de bu yorumu destekler.",
    "Metin bu seçeneği, sorudaki durumun nedenlerini ve sonuçlarını birlikte açıklayan ana görüş olarak ele alır; bölümdeki örnekler bu yorumu güçlendirir.",
)


def load_catalog(path: Path) -> list[dict[str, Any]]:
    return [
        json.loads(line)
        for line in path.read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]


def load_excluded_question_ids(directory: Path | None) -> set[str]:
    if directory is None:
        return set()
    excluded: set[str] = set()
    for path in directory.glob("*.json"):
        pack = json.loads(path.read_text(encoding="utf-8"))
        excluded.update(question["questionId"] for question in pack.get("questions", []))
    return excluded


def suffix_text(base: str, suffix: str) -> str:
    base = " ".join((base or "").strip().split())
    if suffix.startswith("("):
        return f"{base.rstrip(' .;:')} {suffix}"
    return f"{base.rstrip(' .;:')}. {suffix}"


def choose_suffixes(gap: int) -> list[str]:
    # Keep the appended text close to the required gap where possible.  The
    # Longer explanatory clauses are used only for large gaps, so short answers
    # do not acquire a repetitive paragraph.
    options = list(SHORT_SUFFIXES) + list(MEDIUM_SUFFIXES)
    if gap > 110:
        # A single complete sentence reads better than several repeated
        # parenthetical labels on very short distractors.
        candidates = [suffix for suffix in XL_SUFFIXES if len(suffix) + 1 >= gap]
        if candidates:
            return [min(candidates, key=lambda suffix: len(suffix) - gap)]
    if gap > 65:
        # Prefer one complete explanatory sentence for medium-sized gaps;
        # parenthetical labels are reserved for small adjustments.
        candidates = list(LONG_SUFFIXES) + list(XL_SUFFIXES)
        feasible = [suffix for suffix in candidates if len(suffix) + 1 >= gap]
        if feasible:
            return [min(feasible, key=lambda suffix: len(suffix) - gap)]
        return [max(candidates, key=len)]
    if gap > 70:
        options.extend(LONG_SUFFIXES)
    if gap > 125:
        options.append(
            "Metin bu görüşü, anlatılan olayın nedenlerini ve sonuçlarını açıklayan "
            "temel çerçeve olarak sunar."
        )
    # A bounded dynamic search finds one or two clauses with the smallest
    # overshoot.  Three clauses are allowed only for very long legacy options.
    candidates: list[tuple[int, list[str]]] = [(0, [])]
    for _ in range(3):
        expanded = list(candidates)
        for length, selected in candidates:
            for option in options:
                if option in selected:
                    continue
                expanded.append((length + len(option) + 1, selected + [option]))
        candidates = sorted(expanded, key=lambda item: item[0])[:200]
    feasible = [item for item in candidates if item[0] >= max(1, gap)]
    if not feasible:
        return [options[-1]]
    return min(feasible, key=lambda item: (item[0] - gap, len(item[1])))[1]


def normalize_question(question: dict[str, Any]) -> dict[str, Any] | None:
    answer = (question.get("correctAnswer") or "").strip().upper()
    if answer not in ANSWER_KEYS:
        return None
    options = [" ".join((question.get(f"option{key}") or "").strip().split()) for key in ANSWER_KEYS]
    lengths = [len(option) for option in options]
    answer_index = ANSWER_KEYS.index(answer)
    if lengths[answer_index] != max(lengths) or lengths.count(lengths[answer_index]) != 1:
        return None

    # Deterministically vary which distractor receives the clarification;
    # this avoids creating a new fixed position cue.
    distractors = [index for index in range(4) if index != answer_index]
    digest = hashlib.sha256(question["questionId"].encode("utf-8")).digest()
    distractors.sort(key=lambda index: digest[index])
    target_index = max(distractors, key=lambda index: lengths[index])
    gap = lengths[answer_index] - lengths[target_index]
    for suffix in choose_suffixes(gap):
        options[target_index] = suffix_text(options[target_index], suffix)
        if len(options[target_index]) >= lengths[answer_index]:
            break
    suffix_index = 0
    while len(options[target_index]) < lengths[answer_index]:
        # Sentence punctuation adds a few characters that are not part of the
        # suffix search estimate; close the final edge case without truncating
        # or changing the original distractor meaning.
        options[target_index] = suffix_text(
            options[target_index],
            SHORT_SUFFIXES[suffix_index % len(SHORT_SUFFIXES)],
        )
        suffix_index += 1

    # If a suffix overshot by a lot, the answer is no longer the unique longest;
    # that is acceptable and is recorded in the audit report.
    return {
        "questionId": question["questionId"],
        "optionA": options[0],
        "optionB": options[1],
        "optionC": options[2],
        "optionD": options[3],
        "correctAnswer": answer,
        "explanation": (question.get("explanation") or "").strip()
        or "Yanıt, metindeki ilgili bilgi ve çıkarımlarla birlikte değerlendirilir.",
        "changeReason": "correct-option-unique-longest",
        "targetOption": ANSWER_KEYS[target_index],
        "sourceLengths": lengths,
        "repairedLengths": [len(option) for option in options],
        "originalOptionA": question.get("optionA", ""),
        "originalOptionB": question.get("optionB", ""),
        "originalOptionC": question.get("optionC", ""),
        "originalOptionD": question.get("optionD", ""),
        "originalExplanation": question.get("explanation") or "",
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("catalog", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--exclude-pack-dir", type=Path)
    args = parser.parse_args()

    rows = load_catalog(args.catalog)
    excluded_question_ids = load_excluded_question_ids(args.exclude_pack_dir)
    records: list[dict[str, Any]] = []
    by_text: dict[str, int] = {}
    for row in rows:
        for question in row.get("questions", []):
            if question.get("questionId") in excluded_question_ids:
                continue
            record = normalize_question(question)
            if record is None:
                continue
            record = {
                "readingTextId": row["textId"],
                "title": row.get("title", ""),
                **record,
            }
            records.append(record)
            by_text[row["textId"]] = by_text.get(row["textId"], 0) + 1

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="\n") as handle:
        for record in records:
            handle.write(json.dumps(record, ensure_ascii=False) + "\n")

    unique_longest_after = 0
    for record in records:
        lengths = [len(record[f"option{key}"].strip()) for key in ANSWER_KEYS]
        answer_length = lengths[ANSWER_KEYS.index(record["correctAnswer"])]
        unique_longest_after += int(answer_length == max(lengths) and lengths.count(answer_length) == 1)
    report = {
        "catalogTextCount": len(rows),
        "repairedQuestionCount": len(records),
        "excludedReviewedQuestionCount": len(excluded_question_ids),
        "repairedTextCount": len(by_text),
        "uniqueLongestBefore": len(records),
        "uniqueLongestAfter": unique_longest_after,
        "texts": [
            {"readingTextId": text_id, "questionCount": count}
            for text_id, count in sorted(by_text.items())
        ],
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
