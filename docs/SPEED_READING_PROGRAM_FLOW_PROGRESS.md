# Hızlı Okuma program akışı — ilerleme raporu

Güncelleme: 1 Ekim 2026. Planın tamamı henüz bitmedi. Uygulama değişiklikleri canlıya alınmadı.

## Tamamlanan işler

- Kullanıcının açık onayıyla yalnız kullanılmayan mükerrer program `f035811f-31ae-4665-bc53-bb7df2d197f4` canlıdan silindi. Asıl `eaf89bb3-7427-4287-a6de-d19f49b782b6` ve öğrenci geçmişi korunuyor. Tüm tablo UUID referansları, FK'ler ve içerik eşitliği kontrol edildi. Yedek: `/var/lib/eduivme/backups/program-catalog-20260930/speedreading_owned_db.dump`.
- İlk atama yalnız aktif, silinmemiş, yaşa uygun genel eğitim programlarından; ondalık puan korunuyor, seçim deterministik ve ilgisiz fallback yok.
- 18 eğitim programının 490 günü kapasite kontrolünden geçti. Dört yaş grubu için okuma metni/soru bağımlılıkları kontrol edildi. Bu, pedagojik kalite veya bilimsel etkinlik onayı değildir.
- Atama takvimi sabit; boş gün ve yetersiz egzersiz atamayı engelliyor. Gerçek program adı ve süresi arayüzde kullanılıyor.
- Öğrenci menüsünde ödev, tekrar ve öğrenme yolu erişimleri var. Assessment API hatası eğitim erişimini açmıyor.
- Son gün tamamlaması idempotent. Günlük ilerleme hatasında görünür uyarı ve aynı session anahtarıyla retry var; Review günlük eğitimi tamamlamıyor.
- Paralel son egzersizler öğrenci bazlı PostgreSQL advisory lock/transaction ile seri işleniyor. Retry sırasında önceki izlenen değişiklikler temizleniyor.
- Tamamlanma tarihi ve özet ana sayfada korunuyor. Sonraki ölçüm aşaması sunucudan okunuyor; hata durumunda tahmin edilmiyor.
- Her eğitim sonrası ölçüm kendi program ilerlemesine bağlanıyor. Eski program sonucu yeni programın ölçümünü açmıyor; Retention/Transfer aynı döngüdeki önkoşulları arıyor.
- Nullable `program_progress_id` migration'ı geçmişi silmiyor veya tahminle bağlamıyor. Baseline ve program döngüsü için ayrı unique index'ler var. İleri/geri SQL gerçek PostgreSQL'de doğrulandı; canlıda uygulanmadı.
- Ölçüm başlatma ve program atama aynı öğrenci kilidini kullanıyor; yarış durumu PostgreSQL testinde yeniden üretildi ve kapatıldı.
- Eğitim sonrası sunucu ölçümüyle yaşa uygun genel program öneriliyor. Otomatik başlamıyor: bireysel kullanıcı açıkça onaylıyor; öğretmen/kurum bağlantılı öğrenci için yetkili onayı gerekiyor.
- Onay kaynak program/ölçüm kimliğini kilit altında tekrar doğruluyor. Tek aktif program ve idempotent tekrar korunuyor; onaylayan aktör CreatedBy alanına kaydediliyor.
- Öğrenci ana sayfasında öneri/onay; öğretmen/kurum öğrenci raporunda öneriyi inceleme/onay bölümü var. Öğrenci sahipliği ve kurum kapsamı sunucuda kontrol ediliyor.
- Eksik/pasif öneri onayında 500 yerine 404 var. 39,99 gibi puanlar görüntüleme yuvarlamasından etkilenmeden paket seçimine giriyor.
- Eski başlatma endpoint'i ilk seviye tespitini veya yeni açık onayı atlayamıyor; mevcut aktif programın zararsız tekrarı korunuyor.
- Çift rollü hesaplarda kişisel içerik, seri, ödev ve gamification için öğrenci aboneliği kontrol ediliyor; staff yönetimi ayrı tutuluyor.
- Student+Admin/SystemAdmin/Editor hesaplarının onboarding/önizleme yönlendirme döngüsü kapatıldı.
- Ek kullanıcı talebi: yalnız öğrenci rolü olmayan admin/öğretmen için ayrı Eğitim Programları sekmesi, serbest program seçimi ve kalıcı eğitim kaydı eklendi. Günün tüm egzersizleri bitince takvim beklemeden sonraki gün açılır; program bitince tekrar kayıt yapılabilir. Normal öğrenci kuralları korunur. Ayrıntılar: [staff eğitim akışı](SPEED_READING_STAFF_TRAINING.md).

## Son doğrulama

- Backend birim testleri: **760 geçti**, başarısız/atlanan yok.
- İlgili middleware ve gerçek PostgreSQL testleri: **39 geçti**, başarısız/atlanan yok.
- Öğrenci arayüzü: **379 geçti**.
- Öğretmen/kurum paneli: **226 geçti**, 41 test dosyası.
- API Release ve iki frontend production derlemesi başarılı. Son middleware/onboarding düzenlemesinden sonra API ve öğrenci arayüzü yeniden derlendi.
- Son kapsamlı kod incelemesinde açık bulgu kalmadı; inceleyen ajan ayrıca hedefli 12 backend ve tüm 226 staff panel testini doğruladı.
- Mevcut SCSS bütçe uyarıları: öğrenci player 128,02/120 kB; staff app 14,38/14 kB. İlgili SCSS değiştirilmedi.
- Kod kapsamı yüzdesi ölçülmedi. Testlerin geçmesi ürünün yüzde yüz tamamlandığı anlamına gelmez.

## Kalan işler

1. Katalog açıklamalarını ve genel/sınav/kamp/maraton amaçlarının admin yönetimini gerçek içerikle eşleştirmek.
2. Yeni program onayı için ayrı gerçek PostgreSQL eşzamanlılık ve HTTP yetkilendirme senaryoları.
3. Admin program yaşam döngüsü yönetimi ve mevcut yanlış atamaları kontrollü inceleme.
4. Öğrenci, öğretmen, kurum ve çift rol tam tarayıcı yolculukları: tamamla → ölç → öneri → onay → yeni aktif program.
5. Motor konfigürasyonu/içerik bütünlüğü ve bilimsel/pedagojik kaliteyi ayrı değerlendirmek.
6. Son toplu doğrulama ve rapor; uygulama canlıya alınmadan kullanıcı bilgilendirilecek.

## Kapsam koruması

Önceden yaklaşık 500 dosya değişikliği var; yalnız bu çalışmanın dosya/hunk'ları commit ediliyor. Koçluk ve Identity veritabanlarına dokunulmadı. Canlıdaki tek değişiklik açıkça onaylanan mükerrer program silmesidir.
