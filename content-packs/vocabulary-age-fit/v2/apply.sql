-- Vocabulary age fit v2 (follow-up to the live test 19.11.2026, A2, decision K4): level 3 of the shared pool.
-- * Business, economy, human resources, management and law terms (Likidite, Bordro, Cari Açık …) go to adults only:
--   the row to Yetişkin, a copy to Genç yetişkin.
-- * Other academic, political and historical terms stay from Genç upwards but leave Çocuk: the row to Genç, copies to
--   Genç yetişkin and Yetişkin.
-- * Çocuk keeps geography and a few everyday words, and gets 25 age-appropriate words of its own.
-- Run once, as a whole; any failing check rolls everything back. Independent of v1 (levels 4-5).
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('vocabulary-age-fit-v2'));
LOCK TABLE speed_reading.vocabulary_items IN SHARE ROW EXCLUSIVE MODE;

CREATE TEMP TABLE age_fit_words ON COMMIT DROP AS
SELECT (w->>'id')::uuid id, w->>'word' word, w->>'definition' definition, w->>'category' category,
       (w->>'level')::int level, (w->>'ageGroupId')::uuid age_id
FROM jsonb_array_elements($words$[{"id":"a9e00000-0000-4000-8130-000000000001","word":"Pusula","definition":"Yön bulmaya yarayan, iğnesi hep kuzeyi gösteren araçtır.","category":"Coğrafya","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000002","word":"Buzul","definition":"Kutuplarda ve yüksek dağlarda biriken büyük buz kütlesidir.","category":"Coğrafya","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000003","word":"Şelale","definition":"Yüksek bir yerden aşağıya dökülen akarsudur.","category":"Coğrafya","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000004","word":"Mağara","definition":"Kayaların içinde doğal olarak oluşmuş derin oyuktur.","category":"Coğrafya","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000005","word":"Gezegen","definition":"Bir yıldızın çevresinde dönen büyük gök cismidir.","category":"Bilim","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000006","word":"Termometre","definition":"Sıcaklığı ölçmeye yarayan araçtır.","category":"Bilim","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000007","word":"Mikroskop","definition":"Gözle görülemeyecek kadar küçük şeyleri büyüterek gösteren araçtır.","category":"Bilim","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000008","word":"Hasat","definition":"Olgunlaşan ürünlerin tarladan ya da bahçeden toplanmasıdır.","category":"Doğa","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000009","word":"Fidan","definition":"Yeni dikilmiş genç ağaçtır.","category":"Doğa","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000010","word":"Kovan","definition":"Arıların içinde yaşayıp bal yaptığı yuvadır.","category":"Doğa","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000011","word":"Göçmen kuş","definition":"Mevsim değişince sıcak yerlere uçan kuştur.","category":"Doğa","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000012","word":"Kervan","definition":"Yük taşıyan hayvanlardan ve yolculardan oluşan topluluktur.","category":"Tarih","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000013","word":"Kâşif","definition":"Bilinmeyen yerleri bulmak için yolculuğa çıkan kişidir.","category":"Tarih","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000014","word":"Müze","definition":"Tarihî ve sanat eserlerinin sergilendiği yerdir.","category":"Kültür","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000015","word":"Arşiv","definition":"Eski belge ve kayıtların saklandığı yerdir.","category":"Genel","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000016","word":"Tasarruf","definition":"Parayı ya da bir kaynağı gereksiz yere harcamadan saklamaktır.","category":"Genel","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000017","word":"Gönüllü","definition":"Bir işi karşılık beklemeden isteyerek yapan kişidir.","category":"Toplum","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000018","word":"Yardımlaşma","definition":"İnsanların zor zamanda birbirine destek olmasıdır.","category":"Toplum","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000019","word":"Saygı","definition":"Başkalarının duygularına ve haklarına önem vermektir.","category":"Kişilik","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000020","word":"Dürüstlük","definition":"Doğruyu söylemek ve hile yapmamaktır.","category":"Kişilik","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000021","word":"Bilmece","definition":"Cevabı düşünülerek bulunan eğlenceli sorudur.","category":"Edebiyat","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000022","word":"Efsane","definition":"Halk arasında anlatılan, olağanüstü olaylar içeren eski öyküdür.","category":"Edebiyat","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000023","word":"Ninni","definition":"Bebekleri uyutmak için söylenen yumuşak ezgili şarkıdır.","category":"Kültür","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000024","word":"Sergi","definition":"Eserlerin insanlara gösterilmek için bir yerde düzenlenmesidir.","category":"Sanat","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"},{"id":"a9e00000-0000-4000-8130-000000000025","word":"Heykel","definition":"Taş, metal ya da kilden yapılan üç boyutlu sanat eseridir.","category":"Sanat","level":3,"ageGroupId":"10000000-0000-0000-0000-000000000001"}]$words$::jsonb) w;
CREATE TEMP TABLE adult_categories ON COMMIT DROP AS
SELECT value AS category FROM jsonb_array_elements_text($json$["Ekonomi","Insan Kaynaklari","Is Dunyasi","İş Dünyası","Yonetim","Hukuk"]$json$::jsonb);
CREATE TEMP TABLE child_keep ON COMMIT DROP AS
SELECT k->>0 AS category, k->>1 AS word FROM jsonb_array_elements($json$[["Tarih","Anıt"],["Tarih","Demokrasi"],["Tarih","Taht"],["Edebiyat","Destan"],["Sosyal","Adalet"],["Bilim","Astronomi"],["Bilim","Kütleçekimi"],["Toplum","Kültür"]]$json$::jsonb) k
UNION ALL SELECT value, NULL FROM jsonb_array_elements_text($json$["Cografya"]$json$::jsonb);

