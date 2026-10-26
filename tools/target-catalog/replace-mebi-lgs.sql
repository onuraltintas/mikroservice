\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '10s';
SELECT pg_advisory_xact_lock(hashtext('coaching-mebi-lgs-catalog'));
LOCK TABLE coaching.target_schools, coaching.target_admission_batches,
  coaching.target_admission_records, coaching.academic_goals IN SHARE ROW EXCLUSIVE MODE;

CREATE TEMP TABLE preserved(table_name text PRIMARY KEY, fingerprint text) ON COMMIT DROP;
DO $$ DECLARE t record; BEGIN
  FOR t IN SELECT tablename FROM pg_tables WHERE schemaname='coaching'
    AND tablename NOT IN ('target_schools','target_admission_batches','target_admission_records')
  LOOP
    EXECUTE format('INSERT INTO preserved SELECT %L,md5(coalesce(string_agg(md5(to_jsonb(r)::text),%L ORDER BY md5(to_jsonb(r)::text)),%L)) FROM coaching.%I r',t.tablename,'','',t.tablename);
  END LOOP;
END $$;
CREATE TEMP TABLE yks_batches ON COMMIT DROP AS SELECT * FROM coaching.target_admission_batches WHERE "ExamCode"<>'LGS';
CREATE TEMP TABLE yks_records ON COMMIT DROP AS SELECT r.* FROM coaching.target_admission_records r JOIN yks_batches b ON b."Id"=r."BatchId";
\ir incoming.sql

CREATE FUNCTION pg_temp.school_key(name text, city text, district text) RETURNS text
LANGUAGE sql IMMUTABLE AS $$
  SELECT translate(upper(regexp_replace(btrim(name)||'|'||btrim(city)||'|'||regexp_replace(btrim(district),' \(MERKEZ\)$','','i'),'\s+',' ','g')),'İ','I')
$$;

-- An existing current-source identity is stable on repeated imports.
UPDATE incoming i SET target_id=s."Id" FROM coaching.target_schools s
WHERE s."Source"='mebi-lgs' AND s."SourceId"=i.id::text
  AND pg_temp.school_key(s."Name",s."City",s."District")=pg_temp.school_key(i."schoolName",i.city,i.town);

CREATE TEMP TABLE referenced_matches ON COMMIT DROP AS
SELECT s."Id" AS old_id, i.id AS source_id
FROM coaching.target_schools s JOIN incoming i
  ON pg_temp.school_key(s."Name",s."City",s."District")=pg_temp.school_key(i."schoolName",i.city,i.town)
WHERE EXISTS(SELECT 1 FROM coaching.academic_goals g WHERE g.target_school_id=s."Id")
  AND (s."Source"<>'mebi-lgs' OR s."SourceId"=i.id::text);

DO $$ BEGIN
  IF (SELECT count(*) FROM incoming)<>3150
    OR EXISTS(SELECT 1 FROM incoming WHERE "schoolName" IS NULL OR city IS NULL OR town IS NULL OR "schoolType" IS NULL OR language IS NULL
      OR "minScore" IS NULL OR "minScore"<0 OR "minScore">500 OR branch IS NOT NULL)
  THEN RAISE EXCEPTION 'Unexpected or incomplete reviewed source'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.academic_goals g WHERE g.target_school_id IS NOT NULL
    AND (SELECT count(*) FROM referenced_matches m WHERE m.old_id=g.target_school_id)<>1)
    OR EXISTS(SELECT 1 FROM referenced_matches GROUP BY source_id HAVING count(*)<>1)
  THEN RAISE EXCEPTION 'Referenced school has no unique source match; no catalog data was replaced'; END IF;
END $$;
UPDATE incoming i SET target_id=m.old_id FROM referenced_matches m WHERE m.source_id=i.id;
ALTER TABLE incoming ADD UNIQUE(target_id);

