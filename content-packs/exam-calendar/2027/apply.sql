-- KM1 exam calendar 2027 (01.11.2026): the coaching catalog exams TYT, AYT, YDT, LGS, AGS, ÖABT, YDS and YÖKDİL and their
-- 2027 sessions from the ÖSYM 2027 exam calendar announced on 28.10.2026
-- (https://dokuman.osym.gov.tr/web/2026/10/osym-2027-yili-sinav-takvimi-7d09wi-28090734.pdf). LGS 2027 is not announced yet
-- (MEB): the exam is kept, without a session.
--
-- Exams that are missing are added PASSIVE (IsActive = false): the team reviews and activates them in the admin panel
-- ("Ders ve Müfredat Yönetimi" > Sınav türleri). Existing exams keep their name and status. Sessions are upserted by
-- (exam, label): a session with the same label gets the official date and application window; nothing else is touched.
-- Stops without changing anything if the ExamSessionsAndNewPurposes migration has not run, or if another session of the same
-- exam already sits on one of these dates.
--
-- Run on the Coaching database in one transaction:
--   psql -X -qAt -v ON_ERROR_STOP=1 -f apply.sql
-- Prints "before: ..." and "after: ..." as NOTICEs. Running it again changes nothing.
BEGIN;
SET LOCAL lock_timeout = '5s';

DO $$
BEGIN
    IF to_regclass('coaching.study_catalog_exam_sessions') IS NULL THEN
        RAISE EXCEPTION 'coaching.study_catalog_exam_sessions is missing: deploy the ExamSessionsAndNewPurposes migration first. Nothing changed.';
    END IF;
END $$;

LOCK TABLE coaching.study_catalog_exams, coaching.study_catalog_exam_sessions IN SHARE ROW EXCLUSIVE MODE;

CREATE TEMP TABLE km1_calendar_exams (code varchar(10) PRIMARY KEY, id uuid NOT NULL, name text NOT NULL, display_order int NOT NULL) ON COMMIT DROP;
INSERT INTO km1_calendar_exams VALUES
    ('LGS',    'f0ca1000-0000-4000-8000-000000000001', 'Liselere Geçiş Sistemi', 0),
    ('TYT',    'f0ca1000-0000-4000-8000-000000000002', 'Temel Yeterlilik Testi', 1),
    ('AYT',    'f0ca1000-0000-4000-8000-000000000003', 'Alan Yeterlilik Testleri', 2),
    ('YDT',    'f0ca1000-0000-4000-8000-000000000004', 'Yabancı Dil Testi', 3),
    ('AGS',    'f0ca1000-0000-4000-8000-000000000006', 'Akademi Giriş Sınavı', 5),
    ('OABT',   'f0ca1000-0000-4000-8000-000000000007', 'Öğretmenlik Alan Bilgisi Testi', 6),
    ('YDS',    'f0ca1000-0000-4000-8000-000000000008', 'Yabancı Dil Bilgisi Seviye Tespit Sınavı', 7),
    ('YOKDIL', 'f0ca1000-0000-4000-8000-000000000009', 'Yükseköğretim Kurumları Yabancı Dil Sınavı', 8);

-- (exam, label, exam day, application start, application end) from the ÖSYM 2027 calendar.
CREATE TEMP TABLE km1_calendar_sessions (code varchar(10), label varchar(100), date date, application_start date, application_end date,
    PRIMARY KEY (code, label)) ON COMMIT DROP;
INSERT INTO km1_calendar_sessions VALUES
    ('TYT',    '2027',   DATE '2027-06-19', DATE '2027-02-04', DATE '2027-03-02'),
    ('AYT',    '2027',   DATE '2027-06-20', DATE '2027-02-04', DATE '2027-03-02'),
    ('YDT',    '2027',   DATE '2027-06-20', DATE '2027-02-04', DATE '2027-03-02'),
    ('AGS',    '2027',   DATE '2027-05-08', DATE '2027-03-19', DATE '2027-03-29'),
    ('OABT',   '2027',   DATE '2027-05-09', DATE '2027-03-19', DATE '2027-03-29'),
    ('YDS',    '2027/1', DATE '2027-03-28', DATE '2027-02-03', DATE '2027-02-11'),
    ('YDS',    '2027/2', DATE '2027-11-21', DATE '2027-10-05', DATE '2027-10-13'),
    ('YOKDIL', '2027/1', DATE '2027-02-28', DATE '2027-01-12', DATE '2027-01-20'),
    ('YOKDIL', '2027/2', DATE '2027-07-25', DATE '2027-06-08', DATE '2027-06-16');

DO $$
DECLARE exams integer; sessions integer; conflicts text;
BEGIN
    SELECT count(*) INTO exams FROM coaching.study_catalog_exams;
    SELECT count(*) INTO sessions FROM coaching.study_catalog_exam_sessions s JOIN coaching.study_catalog_exams e ON e."Id" = s."ExamId"
    WHERE e."Code" IN (SELECT code FROM km1_calendar_exams) AND s."Date" BETWEEN DATE '2027-01-01' AND DATE '2027-12-31';
    RAISE NOTICE 'before: exams %, 2027 sessions %', exams, sessions;
    SELECT string_agg(e."Code" || ' ' || s."Date" || ' (' || s."Label" || ')', ', ') INTO conflicts
    FROM km1_calendar_sessions c JOIN coaching.study_catalog_exams e ON e."Code" = c.code
    JOIN coaching.study_catalog_exam_sessions s ON s."ExamId" = e."Id" AND s."Date" = c.date AND s."Label" <> c.label;
    IF conflicts IS NOT NULL THEN
        RAISE EXCEPTION 'Another session already sits on an official 2027 date: %. Rename or remove it in the admin panel, then run again. Nothing changed.', conflicts;
    END IF;
END $$;

INSERT INTO coaching.study_catalog_exams ("Id", "Code", "Name", "DisplayOrder", "IsActive")
SELECT c.id, c.code, c.name, c.display_order, false FROM km1_calendar_exams c
WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_exams e WHERE e."Code" = c.code);