CREATE TEMP TABLE level3 ON COMMIT DROP AS
SELECT v.id,
       CASE WHEN v.category IN (SELECT category FROM adult_categories) THEN 'adult'
            WHEN EXISTS (SELECT 1 FROM child_keep k WHERE k.category = v.category AND (k.word IS NULL OR k.word = v.word)) THEN 'child'
            ELSE 'teen' END AS tier
FROM speed_reading.vocabulary_items v
WHERE NOT v.is_deleted AND v.target_age_group_id IS NULL AND v.difficulty_level = 3;

DO $$ BEGIN
  IF (SELECT count(*) FROM age_fit_words) <> 25 THEN RAISE EXCEPTION 'Word payload incomplete'; END IF;
  IF to_regclass('speed_reading.vocabulary_age_fit_v2_backup') IS NOT NULL
    OR EXISTS (SELECT 1 FROM speed_reading.vocabulary_items WHERE created_by = 'vocabulary-age-fit-v2')
    THEN RAISE EXCEPTION 'vocabulary-age-fit-v2 is already applied'; END IF;
  IF (SELECT count(*) FROM speed_reading.age_group_configurations WHERE id IN (
      '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002',
      '10000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000004')) <> 4
    THEN RAISE EXCEPTION 'Age groups missing'; END IF;
  IF (SELECT count(*) FROM level3) <> 323
    OR (SELECT count(*) FROM level3 WHERE tier = 'adult') <> 154
    OR (SELECT count(*) FROM level3 WHERE tier = 'child') <> 39
    THEN RAISE EXCEPTION 'Vocabulary baseline changed; review before applying'; END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.vocabulary_items v JOIN age_fit_words w ON w.id = v.id)
    THEN RAISE EXCEPTION 'Word id collision'; END IF;
END $$;

-- Rollback record: the rows that leave the shared pool, with their previous values and their tier.
CREATE TABLE speed_reading.vocabulary_age_fit_v2_backup AS
SELECT v.id, l.tier, v.target_age_group_id, v.updated_at, v.updated_by
FROM speed_reading.vocabulary_items v JOIN level3 l ON l.id = v.id
WHERE l.tier IN ('adult', 'teen');

