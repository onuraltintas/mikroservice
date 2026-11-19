# Göz Takibi v2 — kısa ve sabit süre

Kullanıcı kararı (19.11.2026): Göz Takibi her seviyede **Çocuk için 45 saniye, diğer yaşlar için 60 saniye** sürer.

**Gerekçe:** v1'de süre seviyeyle birlikte uzuyordu: Çocukta 45–105 sn, Yetişkinde 90–150 sn. Egzersiz yalnız hareket eden bir noktayı izlemekten ibaret; uzun süre zorluk katmıyor, dikkati düşürüyor. Seviye farkı artık yalnız yol ve tempo ile verilir.

Bu paket yalnız 20 normal Göz Takibi kaydının `engineConfig.timing.durationSeconds` alanını değiştirir. Şunlara dokunmaz:
- yol ve tempo (bkz. aşağı)
- nokta boyutu
- seviye tespiti kayıtları
- program atamaları, oturumlar ve sonuçlar
- Sakkad ve Sabitleme

| Yaş grubu | Süre (her seviye) | Yol (S1 → S5) | Tur süresi (S1 → S5, sn) |
|---|---|---|---|
| Çocuk (9–12) | 45 sn | yatay, daire, daire, sekiz, sekiz | 4,0 / 3,8 / 3,6 / 3,4 / 3,2 |
| Genç (13–16) | 60 sn | aynı | 3,6 / 3,4 / 3,2 / 3,0 / 2,8 |
| Genç yetişkin (17–21) | 60 sn | aynı | 3,4 / 3,2 / 3,0 / 2,8 / 2,6 |
| Yetişkin (22+) | 60 sn | aynı | 3,2 / 3,0 / 2,8 / 2,6 / 2,4 |

Bir programdaki 6 Göz Takibinin toplamı: v1'de yaş ve seviyeye göre 4,5–15 dakikaydı; v2'de Çocukta 4,5, diğer yaşlarda 6 dakika. Çocuk Seviye 1'in süresi zaten 45 saniye olduğu için o kayıt değişmez (19 kayıt güncellenir).

Bu değerler ürün ayarıdır, bilimsel yaş normu değildir.

## Uygulama (yalnız Hızlı Okuma veritabanı)
1. `node content-packs/eye-tracking/v2/generate.mjs` ve `node --test content-packs/eye-tracking/v2/pack.test.mjs`.
2. Hızlı Okuma veritabanının yedeğini al.
3. `psql -X -v ON_ERROR_STOP=1 -f apply.sql` çalıştır. API'yi durdurmak gerekmez: kayıtlar tek işlemde değişir, açık oturumlar kendi ayar kopyalarıyla devam eder. Yeni ayar yeni oturumlarda geçer.
4. Betik önce denetler; şunlardan biri varsa hiçbir şeyi değiştirmeden durur:
   - kayıt eksik ya da mükerrer;
   - yaş ya da seviye uyuşmuyor;
   - kayıt v1 ayarından farklı (yönetici sonradan değiştirmiş).
5. Tekrar çalıştırmak bir şey değiştirmez.

**Geri dönüş:** `rollback.sql` 20 kaydı v1 sürelerine döndürür. Kayıt v1 ya da v2 ayarında değilse durur. Hiçbir geçmiş silinmez.

## Doğrulama (19.11.2026, yerel PostgreSQL 16)
- Uygulama: 5 kayıt 45 sn, 15 kayıt 60 sn.
- Tekrar uygulama: değişiklik yok, sürüm artmadı.
- Geri dönüş: v1 süreleri geri geldi.
- Yönetici değişikliği olan bir kayıtta betik "baseline changed" ile durdu; hiçbir kayıt değişmedi.
