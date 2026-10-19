set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-targets-20261019
cd "$release"
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | LC_ALL=C sort > protected.final.txt
BEGIN READ ONLY;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM coaching.%I t;',table_name,'','',table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('InboxState','OutboxState','OutboxMessage','AdminAuditRecords') ORDER BY table_name \gexec
COMMIT;
SQL
echo 'Tables changed during the explicitly authorized browser tests:'
diff protected.before.txt protected.final.txt || test "$?" -eq 1
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | LC_ALL=C sort > original-goals.final.txt
BEGIN READ ONLY;
SELECT 'academic_goals',count(*),md5(coalesce(string_agg(md5(to_jsonb(g)::text),'' ORDER BY md5(to_jsonb(g)::text)),'')) FROM coaching.academic_goals g
WHERE g.title NOT IN ('LGS katalog canlı doğrulama · öğrenci · 19.10.2026','LGS katalog canlı doğrulama · öğretmen · 19.10.2026');
SELECT 'IdempotencyRecords',count(*),md5(coalesce(string_agg(md5(to_jsonb(i)::text),'' ORDER BY md5(to_jsonb(i)::text)),'')) FROM coaching."IdempotencyRecords" i
WHERE NOT EXISTS (SELECT 1 FROM coaching.academic_goals g WHERE g.id=i."ResourceId" AND g.title IN ('LGS katalog canlı doğrulama · öğrenci · 19.10.2026','LGS katalog canlı doğrulama · öğretmen · 19.10.2026'));
COMMIT;
SQL
grep -E '^(academic_goals|IdempotencyRecords)\|' protected.before.txt > original-goals.before.txt
cmp original-goals.before.txt original-goals.final.txt
grep -vE '^(academic_goals|IdempotencyRecords)\|' protected.before.txt > all-other.before.txt
grep -vE '^(academic_goals|IdempotencyRecords)\|' protected.final.txt > all-other.final.txt
cmp all-other.before.txt all-other.final.txt
echo 'PASS: all original goals, idempotency receipts and every other protected coaching table remain byte-for-byte equivalent.'
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT table_name FROM information_schema.tables WHERE table_schema='coaching' AND (table_name ILIKE '%goal%' OR table_name ILIKE '%idempoten%') ORDER BY table_name;
SELECT "MatchStatus",count(*) FROM coaching.target_admission_records GROUP BY "MatchStatus" ORDER BY "MatchStatus";
COMMIT;
SQL
