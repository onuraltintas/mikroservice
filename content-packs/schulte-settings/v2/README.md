# Schulte Tablosu — yaş/seviye ayarları v2

Bu paket yalnız `settings.json` içindeki 20 mevcut eğitim egzersizinin tablo boyutunu
ve üst süre sınırını değiştirir. Kimlikler, yaş/seviye bağlantıları, program çizelgeleri,
seviye tespit ve diğer egzersizler korunur. v1 katalog dosyaları tarihsel kaynak olarak kalır.

| Yaş grubu | Seviye 1 | Seviye 2 | Seviye 3 | Seviye 4 | Seviye 5 |
|---|---|---|---|---|---|
| Çocuk 9–12 | 3×3 / 45 sn | 3×3 / 30 sn | 4×4 / 60 sn | 4×4 / 45 sn | 5×5 / 90 sn |
| Genç 13–16 | 4×4 / 60 sn | 4×4 / 45 sn | 5×5 / 90 sn | 5×5 / 60 sn | 6×6 / 120 sn |
| Genç yetişkin 17–21 | 4×4 / 60 sn | 5×5 / 90 sn | 5×5 / 60 sn | 6×6 / 120 sn | 7×7 / 150 sn |
| Yetişkin 22+ | 4×4 / 60 sn | 5×5 / 90 sn | 5×5 / 60 sn | 6×6 / 120 sn | 7×7 / 150 sn |

Süreler zorunlu çalışma süresi değil, tamamlamanın üst sınırıdır. Sayılar 1'den
tablo boyutunun karesine kadar sırayla bulunur; her yeni oturumda yerleşim karışır.
İpucu, merkez noktası ve tıklama geri bildirimi mevcut davranışla açık kalır.
Başlamış kayıtlı oturumların sunucu ayarları korunur; yeni ayarlar yeni oturumlarda kullanılır.
Özel önizlemede kullanıcı tablo boyutunu değiştirirse kayıtlı boyutu geçersiz kılabilir.

Bu süreler ürün ayarlarıdır, akademik yaş normları veya okuma hızı ölçümü değildir.
6×6–7×7 mobil okunabilirliği ve dokunma rahatlığı kullanıcı tarafından ayrıca test edilecektir.

## Uygulama

`apply.sql` tek transaction içinde kaynak kimlik/yaş/seviye ve eski ya da yeni ayarları
doğrular. Eksik, farklı veya çakışan büyük/küçük harfli ayar anahtarında durur.
Aynı sürümü tekrar uygulamak audit/version dahil değişiklik yapmaz.
Tablo kilidi ve advisory lock eşzamanlı katalog yazımıyla yarışı önler.
Yalnız değişen satırlarda `version`, `updated_at`, `updated_by` güncellenir.

Canlı geçişte aynı API konteynırı kısa süre durdurulur; yedek ve kapsam dışı tüm
tablo özetleri alınır, paket uygulanır, özet eşitliği doğrulanır, API yeniden başlatılır.
Yeni API/frontend imajı veya migration yoktur. Backup list doğrulaması tam restore tatbikatı değildir.
Uygulama yanıtı veya son kontrol belirsizse API kapalı tutulur; transaction sonucu
incelenmeden otomatik tam yedek geri yüklemesi yapılmaz.
