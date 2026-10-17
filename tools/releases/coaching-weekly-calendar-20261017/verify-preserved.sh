set -euo pipefail
umask 077
source /var/lib/eduivme/releases/coaching-weekly-calendar-20261017/deploy.sh validate
fingerprint coaching_db > coaching.browser.after.txt
accounts > accounts.browser.after.txt
cmp coaching.live.before.txt coaching.browser.after.txt
cmp accounts.live.before.txt accounts.browser.after.txt
verifyDb coaching_db > browser-checks.json
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
DO $$ BEGIN
  IF (SELECT count(*) FROM coaching.study_weekly_drafts) <> 1 THEN RAISE EXCEPTION 'Expected one test student draft'; END IF;
  IF (SELECT jsonb_array_length("SlotsJson") FROM coaching.study_weekly_drafts) <> 2 THEN RAISE EXCEPTION 'Expected two half-hour cards'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.study_weekly_drafts d CROSS JOIN LATERAL jsonb_array_elements(d."SlotsJson") s WHERE (s->>'Day')::int<>2 OR (s->>'DurationMinutes')::int<>30 OR (s->>'StartMinute')::int NOT IN (1080,1110)) THEN RAISE EXCEPTION 'Saved cards differ from visible final schedule'; END IF;
END $$;
SELECT 'All existing Coaching business rows and core accounts unchanged; one new weekly draft with two half-hour cards persisted.';
SQL
date -u +%FT%TZ > browser-data-verified.txt
cat browser-checks.json