INSERT INTO coaching.study_catalog_exam_sessions ("Id", "ExamId", "Date", "Label", "ApplicationStart", "ApplicationEnd")
SELECT gen_random_uuid(), e."Id", c.date, c.label, c.application_start, c.application_end
FROM km1_calendar_sessions c JOIN coaching.study_catalog_exams e ON e."Code" = c.code
ON CONFLICT ("ExamId", "Label") DO UPDATE SET "Date" = EXCLUDED."Date", "ApplicationStart" = EXCLUDED."ApplicationStart",
    "ApplicationEnd" = EXCLUDED."ApplicationEnd"
WHERE (coaching.study_catalog_exam_sessions."Date", coaching.study_catalog_exam_sessions."ApplicationStart", coaching.study_catalog_exam_sessions."ApplicationEnd")
    IS DISTINCT FROM (EXCLUDED."Date", EXCLUDED."ApplicationStart", EXCLUDED."ApplicationEnd");

DO $$
DECLARE exams integer; sessions integer; missing integer; passive text;
BEGIN
    SELECT count(*) INTO missing FROM km1_calendar_sessions c
    WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_exam_sessions s JOIN coaching.study_catalog_exams e ON e."Id" = s."ExamId"
        WHERE e."Code" = c.code AND s."Label" = c.label AND s."Date" = c.date
          AND s."ApplicationStart" IS NOT DISTINCT FROM c.application_start AND s."ApplicationEnd" IS NOT DISTINCT FROM c.application_end);
    IF missing > 0 OR (SELECT count(*) FROM coaching.study_catalog_exams WHERE "Code" IN (SELECT code FROM km1_calendar_exams)) <> 8 THEN
        RAISE EXCEPTION 'after-check failed (% sessions differ): rolled back', missing;
    END IF;
    SELECT count(*) INTO exams FROM coaching.study_catalog_exams;
    SELECT count(*) INTO sessions FROM coaching.study_catalog_exam_sessions s JOIN coaching.study_catalog_exams e ON e."Id" = s."ExamId"
    WHERE e."Code" IN (SELECT code FROM km1_calendar_exams) AND s."Date" BETWEEN DATE '2027-01-01' AND DATE '2027-12-31';
    SELECT coalesce(string_agg("Code", ', ' ORDER BY "DisplayOrder"), '-') INTO passive FROM coaching.study_catalog_exams
    WHERE "Code" IN (SELECT code FROM km1_calendar_exams) AND NOT "IsActive";
    RAISE NOTICE 'after: exams %, 2027 sessions %, passive (activate in the admin panel): %', exams, sessions, passive;
END $$;
COMMIT;
