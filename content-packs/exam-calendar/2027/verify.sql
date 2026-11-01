-- KM1 exam calendar 2027: read-only check that apply.sql's exams and 2027 sessions are in place with the official dates.
-- Run on the Coaching database:  psql -X -qAt -v ON_ERROR_STOP=1 -f verify.sql
-- Prints "ok: ..." as a NOTICE, or fails with the list of differences (an error is never read as success).
BEGIN READ ONLY;
DO $$
DECLARE problems text; exams text;
BEGIN
    IF to_regclass('coaching.study_catalog_exam_sessions') IS NULL THEN
        RAISE EXCEPTION 'coaching.study_catalog_exam_sessions is missing: the ExamSessionsAndNewPurposes migration has not run.';
    END IF;
    WITH expected (code, label, date, application_start, application_end) AS (VALUES
        ('TYT',    '2027',   DATE '2027-06-19', DATE '2027-02-04', DATE '2027-03-02'),
        ('AYT',    '2027',   DATE '2027-06-20', DATE '2027-02-04', DATE '2027-03-02'),
        ('YDT',    '2027',   DATE '2027-06-20', DATE '2027-02-04', DATE '2027-03-02'),
        ('AGS',    '2027',   DATE '2027-05-08', DATE '2027-03-19', DATE '2027-03-29'),
        ('OABT',   '2027',   DATE '2027-05-09', DATE '2027-03-19', DATE '2027-03-29'),
        ('YDS',    '2027/1', DATE '2027-03-28', DATE '2027-02-03', DATE '2027-02-11'),
        ('YDS',    '2027/2', DATE '2027-11-21', DATE '2027-10-05', DATE '2027-10-13'),
        ('YOKDIL', '2027/1', DATE '2027-02-28', DATE '2027-01-12', DATE '2027-01-20'),
        ('YOKDIL', '2027/2', DATE '2027-07-25', DATE '2027-06-08', DATE '2027-06-16'))
    SELECT string_agg(x.code || ' ' || x.label || ' ' || x.date, ', ') INTO problems FROM expected x
    WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_exam_sessions s JOIN coaching.study_catalog_exams e ON e."Id" = s."ExamId"
        WHERE e."Code" = x.code AND s."Label" = x.label AND s."Date" = x.date
          AND s."ApplicationStart" = x.application_start AND s."ApplicationEnd" = x.application_end);
    IF problems IS NOT NULL THEN RAISE EXCEPTION 'missing or different 2027 sessions: %', problems; END IF;
    SELECT string_agg(code, ', ') INTO problems FROM unnest(ARRAY['LGS','TYT','AYT','YDT','AGS','OABT','YDS','YOKDIL']) AS code
    WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_exams e WHERE e."Code" = code);
    IF problems IS NOT NULL THEN RAISE EXCEPTION 'missing exams: %', problems; END IF;
    SELECT string_agg("Code" || CASE WHEN "IsActive" THEN '' ELSE ' (pasif)' END, ', ' ORDER BY "DisplayOrder", "Code") INTO exams
    FROM coaching.study_catalog_exams WHERE "Code" IN ('LGS','TYT','AYT','YDT','AGS','OABT','YDS','YOKDIL');
    RAISE NOTICE 'ok: 9 sessions of 2027 match the ÖSYM calendar; exams %', exams;
END $$;
COMMIT;
