-- Official 2026 OSYM and YOK titles agree with each other, but not with the legacy subject.
-- Preserve the old program and every goal referencing it; withhold only this new annual row.
BEGIN;
SELECT pg_advisory_xact_lock(hashtext('yks-identity-review-20261019'));
DO $$ BEGIN
 IF (SELECT count(*) FROM coaching.target_university_programs WHERE "ProgramCode"='409610874' AND "Name"='Ekonomi (%25 İndirimli)' AND "ScoreType"='EA')<>1 THEN
  RAISE EXCEPTION 'Legacy program identity changed; review again';
 END IF;
 IF (SELECT count(*) FROM coaching.target_admission_records r JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId"
     WHERE b."ExamCode"='YKS' AND b."Year"=2026 AND b."Stage"='Main'
       AND b."DocumentHash"='3ff87dd3d41fd8a74fa44c8685b86da0f09fe5ba5faea97dbfc47cd90b30bd37'
       AND r."ExternalCode"='409610874' AND r."Name"='Gastronomi   (Ücretli)' AND r."ScoreType"='EA'
       AND r."Quota"=5 AND r."Placed"=0 AND r."MinimumScore" IS NULL AND r."SchoolId" IS NULL
       AND ((r."MatchStatus"='Verified' AND r."ProgramId"=(SELECT "Id" FROM coaching.target_university_programs WHERE "ProgramCode"='409610874'))
         OR (r."MatchStatus"='Conflict' AND r."ProgramId" IS NULL)))<>1 THEN
  RAISE EXCEPTION 'Official 2026 row or review scope changed';
 END IF;
END $$;
UPDATE coaching.target_admission_records SET "ProgramId"=NULL,"MatchStatus"='Conflict',
 "Issue"='Program identity requires review: legacy Economics (25% discount) versus official 2026 Gastronomy (paid). Same code and score type are insufficient; existing legacy goals preserved.'
WHERE "ExternalCode"='409610874' AND "MatchStatus"='Verified' AND "BatchId" IN (SELECT "Id" FROM coaching.target_admission_batches WHERE "ExamCode"='YKS' AND "Year"=2026 AND "Stage"='Main');
COMMIT;
