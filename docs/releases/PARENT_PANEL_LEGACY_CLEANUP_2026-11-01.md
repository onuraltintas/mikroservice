# Veli paneli — eski veli alanlarının kaldırılması (V6a) — 01.11.2026

Kapsam: `services/identity-service` (migration `20261104090000_RemoveLegacyParentFields`),
`content-packs/parent-weekly-summary-mute/v1`. Kullanıcı kararları 01.11.2026.

## Davranış değişiklikleri

| Alan | Önce | Sonra |
| --- | --- | --- |
| Haftalık veli özeti anahtarı | İki anahtar: Identity `parent_profiles.NotificationPreferences` (`weeklyProgress`) ve notification-service kategorisi `ParentSummary` | Yalnız `ParentSummary` (veli panelinde "Bildirim tercihleri"). Identity alıcı listesi artık veliyi kendi anahtarıyla elemez; sayfalama ve toplam yine SQL'de |
| `StudentProfile.ParentId` | Doğrulanmamış eski veli alanı (FK, index); hiçbir yetkide kullanılmıyordu | Kaldırıldı. Veli–öğrenci bağı yalnız `ParentStudentRelationship` (doğrulanmış bağlantılar etkilenmez) |
| Veli mesajlaşması | — | Değişmedi (karar): öğrenci Koçluk ilerlemesini veliden gizlese de geçerli veli bağlantısı olan veli çocuğun öğretmenleriyle yazışmaya devam eder; bağlantı kaldırılınca yazışma kapanır (test: `CoachingMessagingPostgresTests`) |

## Migration

Up, sütunları silmeden önce değerleri iki yedek tabloya yazar:

- `identity.legacy_parent_notification_preferences_20261101` — varsayılandan farklı her `NotificationPreferences` değeri;
- `identity.legacy_student_parent_ids_20261101` — boş olmayan her `(student_user_id, parent_id)` çifti.

Sonra FK `FK_student_profiles_users_ParentId`, index `IX_student_profiles_ParentId` ve iki sütun silinir. Down sütunları
eski varsayılanla geri ekler, değerleri yedekten geri yazar (silinmiş veli hesabına işaret eden `ParentId` boş kalır) ve
yedek tabloları siler.

## Yayından önce (operatör)

1. Veritabanı yedeği (docs/DB_BACKUP_RUNBOOK.md).
2. Identity veritabanında salt okunur kontrol: `content-packs/parent-weekly-summary-mute/v1/check.sql`
   → çıktı `<sayı>|<veli kullanıcı id'leri>`.
3. **Sayı 0'dan büyükse:** bu veliler haftalık özeti Identity'de kapatmıştı; yayından sonra e-posta almaya başlamamaları
   için aynı id'lerle Notification veritabanında `apply-notification.sql` çalıştırılır (tek transaction, sayılar kontrol
   edilir; yalnız `ParentSummary` e-posta kanalı kapanır). Komutlar paketin README'sinde.
4. Sayı 0 ise yapılacak bir şey yok.

Kontrol migration'dan sonra da çalışır (yedek tablodan okur); yine de yayından önce çalıştırılması önerilir.
