-- Rollback for vocabulary age fit v2: the level 3 words return to the shared pool; the copies and the new Çocuk words
-- are soft-deleted (student vocabulary history stays intact).
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('vocabulary-age-fit-v2'));
LOCK TABLE speed_reading.vocabulary_items IN SHARE ROW EXCLUSIVE MODE;

DO $$ BEGIN
  IF to_regclass('speed_reading.vocabulary_age_fit_v2_backup') IS NULL
    THEN RAISE EXCEPTION 'vocabulary-age-fit-v2 is not applied'; END IF;
END $$;

UPDATE speed_reading.vocabulary_items v
SET target_age_group_id = b.target_age_group_id, updated_at = b.updated_at, updated_by = b.updated_by,
    version = v.version + 1
FROM speed_reading.vocabulary_age_fit_v2_backup b
WHERE b.id = v.id AND v.updated_by = 'vocabulary-age-fit-v2';

UPDATE speed_reading.vocabulary_items
SET is_deleted = TRUE, deleted_at = NOW(), deleted_by = 'vocabulary-age-fit-v2-rollback',
    updated_at = NOW(), updated_by = 'vocabulary-age-fit-v2-rollback', version = version + 1
WHERE created_by = 'vocabulary-age-fit-v2' AND NOT is_deleted;

DROP TABLE speed_reading.vocabulary_age_fit_v2_backup;

SELECT coalesce(target_age_group_id::text, 'ortak') AS age, count(*) AS words
FROM speed_reading.vocabulary_items
WHERE NOT is_deleted AND difficulty_level = 3
GROUP BY 1 ORDER BY 1;
COMMIT;
