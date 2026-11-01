-- V6a (parent panel, 01.11.2026): read-only. Which parents had turned the legacy Identity weekly-summary switch off
-- (parent_profiles.NotificationPreferences containing {"weeklyProgress": false})? Identity no longer keeps that switch;
-- the notification-service category ParentSummary is the only one left, so these parents must get its e-mail channel off
-- (apply-notification.sql) before the release, or they would start receiving the weekly e-mail.
--
-- Works before the Identity migration 20261104090000_RemoveLegacyParentFields (reads the column) and after it (reads the
-- backup table the migration keeps). Fails when neither exists, so an error is never read as "0".
-- Run on the Identity database:  psql -X -qAt -v ON_ERROR_STOP=1 -f check.sql
-- Output: one line  <count>|<parent user ids, comma-separated, sorted>
BEGIN READ ONLY;
SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'identity' AND table_name = 'parent_profiles'
               AND column_name = 'NotificationPreferences') AS has_column,
       to_regclass('identity.legacy_parent_notification_preferences_20261101') IS NOT NULL AS has_backup
\gset
\if :has_column
SELECT count(*) || '|' || coalesce(string_agg("UserId"::text, ',' ORDER BY "UserId"), '')
FROM identity.parent_profiles WHERE "NotificationPreferences" @> '{"weeklyProgress": false}'::jsonb;
\elif :has_backup
SELECT count(*) || '|' || coalesce(string_agg(parent_user_id::text, ',' ORDER BY parent_user_id), '')
FROM identity.legacy_parent_notification_preferences_20261101 WHERE notification_preferences @> '{"weeklyProgress": false}'::jsonb;
\else
DO $$ BEGIN RAISE EXCEPTION 'Neither identity.parent_profiles."NotificationPreferences" nor its backup table exists: wrong database?'; END $$;
\endif
COMMIT;