DO $$ BEGIN
  IF EXISTS(SELECT 1 FROM incoming i JOIN coaching.target_schools s ON s."Id"=i.target_id
    WHERE pg_temp.school_key(s."Name",s."City",s."District")<>pg_temp.school_key(i."schoolName",i.city,i.town))
  THEN RAISE EXCEPTION 'An unrelated school identity would be overwritten'; END IF;
END $$;

DELETE FROM coaching.target_admission_records r USING coaching.target_admission_batches b
WHERE r."BatchId"=b."Id" AND b."ExamCode"='LGS';
DELETE FROM coaching.target_admission_batches WHERE "ExamCode"='LGS';
DELETE FROM coaching.target_schools s WHERE NOT EXISTS(SELECT 1 FROM incoming i WHERE i.target_id=s."Id");

INSERT INTO coaching.target_schools("Id","Source","SourceId","Name","City","District","MinimumScore","ScoreYear","IsActive","SchoolType","Language","Field")
SELECT target_id,'mebi-lgs',id::text,"schoolName",city,town,"minScore",NULL,TRUE,"schoolType",language,branch FROM incoming
ON CONFLICT ("Id") DO UPDATE SET
  "Source"=EXCLUDED."Source","SourceId"=EXCLUDED."SourceId","Name"=EXCLUDED."Name",
  "City"=EXCLUDED."City","District"=EXCLUDED."District","MinimumScore"=EXCLUDED."MinimumScore",
  "ScoreYear"=NULL,"IsActive"=TRUE,"SchoolType"=EXCLUDED."SchoolType","Language"=EXCLUDED."Language","Field"=EXCLUDED."Field";

DO $$ DECLARE t record; actual text; BEGIN
  IF (SELECT count(*) FROM coaching.target_schools)<>3150
    OR EXISTS(SELECT 1 FROM incoming i LEFT JOIN coaching.target_schools s ON s."Id"=i.target_id
      WHERE s."Id" IS NULL OR s."Source"<>'mebi-lgs' OR s."SourceId"<>i.id::text OR NOT s."IsActive"
        OR s."Name"<>i."schoolName" OR s."City"<>i.city OR s."District"<>i.town
        OR s."MinimumScore" IS DISTINCT FROM i."minScore" OR s."SchoolType" IS DISTINCT FROM i."schoolType"
        OR s."Language" IS DISTINCT FROM i.language OR s."Field" IS DISTINCT FROM i.branch OR s."ScoreYear" IS NOT NULL)
  THEN RAISE EXCEPTION 'Saved catalog differs from reviewed source'; END IF;
  IF EXISTS(SELECT * FROM yks_batches EXCEPT SELECT * FROM coaching.target_admission_batches)
    OR EXISTS(SELECT * FROM coaching.target_admission_batches EXCEPT SELECT * FROM yks_batches)
    OR EXISTS(SELECT * FROM yks_records EXCEPT SELECT * FROM coaching.target_admission_records)
    OR EXISTS(SELECT * FROM coaching.target_admission_records EXCEPT SELECT * FROM yks_records)
  THEN RAISE EXCEPTION 'YKS source evidence changed'; END IF;
  FOR t IN SELECT * FROM preserved LOOP
    EXECUTE format('SELECT md5(coalesce(string_agg(md5(to_jsonb(r)::text),%L ORDER BY md5(to_jsonb(r)::text)),%L)) FROM coaching.%I r','','',t.table_name) INTO actual;
    IF actual IS DISTINCT FROM t.fingerprint THEN RAISE EXCEPTION 'Protected table changed: %',t.table_name; END IF;
  END LOOP;
END $$;
SELECT json_build_object('schools',(SELECT count(*) FROM coaching.target_schools),
  'preserved_school_targets',(SELECT count(*) FROM coaching.academic_goals WHERE target_school_id IS NOT NULL),
  'preserved_tables',(SELECT count(*) FROM preserved),'old_lgs_batches',(SELECT count(*) FROM coaching.target_admission_batches WHERE "ExamCode"='LGS'));
COMMIT;
