BEGIN;

DO $reconcile$
DECLARE
    old_migration_count integer;
    new_migration_count integer;
    product_version text;
    institution_id_type text;
    institution_id_nullable text;
    institution_id_default text;
    institution_id_generated text;
    institution_id_identity text;
    institution_index_unique boolean;
    institution_index_valid boolean;
    institution_index_has_no_predicate boolean;
    institution_index_attribute_count integer;
    institution_index_key_attribute_count integer;
    institution_index_method text;
    institution_index_definition text;
BEGIN
    SELECT count(*), max("ProductVersion")
    INTO old_migration_count, product_version
    FROM coaching.__ef_migrations_history
    WHERE "MigrationId" = '20260924152005_AddAcademicGoalInstitutionScope';

    IF old_migration_count <> 1 OR product_version IS NULL THEN
        RAISE EXCEPTION 'Expected the previously applied institution-scope migration and its EF product version.';
    END IF;

    SELECT count(*)
    INTO new_migration_count
    FROM coaching.__ef_migrations_history
    WHERE "MigrationId" = '20260925161618_AddInstitutionScopeToAcademicGoals';

    IF new_migration_count <> 0 THEN
        RAISE EXCEPTION 'The migration being reconciled is already recorded.';
    END IF;

    SELECT udt_name, is_nullable, column_default, is_generated, is_identity
    INTO institution_id_type,
         institution_id_nullable,
         institution_id_default,
         institution_id_generated,
         institution_id_identity
    FROM information_schema.columns
    WHERE table_schema = 'coaching'
      AND table_name = 'academic_goals'
      AND column_name = 'institution_id';

    IF institution_id_type IS DISTINCT FROM 'uuid'
       OR institution_id_nullable IS DISTINCT FROM 'YES'
       OR institution_id_default IS NOT NULL
       OR institution_id_generated IS DISTINCT FROM 'NEVER'
       OR institution_id_identity IS DISTINCT FROM 'NO' THEN
        RAISE EXCEPTION 'The existing institution_id column does not match the expected nullable UUID schema.';
    END IF;

    SELECT index_row.indisunique,
           index_row.indisvalid,
           index_row.indpred IS NULL,
           index_row.indnatts,
           index_row.indnkeyatts,
           access_method.amname,
           pg_get_indexdef(index_row.indexrelid)
    INTO institution_index_unique,
         institution_index_valid,
         institution_index_has_no_predicate,
         institution_index_attribute_count,
         institution_index_key_attribute_count,
         institution_index_method,
         institution_index_definition
    FROM pg_index AS index_row
    JOIN pg_class AS index_class ON index_class.oid = index_row.indexrelid
    JOIN pg_namespace AS index_schema ON index_schema.oid = index_class.relnamespace
    JOIN pg_am AS access_method ON access_method.oid = index_class.relam
    WHERE index_row.indrelid = 'coaching.academic_goals'::regclass
      AND index_schema.nspname = 'coaching'
      AND index_class.relname = 'ix_academic_goals_institution_student';

    IF institution_index_unique IS DISTINCT FROM false
       OR institution_index_valid IS DISTINCT FROM true
       OR institution_index_has_no_predicate IS DISTINCT FROM true
       OR institution_index_attribute_count IS DISTINCT FROM 2
       OR institution_index_key_attribute_count IS DISTINCT FROM 2
       OR institution_index_method IS DISTINCT FROM 'btree'
       OR institution_index_definition IS DISTINCT FROM
          'CREATE INDEX ix_academic_goals_institution_student ON coaching.academic_goals USING btree (institution_id, student_id)' THEN
        RAISE EXCEPTION 'The existing institution/student index does not match the expected non-unique index.';
    END IF;

    INSERT INTO coaching.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260925161618_AddInstitutionScopeToAcademicGoals', product_version);
END
$reconcile$;

COMMIT;
