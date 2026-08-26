# Vocabulary migration

Vocabulary items and spaced-repetition progress now use the SpeedReading API
over the existing `VocabularyItems` and `UserVocabularyProgresses` tables.

## Canonical endpoints

- `GET /api/speed-reading/vocabulary`
- `GET /api/speed-reading/vocabulary/{id}`
- `GET /api/speed-reading/vocabulary/categories`
- `POST|PUT|DELETE /api/speed-reading/vocabulary`
- `GET|POST /api/speed-reading/vocabulary/user`
- `PUT /api/speed-reading/vocabulary/user/{id}`
- `GET /api/speed-reading/vocabulary/user/due`
- `POST /api/speed-reading/vocabulary/import`
- `GET /api/speed-reading/vocabulary/export`
- `GET /api/speed-reading/vocabulary/download-template`

The legacy `/api/v1/vocabulary` path remains available through Caddy. Imports
are intentionally CSV-only, matching the old controller's actual validation.
The progress response keeps the old frontend shape; `status` represents the
Leitner box and the legacy schema does not contain a separate incorrect-attempt
counter, so that field is returned as zero.
