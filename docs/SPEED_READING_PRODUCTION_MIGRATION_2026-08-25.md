# Speed Reading production data migration — 2026-08-25

## Scope

The legacy `speedreading_db` PostgreSQL container was preserved as the source
and copied into the Eduİvme PostgreSQL server as a dedicated database named
`speedreading_db`. The old database was not modified or removed.

Backup created before the copy:

```text
/root/backups/speedreading-migration-20260825T190500Z/SpeedReadingDB.dump
sha256: ca55079b67f06a01bd24d05d0ef1e7cbdd730cac2c8afc659f6ae74bffd85619
```

The production SpeedReading connection must use the internal Compose host:

```text
Host=postgres;Port=5432;Database=speedreading_db;Username=<deployment-user>;Password=<deployment-secret>
```

## Verification

The following table counts were compared exactly between the legacy source and
the new database after `pg_restore`:

| Table | Rows |
| --- | ---: |
| Exercises | 435 |
| ExerciseTypes | 21 |
| ReadingTexts | 300 |
| ReadingQuestions | 2,840 |
| ExerciseSessions | 132 |
| StudentExerciseResults | 123 |
| ReadingSessions | 0 |
| ExerciseProgramTemplates | 23 |
| StudentProgramProgresses | 5 |
| DailyExerciseLogs | 77 |
| PersonalizedLearningPaths | 606 |
| Achievements | 54 |
| UserAchievements | 5 |
| UserGameifications | 7 |
| Users | 10 |

All listed counts matched the source database exactly. The restored database
was created separately; no existing Eduİvme Identity, Coaching or Notification
database was overwritten.

## Identity caveat

The legacy `Users` identifiers are not the same as the current Identity user
identifiers. The legacy records remain intact so no progress/content rows are
lost. Existing accounts must be mapped by a controlled email/activation flow
before historical user-scoped analytics are presented to a new Identity user;
password hashes are not copied into Identity automatically.

## Rollback

The old `speedreading_db` container and volume remain available. If the new
service fails, stop only the new SpeedReading edge/service and restore the
previous `masterhizliokuma.com` vhost/backend. The dump above can recreate the
new database if a restore is required.
