# Parent weekly-summary mute v1 (V6a, 01.11.2026)

Identity used to keep its own weekly-summary switch for parents: `identity.parent_profiles."NotificationPreferences"`
(jsonb), where `{"weeklyProgress": false}` muted the weekly parent e-mail. The user decided on 01.11.2026 that the only switch
is the notification-service category **ParentSummary** (`api/notifications/preferences`), which the weekly-summary consumer
already applies. The Identity migration `20261104090000_RemoveLegacyParentFields` drops the column.

A parent who had muted the summary in Identity, and has no ParentSummary choice in notification-service, would otherwise
start receiving the e-mail after the release. This pack prevents that.

| File | Database | Writes |
| --- | --- | --- |
| `check.sql` | Identity (`$POSTGRES_DB_IDENTITY`) | no (`BEGIN READ ONLY`) |
| `apply-notification.sql` | Notification (`$POSTGRES_DB_NOTIFICATION`) | yes, one guarded transaction |

## Before the release

1. Take the usual database snapshot (docs/DB_BACKUP_RUNBOOK.md).
2. Count the muted parents on Identity:

   ```sh
   docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$POSTGRES_DB_IDENTITY" < check.sql
   ```

   The output is one line, `<count>|<parent user ids>`. It works before the migration (reads the column) and after it (reads
   `identity.legacy_parent_notification_preferences_20261101`, which the migration fills before it drops the column). It
   fails, rather than printing `0`, when neither exists.
3. If the count is `0`, there is nothing to do. If it is greater than `0`, turn the ParentSummary e-mail off for exactly
   those parents on the Notification database:

   ```sh
   docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$POSTGRES_DB_NOTIFICATION" \
     -v expected_count=<count> -v parent_ids='<parent user ids>' < apply-notification.sql
   ```

   It prints the state before (`e-mail on before`, `already off`, `no row before`) and `e-mail off after: <count>`. It rolls
   back without changing anything if the id list does not parse, does not hold exactly `expected_count` distinct ids, or the
   after-check fails. Only the ParentSummary e-mail channel changes; the in-app choice and the SMS/WhatsApp opt-ins stay
   (a parent without a row gets the category default, in-app on). Running it again changes nothing.

The parents can turn the e-mail back on themselves under "Bildirim tercihleri" in the parent panel.

## Rollback

The migration's Down re-adds the column with its old default and restores every kept value from the backup table. The
notification preferences written by `apply-notification.sql` are ordinary user choices and stay as they are.

Test: `PARENT_SUMMARY_MUTE_TEST_DATABASE_URL=postgres://… node --test tests/E2E/parent-weekly-summary-mute.postgres.test.mjs`
