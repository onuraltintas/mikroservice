-- KM2 item 4 (read-only): which year do the LGS school rows (target_catalog, CatalogType=School) represent?
-- The MEBİ export behind them (data/target-catalog/2026/raw/mebi-lgs-full-20261020-export.json) carries
-- "sourceYear": null and "placementStage": null, and tools/target-catalog/prepare_mebi_lgs_replacement.py refuses to
-- infer them. Do NOT set CatalogYear/PlacementYear until an official, year- and stage-labelled source matches the rows.
\set ON_ERROR_STOP on
BEGIN READ ONLY;
SET LOCAL search_path = coaching;

-- 1. Current state: source | active | rows | rows with CatalogYear | rows with PlacementYear
SELECT "Source", "IsActive", count(*), count("CatalogYear"), count("PlacementYear")
FROM target_catalog WHERE "CatalogType"='School' GROUP BY 1, 2 ORDER BY 1, 2;

-- 2. Spot check against year-labelled official MEB rows already reviewed in the repo (data/target-catalog/2026):
--    reference | official year | stage | official minimum score | MEBİ minimum score
--    Equal values for every reference (to 2 decimals) would support a year; any difference means the year is not established.
WITH reference(label, city, district, school, official_year, stage, official_score, source_url) AS (VALUES
  ('Menderes Büküşoğlu AL', 'İZMİR', 'MENDERES', 'Menderes Fatma-Ramazan Büküşoğlu Anadolu Lisesi', 2026, 'unknown', 415.685,
   'https://menderes.meb.gov.tr/www/menderes-liselerinin-lgs-basari-tablosu-ve-taban-puanlari-belli-oldu/icerik/1720/tr'),
  ('Menderes Safitürk AİHL', 'İZMİR', 'MENDERES', 'Şehit Kaymakam Muhammet Fatih Safitürk Anadolu İmam Hatip Lisesi', 2026, 'unknown', 311.926,
   'https://menderes.meb.gov.tr/www/menderes-liselerinin-lgs-basari-tablosu-ve-taban-puanlari-belli-oldu/icerik/1720/tr'),
  ('Çiğli Mehpare Yağcı AİHL', 'İZMİR', 'ÇİĞLİ', 'Çiğli Mehpare Yağcı Anadolu İmam Hatip Lisesi', 2026, 'Main', 149.7576,
   'https://konakram.meb.k12.tr/meb_iys_dosyalar/35/01/164246/dosyalar/2026_08/6a7445f220e50216414292_S%C4%B1navl%C4%B1_%C4%B0lk_Yerle%C5%9Ftirme_Puanlar_%C4%B0ZM%C4%B0R_2026_LGS_WEB_%C4%B0%C3%87%C4%B0N.pdf'))
SELECT r.label, r.official_year, r.stage, r.official_score, string_agg(t."MinimumScore"::text, ' ' ORDER BY t."MinimumScore")
FROM reference r LEFT JOIN target_catalog t ON t."CatalogType"='School' AND t."Source"='mebi-lgs'
  AND t."City"=r.city AND t."District"=r.district AND t."Name"=r.school
GROUP BY 1, 2, 3, 4 ORDER BY 1;
ROLLBACK;
