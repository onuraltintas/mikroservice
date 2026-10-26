-- Reviewed identity evidence is recorded in lgs-review-r3-20261019/sources.json.
-- Never rewrite source cells or move goals away from an existing school ID.
BEGIN;
SELECT pg_advisory_xact_lock(hashtext('lgs-review-r3-20261019'));
DO $$
BEGIN
 IF (SELECT count(*) FROM coaching.target_admission_records r JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId"
     WHERE r."ExternalCode"='lgs-kayseri-mtal-research:1' AND r."Institution"='Melikgazi Belediyesi Mehmet Altun Havacılık ve Uzay Teknolojileri MTAL'
       AND r."City"='KAYSERİ' AND r."District" IS NULL AND b."Year"=2026 AND b."ExamCode"='LGS' AND b."Stage"='Catalog'
       AND r."ProgramId" IS NULL AND r."MatchStatus" IN ('Pending','Verified'))<>1 THEN
  RAISE EXCEPTION 'Kayseri source identity or review scope changed';
 END IF;
 IF EXISTS (SELECT 1 FROM coaching.target_schools WHERE "Name"='Melikgazi Belediyesi Mehmet Altun Havacılık ve Uzay Teknolojileri Mesleki ve Teknik Anadolu Lisesi'
     AND "City"='KAYSERİ' AND "SourceId"<>'review-r3:mehmet-altun') THEN
  RAISE EXCEPTION 'Another school identity exists; review before creating a new one';
 END IF;
 IF (SELECT count(*) FROM coaching.target_admission_records r JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId"
     WHERE r."ExternalCode" IN ('ist:751600:2036f00ba075bf5a90969e62','ist:751600:1d09de8afd34393d38426ce5')
       AND r."Institution"='ŞİLE AYET AZER ARAN SAVUNMA SANAYİ MESLEKİ VE TEKNİK ANADOLU LİSESİ'
       AND r."City"='İSTANBUL' AND r."District"='ŞİLE' AND r."SchoolId" IS NULL AND r."ProgramId" IS NULL
       AND b."Year"=2026 AND b."ExamCode"='LGS' AND b."Stage"='Catalog' AND r."MatchStatus" IN ('Conflict','Ambiguous'))<>2 THEN
  RAISE EXCEPTION 'Sile source identity or review scope changed';
 END IF;
END $$;
INSERT INTO coaching.target_schools ("Id","Source","SourceId","Name","City","District","MinimumScore","ScoreYear","IsActive","ProvinceId","DistrictId")
VALUES ('a4eff62d-693a-4dd5-93f4-d09178f61282','meb-2026','review-r3:mehmet-altun',
 'Melikgazi Belediyesi Mehmet Altun Havacılık ve Uzay Teknolojileri Mesleki ve Teknik Anadolu Lisesi','KAYSERİ','MELİKGAZİ',NULL,NULL,true,NULL,NULL)
ON CONFLICT ("Source","SourceId") DO NOTHING;
DO $$
BEGIN
 IF NOT EXISTS (SELECT 1 FROM coaching.target_schools WHERE "Id"='a4eff62d-693a-4dd5-93f4-d09178f61282' AND "Source"='meb-2026'
    AND "SourceId"='review-r3:mehmet-altun' AND "City"='KAYSERİ' AND "District"='MELİKGAZİ' AND "IsActive") THEN
  RAISE EXCEPTION 'Reviewed target identity changed';
 END IF;
 IF EXISTS (SELECT 1 FROM coaching.target_admission_records WHERE "ExternalCode"='lgs-kayseri-mtal-research:1' AND "MatchStatus"='Verified'
    AND "SchoolId" IS DISTINCT FROM 'a4eff62d-693a-4dd5-93f4-d09178f61282'::uuid) THEN
  RAISE EXCEPTION 'Do not replace an existing verified link';
 END IF;
END $$;
UPDATE coaching.target_admission_records SET "SchoolId"='a4eff62d-693a-4dd5-93f4-d09178f61282',"MatchStatus"='Verified',"Issue"=NULL
WHERE "ExternalCode"='lgs-kayseri-mtal-research:1' AND "MatchStatus"='Pending';
UPDATE coaching.target_admission_records SET "MatchStatus"='Ambiguous',
 "Issue"='Official institution code is 768129 (https://mtegm.meb.gov.tr/kurumlar/?kurumkodu=768129&p=kurum), not source code 751600. Original source code retained. Two legacy school IDs remain indistinguishable; no arbitrary link applied.'
WHERE "ExternalCode" IN ('ist:751600:2036f00ba075bf5a90969e62','ist:751600:1d09de8afd34393d38426ce5') AND "MatchStatus"='Conflict';
COMMIT;
