# Question-bank migration

Exam questions now use the SpeedReading API over the existing
`ExamQuestions` table.

## Canonical endpoints

- `GET /api/speed-reading/exam-questions`
- `GET /api/speed-reading/exam-questions/{id}`
- `POST|PUT|DELETE /api/speed-reading/exam-questions`
- `DELETE /api/speed-reading/exam-questions/{id}/hard`

The old `/api/v1/exam-questions` path remains available through Caddy. The
new query applies all filters emitted by the admin UI (`examType`,
`difficulty`, `category`, `searchTerm`, and `ageGroupId`). Frontend exam and
category enum values now match the persisted legacy values.
