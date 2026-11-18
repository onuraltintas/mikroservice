# Reading question repair v2 (18.11.2026) — applied to production

v1 (15.09.2026) kept the correct option from being the unique longest by appending template phrases to a wrong option ("(metin bağlamında)", "Metin bu görüşü olayın temel nedeni olarak sunar.") and filled empty explanations with template sentences. v2 replaces that with hand-written content, checked per question.

- `questions.jsonl` — 3,102 questions. Each line holds the question `id` and the fields that changed (`option_a`…`option_d`, `explanation`; for 27 broken questions also `question_text` and `correct_answer`). `note` explains special cases.
- `texts.jsonl` — 314 reading texts, full new `content`: leftover Markdown removed (`---`, `**`, `#`) and 173 reviewed spelling fixes. `word_count` is recomputed by the SQL with the domain rule.
- `RULES.md` — the writing rules (Turkish) used for every question.
- `apply.sql.gz` / `rollback.sql.gz` — the exact SQL run on 18.11.2026 15:18 (`speedreading_owned_db`). sha256 of the unpacked files: apply `3d2775baa4decba915a9e54aa09875a5542ba867167b589caea2df4b6bb0d8b6`, rollback `8b3f3c939fb8caa5f8ea1fbaa488c6878898062d63db4bdea35ea7282c42cbc3`. Each row changes only if it still holds the exported value. Result: 3102 questions, 314 texts, 0 skipped.

Checks (`tools/reading-content-repair/validate_rewrite_pack.py <questions export> questions.jsonl --allow-key-change`): no template phrase, correct option never the unique longest, four distinct options, explanation 1–2000 characters (all ≤ 300), no option letters.

Tools: `clean_markdown_texts.py` (Markdown cleanup with a word-count guard), `build_rewrite_sql.py` (apply/rollback SQL from the export and the pack). The live export (`reading-*-20261118.jsonl`) is not kept in the repository.