-- Copies: adults' terms for Genç yetişkin; the "teen" terms for Genç yetişkin and Yetişkin.
INSERT INTO speed_reading.vocabulary_items (id, word, definition, example_sentence, synonyms, antonyms,
  target_age_group_id, difficulty_level, category, is_deleted, created_at, created_by, version)
SELECT md5('vocabulary-age-fit-v2:' || c.age || ':' || v.id::text)::uuid, v.word, v.definition, v.example_sentence,
  v.synonyms, v.antonyms, c.age::uuid, v.difficulty_level, v.category, FALSE, NOW(), 'vocabulary-age-fit-v2', 0
FROM speed_reading.vocabulary_items v
JOIN speed_reading.vocabulary_age_fit_v2_backup b ON b.id = v.id
JOIN (VALUES ('adult', '10000000-0000-0000-0000-000000000004'),
             ('teen', '10000000-0000-0000-0000-000000000004'),
             ('teen', '10000000-0000-0000-0000-000000000003')) AS c(tier, age) ON c.tier = b.tier;

UPDATE speed_reading.vocabulary_items v
SET target_age_group_id = CASE b.tier WHEN 'adult' THEN '10000000-0000-0000-0000-000000000003'::uuid
                                      ELSE '10000000-0000-0000-0000-000000000002'::uuid END,
    updated_at = NOW(), updated_by = 'vocabulary-age-fit-v2', version = v.version + 1
FROM speed_reading.vocabulary_age_fit_v2_backup b
WHERE b.id = v.id;

INSERT INTO speed_reading.vocabulary_items (id, word, definition, target_age_group_id, difficulty_level, category,
  is_deleted, created_at, created_by, version)
SELECT id, word, definition, age_id, level, category, FALSE, NOW(), 'vocabulary-age-fit-v2', 0
FROM age_fit_words;

-- Every age keeps a full, unambiguous level 3 pool (words per session: Çocuk 12, Genç 14, Genç yetişkin 16, Yetişkin 18).
DO $$ DECLARE missing text; BEGIN
  SELECT string_agg(r.age_id::text, ', ') INTO missing
  FROM (VALUES ('10000000-0000-0000-0000-000000000001'::uuid, 12), ('10000000-0000-0000-0000-000000000002'::uuid, 14),
               ('10000000-0000-0000-0000-000000000004'::uuid, 16), ('10000000-0000-0000-0000-000000000003'::uuid, 18)) AS r(age_id, needed)
  WHERE (SELECT count(*) FROM (
      SELECT v.word, v.definition FROM speed_reading.vocabulary_items v
      WHERE NOT v.is_deleted AND v.difficulty_level = 3 AND (v.target_age_group_id IS NULL OR v.target_age_group_id = r.age_id)) visible
    WHERE (SELECT count(*) FROM speed_reading.vocabulary_items o
           WHERE NOT o.is_deleted AND o.difficulty_level = 3 AND (o.target_age_group_id IS NULL OR o.target_age_group_id = r.age_id)
             AND (lower(trim(o.word)) = lower(trim(visible.word)) OR lower(trim(o.definition)) = lower(trim(visible.definition)))) = 1
  ) < r.needed;
  IF missing IS NOT NULL THEN RAISE EXCEPTION 'Vocabulary pool too small after age fit: %', missing; END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.vocabulary_items v
      WHERE NOT v.is_deleted AND v.difficulty_level = 3
        AND (v.target_age_group_id IS NULL OR v.target_age_group_id = '10000000-0000-0000-0000-000000000001')
        AND v.category IN (SELECT category FROM adult_categories))
    THEN RAISE EXCEPTION 'Çocuk still sees business or economy words'; END IF;
END $$;

SELECT coalesce(target_age_group_id::text, 'ortak') AS age, count(*) AS words
FROM speed_reading.vocabulary_items
WHERE NOT is_deleted AND difficulty_level = 3
GROUP BY 1 ORDER BY 1;
COMMIT;
