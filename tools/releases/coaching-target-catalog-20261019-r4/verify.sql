DO $$
DECLARE
    program_count integer;
    active_count integer;
    unique_code_count integer;
    yearly_count integer;
    ydt_count integer;
BEGIN
    SELECT count(*), count(*) FILTER (WHERE "IsActive"), count(DISTINCT "ProgramCode")
    INTO program_count, active_count, unique_code_count
    FROM coaching.target_university_programs;
    IF program_count <> 21493 OR active_count <> 21493 OR unique_code_count <> 21493 THEN
        RAISE EXCEPTION 'Expected exactly 21,493 unique active 2026 programs; found rows %, active %, unique codes %',
            program_count, active_count, unique_code_count;
    END IF;
    IF (SELECT count(*) FROM coaching.target_university_programs WHERE "ProgramCode" IS NULL) <> 0 THEN
        RAISE EXCEPTION 'Program rows without a 2026 source code remain.';
    END IF;
    IF (SELECT count(*) FROM coaching.target_university_programs WHERE "ScoreType" = 'TYT') <> 9254
       OR (SELECT count(*) FROM coaching.target_university_programs WHERE "ScoreType" = 'SAY') <> 5744
       OR (SELECT count(*) FROM coaching.target_university_programs WHERE "ScoreType" = 'EA') <> 3900
       OR (SELECT count(*) FROM coaching.target_university_programs WHERE "ScoreType" = 'SOZ') <> 1914
       OR (SELECT count(*) FROM coaching.target_university_programs WHERE "ScoreType" = 'YDT') <> 681
       OR (SELECT count(*) FROM coaching.target_university_programs WHERE "ScoreType" NOT IN ('TYT','SAY','EA','SOZ','YDT')) <> 0 THEN
        RAISE EXCEPTION 'The program catalog score-type distribution does not match the verified 2026 source.';
    END IF;

    SELECT count(*) INTO yearly_count
    FROM coaching.target_admission_records r
    JOIN coaching.target_admission_batches b ON b."Id" = r."BatchId"
    WHERE b."ExamCode" = 'YKS' AND b."Year" = 2026 AND b."Stage" = 'Main';
    SELECT count(*) INTO ydt_count
    FROM coaching.target_admission_records r
    JOIN coaching.target_admission_batches b ON b."Id" = r."BatchId"
    WHERE b."ExamCode" = 'YKS' AND b."Year" = 2026 AND b."Stage" = 'Main'
      AND r."ScoreType" = 'YDT' AND r."MatchStatus" = 'Verified';
    IF yearly_count <> 21493 OR ydt_count <> 681 THEN
        RAISE EXCEPTION 'Expected 21,493 annual YKS results including 681 verified YDT rows; found total %, YDT %',
            yearly_count, ydt_count;
    END IF;
    IF (SELECT count(*) FROM coaching.target_admission_batches
        WHERE "ExamCode" = 'YKS' AND "Year" = 2026 AND "Stage" = 'Main'
          AND "DocumentHash" = 'c907978c90fbe975dd5025332cd8464207a4e4ad9dc27f25a251dc55e5baeabc'
          AND "SourceUrl" = 'https://www.osym.gov.tr/2026-yks-yerlestirme-sonuclarina-iliskin-sayisal-bilgiler') <> 1 THEN
        RAISE EXCEPTION 'The official 2026 YDT results batch is missing or duplicated.';
    END IF;
END $$;

SELECT "ScoreType", count(*) FROM coaching.target_university_programs GROUP BY "ScoreType" ORDER BY "ScoreType";
SELECT 'YKS 2026 Main records', count(*)
FROM coaching.target_admission_records r
JOIN coaching.target_admission_batches b ON b."Id" = r."BatchId"
WHERE b."ExamCode" = 'YKS' AND b."Year" = 2026 AND b."Stage" = 'Main';
SELECT 'YDT verified', count(*)
FROM coaching.target_admission_records r
JOIN coaching.target_admission_batches b ON b."Id" = r."BatchId"
WHERE b."ExamCode" = 'YKS' AND b."Year" = 2026 AND b."Stage" = 'Main'
  AND r."ScoreType" = 'YDT' AND r."MatchStatus" = 'Verified';
