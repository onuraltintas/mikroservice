"""Validate a reading-question rewrite pack (v2) against the live catalog export.

Inputs:
  questions export : reading-questions-YYYYMMDD.jsonl (row_to_json of speed_reading.reading_questions)
  rewrite pack     : JSONL, one object per changed question:
                     {"id", "option_a".."option_d", "explanation", "note"?}
                     Fields that are absent keep the live value. The answer key is never part of the pack.

Checks (any failure -> exit 1):
  - every pack id exists in the export and appears once
  - no option or explanation contains a leftover template phrase from the v1 repair
  - the correct option is not the unique longest option
  - the four options are distinct (case and punctuation insensitive)
  - option <= 500 chars, explanation 1..2000 chars
  - the explanation is not just the correct option repeated in a template
  - no stray markdown rule ("---") or doubled spaces
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

KEYS = "abcd"

TEMPLATE_PHRASES = (
    "(metin bağlamında)",
    "(sorudaki kavram bağlamında)",
    "(metindeki ilgili bölümle birlikte)",
    "(soru kökündeki bilgiyle birlikte)",
    "metin bu görüşü",
    "metindeki örnekler bu yorumu",
    "bu açıklama, soruda anlatılan durumun",
    "metin, bu görüşün sonuçları",
    "metin bu seçeneği",
    "yanıt, metindeki ilgili bilgi ve çıkarımlarla birlikte değerlendirilir",
    "bilgisini doğrudan verir",
    "doğru çıkarım “",
)


def normalise(text: str) -> str:
    return re.sub(r"[\W_]+", " ", text.casefold()).strip()


def load_export(path: Path) -> dict[str, dict]:
    rows = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            row = json.loads(line)
            rows[str(row["id"])] = row
    return rows


def check(question: dict) -> list[str]:
    problems = []
    options = [(question.get(f"option_{k}") or "").strip() for k in KEYS]
    explanation = (question.get("explanation") or "").strip()
    answer = (question.get("correct_answer") or "").strip().upper()
    if answer not in "ABCD" or len(answer) != 1:
        return [f"unexpected correct_answer {answer!r}"]
    for key, option in zip(KEYS, options):
        if not option:
            problems.append(f"option {key} empty")
        if len(option) > 500:
            problems.append(f"option {key} longer than 500")
    lowered = [o.casefold() for o in options] + [explanation.casefold()]
    for phrase in TEMPLATE_PHRASES:
        if any(phrase in text for text in lowered):
            problems.append(f"template phrase: {phrase}")
    lengths = [len(o) for o in options]
    correct = "ABCD".index(answer)
    if lengths[correct] == max(lengths) and lengths.count(max(lengths)) == 1:
        problems.append("correct option is the unique longest")
    if len({normalise(o) for o in options}) < 4:
        problems.append("options not distinct")
    if not explanation:
        problems.append("explanation empty")
    elif len(explanation) > 2000:
        problems.append("explanation longer than 2000")
    elif normalise(explanation) == normalise(options[correct]):
        problems.append("explanation only repeats the correct option")
    for text in options + [explanation]:
        if "---" in text or "  " in text:
            problems.append("stray '---' or double space")
            break
    return problems


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("questions_export", type=Path)
    parser.add_argument("rewrite_pack", type=Path)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--allow-key-change", action="store_true", help="accept reviewed question rewrites that change question_text or correct_answer")
    args = parser.parse_args()

    live = load_export(args.questions_export)
    seen: set[str] = set()
    failures: dict[str, list[str]] = {}
    for number, line in enumerate(args.rewrite_pack.read_text(encoding="utf-8").splitlines(), 1):
        if not line.strip():
            continue
        change = json.loads(line)
        qid = str(change.get("id"))
        if qid not in live:
            failures[f"line {number}"] = [f"unknown id {qid}"]
            continue
        if qid in seen:
            failures[qid] = ["duplicate id in pack"]
            continue
        seen.add(qid)
        if "correct_answer" in change and not args.allow_key_change:
            failures[qid] = ["pack must not change the answer key"]
            continue
        merged = {**live[qid], **{k: v for k, v in change.items() if k != "note"}}
        problems = check(merged)
        if problems:
            failures[qid] = problems

    report = {"questions_in_export": len(live), "changed": len(seen), "failed": len(failures), "failures": failures}
    if args.report:
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "failures"}, ensure_ascii=False))
    for qid, problems in list(failures.items())[:20]:
        print(qid, "; ".join(problems))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
