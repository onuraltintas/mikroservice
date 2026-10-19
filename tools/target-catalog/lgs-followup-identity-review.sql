-- Four source abbreviations resolved against current official school pages.
-- Keep every original source cell, quota, score and historical school row unchanged.
BEGIN;
SELECT pg_advisory_xact_lock(hashtext('lgs-followup-identity-20261019'));
CREATE TEMP TABLE reviewed_identity (code text PRIMARY KEY, school_id uuid, old_name text, full_name text, district text, evidence_url text);
INSERT INTO reviewed_identity VALUES
('lgs-kayseri-mtal-research:3','c9f6f421-647e-4f56-bb69-6714f9edd9d4','Kayseri Kadı Burhaneddin MTAL','Kayseri Kadı Burhaneddin Mesleki ve Teknik Anadolu Lisesi','MELİKGAZİ','https://kadiburhaneddinmtal.meb.k12.tr/'),
('lgs-kayseri-mtal-research:14','5ef71e89-baca-4cb2-be43-4316a8c37779','Atatürk Kız MTAL','Atatürk Kız Mesleki ve Teknik Anadolu Lisesi','KOCASİNAN','https://ataturkktml.meb.k12.tr/'),
('lgs-kayseri-mtal-research:15','7f1ec077-17d3-436a-94d5-7e6cb8a9ed8f','Mimar Sinan MTAL','Mimar Sinan Mesleki ve Teknik Anadolu Lisesi','MELİKGAZİ','https://mimarsinanteml.meb.k12.tr/'),
('lgs-kayseri-mtal-research:17','e3dcab7d-c5a7-4d3c-9fed-d29403a03cd5','Seyyid Burhaneddin MTAL','Seyyid Burhaneddin Mesleki ve Teknik Anadolu Lisesi','MELİKGAZİ','https://sbeml.meb.k12.tr/');
DO $$
BEGIN
 IF (SELECT count(*) FROM reviewed_identity v JOIN coaching.target_schools s ON s."Id"=v.school_id
     WHERE s."Name"=v.full_name AND s."City"='KAYSERİ' AND s."District" IN (v.district,v.district||' (MERKEZ)') AND s."IsActive")<>4 THEN
  RAISE EXCEPTION 'Reviewed canonical school identity differs from evidence';
 END IF;
 IF EXISTS (SELECT 1 FROM reviewed_identity v WHERE
     (SELECT count(*) FROM coaching.target_schools s WHERE s."Name"=v.full_name AND s."City"='KAYSERİ'
      AND s."District" IN (v.district,v.district||' (MERKEZ)'))<>1) THEN
  RAISE EXCEPTION 'Multiple canonical candidates; do not choose an arbitrary ID';
 END IF;
 IF (SELECT count(*) FROM reviewed_identity v JOIN coaching.target_admission_records r ON r."ExternalCode"=v.code
     JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId"
     WHERE b."Year"=2026 AND b."Stage"='Catalog' AND b."ExamCode"='LGS'
      AND r."City"='KAYSERİ' AND r."Institution"=v.old_name AND r."District" IS NULL
      AND r."ProgramId" IS NULL AND ((r."MatchStatus"='Pending' AND r."SchoolId" IS NULL)
          OR (r."MatchStatus"='Verified' AND r."SchoolId"=v.school_id)))<>4 THEN
  RAISE EXCEPTION 'Review scope or source cells changed';
 END IF;
END $$;
UPDATE coaching.target_admission_records r SET "SchoolId"=v.school_id,"MatchStatus"='Verified',"Issue"=NULL
FROM reviewed_identity v WHERE r."ExternalCode"=v.code AND r."MatchStatus"='Pending';
SELECT v.code,v.evidence_url,r."MatchStatus",r."SchoolId" FROM reviewed_identity v
JOIN coaching.target_admission_records r ON r."ExternalCode"=v.code ORDER BY v.code;
COMMIT;
