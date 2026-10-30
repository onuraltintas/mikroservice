# Gamification audit v1 (read-only)

H19 (audit 30.10.2026): badges are defined in the admin panel, so which ones exist in production is only known there.
`audit.sql` lists every active badge that:

- can never be earned: invalid criteria, an unknown criteria type, a repeatable badge, a score above 100, a "first exercise"
  badge for a type whose completion is never verified (eye warm-ups, grouping, paced reading, N-back), or a type with no
  active exercise;
- only some age groups can earn. For example, vocabulary badges where YoungAdult/Adult have no vocabulary exercise.
  Students in those age groups no longer see such badges unless they already earned them.

The script only reads. Run it on the server:

```sh
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d speedreading_owned_db < audit.sql
```

Each row is `id|name|criteria|value|problem`. Fix or deactivate the reported badges in the admin panel. Badge edits are now
validated, so an invalid criterion can no longer be saved.

Test: `GAMIFICATION_AUDIT_TEST_DATABASE_URL=postgres://… node --test tests/E2E/gamification-audit.postgres.test.mjs`
