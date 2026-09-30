# Hızlı Okuma program akışı düzeltmeleri

Başlangıç: 30 Eylül 2026. Planın tamamı henüz bitmedi. Uygulama sürümü canlıya alınmadı.

## Doğrulanan işler

- Kullanıcının açık onayıyla VPS `speedreading_owned_db` içinde yalnız kullanılmayan `f035811f-31ae-4665-bc53-bb7df2d197f4` programı kalıcı silindi. Tüm `speed_reading` tablolarında satır JSON'unda UUID referansı tarandı, FK'ler incelendi, diğer kayıtla içerik eşitliği doğrulandı. Asıl `eaf89bb3-7427-4287-a6de-d19f49b782b6` programı ve bir öğrenci ilerleme kaydı korunuyor. Yedek: `/var/lib/eduivme/backups/program-catalog-20260930/speedreading_owned_db.dump`.
- İlk otomatik atama yalnız `ProgramType=0`, sınav türü boş, aktif/silinmemiş ve yaşa uygun genel eğitim programlarından yapılır. Ondalıklı puan kesilmez. Ortak eşik puanında yüksek alt sınıra sahip program seçilir; ardından DisplayOrder ve Id ile deterministik sıralama yapılır. Uygun aday yoksa ilgisiz fallback yoktur. Mevcut atamalar değiştirilmedi.
- 18 aktif eğitim programının 490 günü salt okunur kapasite kontrolünden geçti. Boş gün/yetersiz tür-yaş-zorluk kapasitesi yok. Aktif okuma motorlarına dört gerçek yaş grubu ile uygun metin ve gerekli A/B/C/D cevaplı soru varlığı kontrol edildi; eksik bağımlılık 0. Bu denetim metinlerin pedagojik kalitesinin veya bilimsel etkinliğinin onayı değildir.
- Mevcut atama takviminin değişmezliği, boş gün ve yetersiz egzersiz durumunda atamanın engellenmesi test edildi.
- Öğrenci menüsüne ödevler, tekrar egzersizleri ve kişisel öğrenme yolu eklendi.
- Assessment durum API hatası eğitim erişimini açmaz; hata sayfasına yönlendirilir.
- Öğrenci program ilerlemesi DTO'suna gerçek program adı ve sabit atama takviminden gün/hafta sayısı eklendi. Frontend boş ad/0 süre üretmiyor.
- Tamamlanmış veya pasife alınmış programın tekrar tamamlanması sayaçları değiştirmez; program bitiminde son gün aşılmaz.
- İlk eğitim sonrası ölçümü başlatmak için tamamlanmış program gerekiyor. Hem aşama planı hem başlatma servisi kontrol ediyor. Retention/Transfer süreleri korunuyor.
- Player günlük ilerleme hatası görünür uyarı ve ayrı retry sunar; aynı session idempotency anahtarı kullanılır. Review kaydı günlük programa yanlışlıkla yazılmaz.
- Program tamamlandı popup'ındaki tüm seviyelerin bittiği/usta okuyucu iddiası kaldırıldı. Eğitim sonrası ölçüm bağlantısı eklendi.
- Çift öğrenci/öğretmen rollü hesaplarda öğrenci profil/ölçüm/abonelik önkoşulları uygulanır. Sunucu kişisel eğitim endpoint'lerinde öğrenci aboneliğini kontrol eder; öğretmen yönetimi staff yetkileriyle korunur.

## Test kanıtı

- Backend tüm `SpeedReading.Application.UnitTests`: 733 geçti, 0 başarısız, 0 atlanan (frontend son düzeltmelerinden önce; backend aynı).
- Hedefli backend son çalışma: 15 geçti.
- Hedefli Angular son çalışma: 10 geçti.
- Docker kullanıcı tarafından açıldı ve erişim doğrulandı: 29.8.0.
- Geniş Angular son çalışma: 362 geçti, 0 başarısız.
- İlgili API/middleware testleri: 14 geçti.
- Docker Testcontainers gerçek PostgreSQL atama sorgusu: 1 geçti (geçici veritabanı; canlıya dokunulmadı).
- Hızlı Okuma API Release build: başarılı, 0 hata, 0 uyarı.
- Angular production build başarılı; mevcut player SCSS bütçe uyarısı (128.02 kB / 120 kB) var. Bu görevde SCSS değiştirilmedi.

## Sırayla tamamlanacak işler

1. Katalog açıklamalarının gerçek içerik sayılarıyla uyumlandırılması; genel/sınav/kamp/maraton sınıflarının yönetim ekranında doğrulanması. Şimdiki veri kapasitesi kontrolü motor içi içerik kalitesi/konfigürasyon bütünlüğü testinin yerine geçmez.
2. Çift rol önkoşul/abonelik düzeltmesinin tam tarayıcı yolculuğuyla doğrulanması; öğrenci+admin/editor persona/onboarding yönlendirmelerinin ayrıca kontrolü.
3. Öğrenci yeniden giriş yaptığında erişilebilir, kalıcı program bitiş özeti ve sıradaki iş çağrısı.
4. Her biten programı ilgili eğitim sonrası ölçüm döngüsüne bağlama. Şimdiki gate en az bir tamamlanmış program arar; önceki programın tamamlanması sonraki aktif program için yeterli kabul edilmemeli. Geçmiş ölçüm sonuçları yanlış döngüye öneri oluşturmamalı.
5. Geçerli sunucu ölçümleriyle sonraki program önerisi; bireysel kullanıcı onayı, öğretmen/kurum atamalı öğrencide yetkili onayı; tek aktif program, tekrarlanan başlatma idempotency'si; eski sonuçların korunması. Rastgele bilimsel eşik üretme yok.
6. Admin/öğretmen program amaç/yaş/eşik/katalog doğrulaması, tamamlanan/önerilen program yönetimi ve mevcut yanlış atamaları kontrollü inceleme.
7. Gerçek PostgreSQL entegrasyon testleri, tüm frontend testleri, production build ve uçtan uca kullanıcı/kurum/çift rol/tamamla-ölç-yeni program senaryoları.
8. Son rapor; uygulama canlıya alınmadan kullanıcı bilgilendirilecek. Şu an yalnız açık onaylı mükerrer veri silmesi canlıda uygulandı.

## Kapsam koruması

Çalışma ağacında önceki yaklaşık 500 dosya değişikliği var; bunlar bu görevin commit'lerine topluca dahil edilmez. Navigation dosyasındaki önceden mevcut eski öğretmen/kurum menüsü kaldırmaları korunmuş ve yalnız bu görevde eklenen öğrenci menü hunk'ı commit edilmiştir. Koçluk/Identity veritabanına dokunulmadı.
