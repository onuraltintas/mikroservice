-- Newsletter consent texts in force (user decision 14.11.2026, option B).
-- The two consent pages published on 05.10.2026 for the adult test period lose the test-period title, the "18 yaş ve üzeri test
-- katılımcıları" line and the whole closing paragraph about the test period; every other sentence is kept word for word.
-- Run:  psql -v ON_ERROR_STOP=1 -v publish_day='<gün Ay yıl>' -f publish-newsletter-consent-20261114.sql   (identity database)
-- Safe to run twice: a page that already carries the new text is left alone. A page edited on the server stops the script.
\set ON_ERROR_STOP on
BEGIN;
CREATE TEMP TABLE final_consent(slug text PRIMARY KEY, title text NOT NULL, content text NOT NULL, previous_md5 text NOT NULL) ON COMMIT DROP;
INSERT INTO final_consent VALUES
('coaching-newsletter-consent','Koçluk bülten onayı',replace($legal$__YAYIN_GÜNÜ__ — Koçluk bülten onayı

E-posta adresimin ONAL YAZILIM OTOMASYON EĞİTİM DANIŞMANLIK SAN. TİC. LTD. ŞTİ. tarafından Koçluk bülteni kapsamında eğitim içerikleri, hizmet duyuruları ve kampanya bilgileri göndermek amacıyla kullanılmasına ve bu kapsamda e-posta almaya isteğe bağlı olarak izin veriyorum.

Bu izin hesap açma, giriş yapma veya hizmet kullanma şartı değildir. Hızlı Okuma bültenine otomatik abonelik sağlamaz; SMS veya telefon araması izni içermez. Abonelik e-posta adresi doğrulamasıyla tamamlanır. İznimi e-postadaki abonelikten çıkma bağlantısıyla veya info@onalotomasyon.com adresine başvurarak geri alabilirim; geri alma hizmet erişimimi etkilemez.

Kişisel verilerin işlenmesine ilişkin açıklamalar ortak Gizlilik Politikası ve KVKK Aydınlatma Metninde yer alır. Bu seçim genel bir kişisel veri işleme veya yurt dışı aktarım rızası değildir.

İletişim: info@onalotomasyon.com · 0505 707 20 05.$legal$, '__YAYIN_GÜNÜ__', :'publish_day'),'61157804758becbce5f4dbc7a3473d3e'),
('speed-reading-newsletter-consent','Hızlı Okuma bülten onayı',replace($legal$__YAYIN_GÜNÜ__ — Hızlı Okuma bülten onayı

E-posta adresimin ONAL YAZILIM OTOMASYON EĞİTİM DANIŞMANLIK SAN. TİC. LTD. ŞTİ. tarafından Hızlı Okuma bülteni kapsamında eğitim içerikleri, hizmet duyuruları ve kampanya bilgileri göndermek amacıyla kullanılmasına ve bu kapsamda e-posta almaya isteğe bağlı olarak izin veriyorum.

Bu izin hesap açma, giriş yapma veya hizmet kullanma şartı değildir. Koçluk bültenine otomatik abonelik sağlamaz; SMS veya telefon araması izni içermez. Abonelik e-posta adresi doğrulamasıyla tamamlanır. İznimi e-postadaki abonelikten çıkma bağlantısıyla veya info@onalotomasyon.com adresine başvurarak geri alabilirim; geri alma hizmet erişimimi etkilemez.

Kişisel verilerin işlenmesine ilişkin açıklamalar ortak Gizlilik Politikası ve KVKK Aydınlatma Metninde yer alır. Bu seçim genel bir kişisel veri işleme veya yurt dışı aktarım rızası değildir.

İletişim: info@onalotomasyon.com · 0505 707 20 05.$legal$, '__YAYIN_GÜNÜ__', :'publish_day'),'0faea8be281222b621770449b5e138aa');

DO $$
DECLARE problems text;
BEGIN
 IF EXISTS (SELECT 1 FROM final_consent WHERE content !~ '^[0-9]{1,2} (Ocak|Şubat|Mart|Nisan|Mayıs|Haziran|Temmuz|Ağustos|Eylül|Ekim|Kasım|Aralık) 20[0-9]{2} — ') THEN
   RAISE EXCEPTION 'Set publish_day to the publish day (psql -v publish_day=''14 Kasım 2026''). Nothing changed.';
 END IF;
 IF (SELECT count(*) FROM identity."users" WHERE "Email"='admin@eduivme.com') <> 1 THEN
   RAISE EXCEPTION 'Expected existing admin actor not found';
 END IF;
 -- Each page must be live exactly as published on 05.10.2026, or already carry the new text (a rerun; the date line may differ).
 SELECT string_agg(c.slug, ', ' ORDER BY c.slug) INTO problems FROM final_consent c
 WHERE NOT EXISTS (SELECT 1 FROM identity."PlatformLegalPages" p WHERE p."Slug"=c.slug AND p."ArchivedAt" IS NULL
     AND (md5(p."Content")=c.previous_md5
       OR substr(p."Content", strpos(p."Content", E'\n')+1)=substr(c.content, strpos(c.content, E'\n')+1)));
 IF problems IS NOT NULL THEN
   RAISE EXCEPTION 'These pages differ from the 05.10.2026 texts (missing, archived or edited on the server): %. Merge the edit by hand. Nothing changed.', problems;
 END IF;
END $$;

INSERT INTO identity."PlatformLegalPageRevisions" ("Id","PageId","Slug","Title","Content","IsPublished","IsArchived","Version","CreatedBy","CreatedAt")
SELECT gen_random_uuid(), p."Id",p."Slug",p."Title",p."Content",p."IsPublished",false,p."Version",coalesce(p."UpdatedBy",p."CreatedBy"),coalesce(p."UpdatedAt",p."CreatedAt")
FROM identity."PlatformLegalPages" p JOIN final_consent c ON c.slug=p."Slug"
WHERE md5(p."Content")=c.previous_md5;

UPDATE identity."PlatformLegalPages" p SET "Title"=c.title, "Content"=c.content, "IsPublished"=true, "Version"=p."Version"+1,
 "UpdatedBy"=(SELECT "Id" FROM identity."users" WHERE "Email"='admin@eduivme.com'), "UpdatedAt"=now()
FROM final_consent c
WHERE c.slug=p."Slug" AND md5(p."Content")=c.previous_md5;

SELECT "Slug","Version","IsPublished","Title",left("Content",40) AS first_line,length("Content") AS chars FROM identity."PlatformLegalPages"
WHERE "Slug" IN (SELECT slug FROM final_consent) ORDER BY "Slug";
COMMIT;
