-- Rollback for vocabulary age fit v1: the level 4-5 words return to the shared pool; the copies and the
-- new Çocuk/Genç words are soft-deleted (student vocabulary history stays intact).
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('vocabulary-age-fit-v1'));
LOCK TABLE speed_reading.vocabulary_items IN SHARE ROW EXCLUSIVE MODE;

DO $$ BEGIN
  IF to_regclass('speed_reading.vocabulary_age_fit_v1_backup') IS NULL
    THEN RAISE EXCEPTION 'vocabulary-age-fit-v1 is not applied'; END IF;
END $$;

UPDATE speed_reading.vocabulary_items v
SET target_age_group_id = b.target_age_group_id, updated_at = b.updated_at, updated_by = b.updated_by,
    version = v.version + 1
FROM speed_reading.vocabulary_age_fit_v1_backup b
WHERE b.id = v.id AND v.target_age_group_id = '10000000-0000-0000-0000-000000000003'::uuid;

UPDATE speed_reading.vocabulary_items
SET is_deleted = TRUE, deleted_at = NOW(), deleted_by = 'vocabulary-age-fit-v1-rollback',
    updated_at = NOW(), updated_by = 'vocabulary-age-fit-v1-rollback', version = version + 1
WHERE created_by = 'vocabulary-age-fit-v1' AND NOT is_deleted;

DROP TABLE speed_reading.vocabulary_age_fit_v1_backup;

SELECT target_age_group_id, difficulty_level, count(*) AS words
FROM speed_reading.vocabulary_items
WHERE NOT is_deleted AND difficulty_level IN (4, 5)
GROUP BY 1, 2 ORDER BY 1, 2;
COMMIT;
