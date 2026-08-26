# Student program migration

Student program reads and manual starts now use the SpeedReading API over the
existing `StudentProgramProgresses` and `ExerciseProgramTemplates` tables.

## Canonical endpoints

- `GET /api/speed-reading/student-program/my-program`
- `GET /api/speed-reading/student-program/my-programs`
- `POST /api/speed-reading/student-program/start`

The old `/api/v1/student-program` path remains available through Caddy. Starting
a program now closes every active program for the user before creating the new
one, while preserving the previous active program's streak values.
