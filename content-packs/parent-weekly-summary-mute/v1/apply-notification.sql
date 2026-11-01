-- V6a (parent panel, 01.11.2026): turn the ParentSummary e-mail channel off for the parents check.sql reported, so that
-- parents who had turned the legacy Identity weekly-summary switch off do not start receiving the weekly e-mail.
-- Only the e-mail channel of the ParentSummary category changes; the in-app choice and the SMS/WhatsApp opt-ins are kept
-- (a parent without a row gets in-app on, the category default). Other categories are not touched. Re-running is harmless.
--
-- Run on the Notification database, in one transaction, with the output of check.sql (<count>|<ids>):
--   psql -X -qAt -v ON_ERROR_STOP=1 -v expected_count=<count> -v parent_ids='<ids>' -f apply-notification.sql
-- Rolls back (nothing changes) if the id list does not parse, does not hold exactly expected_count distinct ids, or the
-- after-check does not find the e-mail channel off for every one of them.
\set ON_ERROR_STOP on
BEGIN;
CREATE TEMP TABLE v6a_parent_summary_mute ON COMMIT DROP AS
SELECT DISTINCT btrim(id)::uuid AS user_id
FROM unnest(string_to_array(:'parent_ids', ',')) AS id
WHERE btrim(id) <> '';
SELECT set_config('v6a.expected_count', :'expected_count', true) AS v6a_expected_count \gset

DO $$
DECLARE expected integer := current_setting('v6a.expected_count')::integer; found integer;
BEGIN
    SELECT count(*) INTO found FROM v6a_parent_summary_mute;
    IF expected < 1 OR found <> expected THEN
        RAISE EXCEPTION 'parent_ids holds % distinct ids, expected %: nothing changed', found, expected;
    END IF;
END $$;

SELECT 'parents: ' || count(*)
    || ' | e-mail on before: ' || count(*) FILTER (WHERE p."Email")
    || ' | already off: ' || count(*) FILTER (WHERE p."Email" = false)
    || ' | no row before: ' || count(*) FILTER (WHERE p."UserId" IS NULL)
FROM v6a_parent_summary_mute AS m
LEFT JOIN "NotificationPreferences" AS p ON p."UserId" = m.user_id AND p."Category" = 'ParentSummary';

INSERT INTO "NotificationPreferences" ("UserId", "Category", "InApp", "Email", "Sms", "WhatsApp", "UpdatedAt")
SELECT user_id, 'ParentSummary', true, false, false, false, now() FROM v6a_parent_summary_mute
ON CONFLICT ("UserId", "Category") DO UPDATE SET "Email" = false, "UpdatedAt" = now()
WHERE "NotificationPreferences"."Email";

DO $$
DECLARE expected integer := current_setting('v6a.expected_count')::integer; muted integer;
BEGIN
    SELECT count(*) INTO muted FROM v6a_parent_summary_mute AS m
    JOIN "NotificationPreferences" AS p ON p."UserId" = m.user_id AND p."Category" = 'ParentSummary' AND p."Email" = false;
    IF muted <> expected THEN
        RAISE EXCEPTION 'after the change % of % parents have the ParentSummary e-mail off: rolled back', muted, expected;
    END IF;
END $$;

SELECT 'e-mail off after: ' || count(*)
FROM v6a_parent_summary_mute AS m
JOIN "NotificationPreferences" AS p ON p."UserId" = m.user_id AND p."Category" = 'ParentSummary' AND p."Email" = false;
COMMIT;
