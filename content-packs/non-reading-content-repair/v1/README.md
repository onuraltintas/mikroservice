# Non-reading content repair v1

This source-controlled, reversible pack resolves the remaining non-reading content audit findings detected on 15 September 2026.

- 48 options across 45 external question-bank questions were editorially revised so a visibly long correct answer cannot act as a reliable shortcut.
- 36 short vocabulary definitions were expanded into age-appropriate complete explanations.
- 137 redundant vocabulary records across 114 same-word/category/age groups are soft-deleted; the selected canonical record is retained. There was no user vocabulary progress attached to these records when this pack was prepared.

`apply.sql` checks each original value before changing it, writes a maintenance audit record, and can be safely re-run. Take a production database snapshot before applying. `rollback.sql` only reverts rows that still have the repair value and repair audit marker.
