\set ON_ERROR_STOP on
BEGIN;
LOCK TABLE coaching.subscription_plans IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE requested_plans (slug text, name text, description text, audience text, price numeric, contact boolean, features jsonb, position integer);
INSERT INTO requested_plans VALUES
('ogrenci-kocluk-1-yil', 'Öğrenci Koçluk - 1 Yıl', 'Öğrenciler için 365 günlük Koçluk platformu araçlarına erişim. Kişisel koçluk hizmeti içermez; otomatik yenilenmez.', 'Individual', 499, false, '["365 gün platform erişimi", "Ödev ve hedef takibi", "Seans ve çalışma takvimi", "İlerleme raporları"]', 1),
('ogretmen-kocluk-1-yil', 'Öğretmen Koçluk - 1 Yıl', 'Bağımsız öğretmenler için 365 günlük Koçluk platformu. Fiyat ve öğrenci kontenjanı teklif ile belirlenir; otomatik yenilenmez. İletişim: info@onalotomasyon.com / 0505 707 20 05.', 'Teacher', 0, true, '["365 gün platform erişimi", "Öğrenci yönetimi", "Ödev ve seans yönetimi", "Öğrenci raporları", "Öğrenci kontenjanı teklif ile belirlenir"]', 2),
('kurumsal-kocluk-1-yil', 'Kurumsal Koçluk - 1 Yıl', 'Kurumlar için 365 günlük Koçluk platformu. Fiyat ve öğrenci kontenjanı teklif ile belirlenir; otomatik yenilenmez. İletişim: info@onalotomasyon.com / 0505 707 20 05.', 'Institution', 0, true, '["365 gün platform erişimi", "Kurum çapında öğrenci ve öğretmen yönetimi", "Öğretmen-öğrenci atamaları", "Kurum ve öğrenci raporları", "Öğrenci kontenjanı teklif ile belirlenir"]', 3);
INSERT INTO coaching.subscription_plans
("Id", "Slug", "Name", "Description", "Audience", "Price", "IsContactOnly", "BillingPeriod", "DurationDays", "IncludedStudentSeats", "FeaturesJson", "IsActive", "IsPublic", "SortOrder", "CreatedBy", "CreatedAt")
SELECT gen_random_uuid(), slug, name, description, audience, price, contact, 'OneTime', 365, NULL, features, true, true, position, :'actor_id'::uuid, now()
FROM requested_plans
ON CONFLICT ("Slug") DO NOTHING;
DO $$ BEGIN
    IF (SELECT count(*) FROM requested_plans r JOIN coaching.subscription_plans p ON p."Slug" = r.slug
        WHERE p."Name" = r.name AND p."Description" = r.description AND p."Audience" = r.audience AND p."Price" = r.price
        AND p."IsContactOnly" = r.contact AND p."DurationDays" = 365 AND p."BillingPeriod" = 'OneTime'
        AND p."IncludedStudentSeats" IS NULL AND p."IsActive" AND p."IsPublic" AND p."FeaturesJson" = r.features) <> 3 THEN
        RAISE EXCEPTION 'Plan conflict: no existing data overwritten';
    END IF;
END $$;
SELECT "Slug", "Audience", "Price", "IsContactOnly", "DurationDays", "IncludedStudentSeats" FROM coaching.subscription_plans WHERE "Slug" IN (SELECT slug FROM requested_plans) ORDER BY "SortOrder";
\if :apply
COMMIT;
\else
ROLLBACK;
\endif
