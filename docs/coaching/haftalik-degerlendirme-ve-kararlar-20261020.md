# Haftalık değerlendirme ve karar takibi

20 Ekim 2026. Geliştirme, yerel doğrulama ve canlı dağıtım tamamlandı. Gerçek hesaplarla test ve veri koruma sonuçları: [canlı doğrulama raporu](haftalik-degerlendirme-canli-20261020.md).

## Kullanım

Öğrenci, Gelişim > Çalışma raporu bölümünden **Haftalık değerlendirme ve kararlar** alanını açar. Öğretmen aynı alanı yetkili olduğu öğrencinin gelişim ekranında görür. Bölüm ilk açıldığında yüklenir; diğer gelişim sekmelerine geçince form örneği ve girilmiş metin korunur.

Varsayılan olarak son tamamlanan hafta açılır. Haftalar Türkiye saatine göre pazartesi–pazardır. Önceki haftalar seçilebilir; devam eden hafta seçilirse henüz tamamlanmadığı belirtilir. Gelecek haftanın değerlendirmesi oluşturulamaz. Hafta seçimi genel gelişim raporunun tarih aralığından bağımsızdır.

Öğrenci şu bilgileri kaydeder:

- İyi giden çalışmalar: zorunlu, en fazla 2.000 karakter.
- Zorlandığı noktalar: isteğe bağlı, en fazla 2.000 karakter.
- Gelecek hafta odağı: zorunlu, en fazla 2.000 karakter.
- Çalışma düzeni değerlendirmesi: isteğe bağlı, 1–5; öğrencinin kendi beyanıdır.

Aynı öğrenci ve hafta için tek değerlendirme vardır; sonradan düzenlenebilir. Öğretmen öğrencinin değerlendirmesini okuyabilir, öğrenci adına değiştiremez. Ekranda seçilen haftanın planlanan/tamamlanan çalışma sayısı ile planlanan ve kaydedilen süreleri de gösterilir. Aktif plan çalışmaları ve arşivlenmiş planlardaki tamamlanan çalışmalar, mevcut çalışma raporu hesaplama kurallarıyla dikkate alınır. Kayıtlı gerçek süre yoksa sıfır varsayılmaz.

Öğretmen **Karar ekle** ile sonraki hafta için başlık, açıklama ve son tarih kaydeder. Başlık zorunludur ve 200 karakterle, açıklama 2.000 karakterle sınırlıdır. Son tarih, değerlendirilen haftayı izleyen pazartesi–pazar aralığında olmalıdır. Yeni karar Planlandı durumuyla başlar.

Kararı oluşturan ve öğrenciye erişimi devam eden öğretmen durumunu Planlandı, Devam ediyor, Tamamlandı veya İptal olarak değiştirebilir. Tamamlama zamanı ve her değişiklikte başlık, açıklama, son tarih ve durumun o andaki hali geçmişte saklanır. Tamamlanan karar yeniden açılabilir; önceki tamamlanma kararı geçmişte korunur.

Seçili haftanın kararlarının yanında önceki haftalardan açık kalan kararlar da gösterilir. Son tarihi geçen açık karar işaretlenir. Liste 20 karar üzerinden sayfalanır. Eski bir açık karar düzenlenirken tarih sınırı, kararın ait olduğu özgün haftaya göre korunur. Tamamlanan/iptal edilen eski kararlar kendi haftalarından görülebilir. Öğrenci kararları ve geçmişini okuyabilir; öğretmen kararlarının durumunu değiştiremez.

Kararlar planı yeniden dağıtmaz, ders saatlerini değiştirmez ve ödev oluşturmaz. Bu pakette yeni bildirim türü veya otomatik hatırlatıcı eklenmedi.

## Kayıt, yetki ve veri koruması

- Öğrenci yalnız kendi aktif koçluk profiliyle işlem yapabilir. Öğretmen erişimi aktif atama, hesap/profil uygunluğu ve öğrencinin öğretmenle gelişim paylaşma izni üzerinden denetlenir. Kurum yetkilisinin mevcut kapsamlı okuma erişimi korunur; karar yazımı öğretmene aittir.
- `expectedVersion` ile eski formdan yapılan güncellemeler 409 olarak reddedilir. Arayüz bu durumda girilen metni korur, güncel kayıt yüklenmeden tekrar yazmayı engeller. Yenileme veya sayfadan ayrılma sırasında kaydedilmemiş form için uyarır; kayıt isteği sürerken ayrılmayı engeller.
- Öğrenci değişince önceki istekler ve ekran verisi temizlenir; eski bir yanıt yeni öğrencinin ekranına yazılmaz. Yetki kaybında korunan ekran verisi kaldırılır.
- Oluşturma isteklerinin güvenli tekrarı için değerlendirmede öğrenci/hafta benzersizliği, kararda istemcinin ürettiği sabit kimlik kullanılır. Aynı içerikli oluşturma tekrarı yeni kayıt üretmez.
- Yazımlar, mevcut öğrenci çalışma planı kilidiyle ve işlem içinde yapılır. Kilitten sonra yetki yeniden doğrulanır.
- Hesap verisi dışa aktarımına ve mevcut kişisel veri silme işlemlerine yeni kayıtlar dahil edildi. Dışa aktarım şeması 1.6 oldu. Normal haftalık iş akışında kayıt silme yoktur.

## Veritabanı

`20261020130142_WeeklyReviewsAndDecisions` geçişi yalnız aşağıdaki tabloları ve indekslerini ekler; eski tablo veya sütunları değiştirmez.

| Tablo | Temel alanlar | Kısıtlar |
|---|---|---|
| `coaching.weekly_reflections` | Id, StudentId, WeekStart, Successes, Obstacles, NextFocus, Rating, Version, oluşturma/güncelleme alanları | Öğrenci + hafta benzersiz; pazartesi kontrolü; puan 1–5 veya boş |
| `coaching.weekly_decisions` | Id, StudentId, TeacherId, ReviewWeekStart, Title, Note, DueOn, Status, CompletedAt, HistoryJson, Version, oluşturma/güncelleme alanları | Pazartesi ve sonraki hafta tarih aralığı; geçerli durum; tamamlanma tarihi/durum tutarlılığı |

Kimlikler ortak hesap `UserId` standardındadır. Geçmiş JSONB olarak saklanır. Öğrenci/hafta/durum ile öğretmen indeksleri vardır. Mevcut plan, görev, kazanım bağlantısı, ödev ve hesap kayıtlarına veri dönüşümü uygulanmaz. Geçişin geri dönüşü yeni tabloları kaldırır; haftalık kayıt oluşmuşsa geri dönüşten önce bunlar ayrıca yedeklenmelidir.

## API ve arayüz

| İşlem | Yol |
|---|---|
| Öğrenci haftasını oku | `GET /api/coaching/study-planning/weekly-review?weekStart=YYYY-MM-DD&decisionPage=1` |
| Öğrenci değerlendirmesini kaydet | `PUT /api/coaching/study-planning/weekly-review/{weekStart}` |
| Yetkili öğrencinin haftasını oku | `GET /api/coaching/staff/students/{studentId}/study/weekly-review?weekStart=YYYY-MM-DD&decisionPage=1` |
| Öğretmen kararını oluştur/güncelle | `PUT /api/coaching/staff/students/{studentId}/study/weekly-review/{weekStart}/decisions/{id}` |

Geçersiz alanlar 400, yetkisiz erişim 403, sürüm çakışması 409 döndürür. Mevcut yanıt zarfı kullanılır. Ortak `WeeklyReviewComponent`, öğrenci ve öğretmenin kullandığı `PracticeProgressComponent` içindeki Çalışma raporu paneline ertelenmiş yüklemeyle eklendi. Öğretmen ekranında tekrar form veya iş kuralı kopyalanmadı.

## Doğrulama

- Sunucu/gerçek yerel PostgreSQL regresyonu: **56 başarılı, 0 başarısız, 0 atlanan**. Haftalık kaydetme/düzenleme, güvenli tekrar, eski sürüm, farklı öğrenci/öğretmen yetkisi, paylaşım izninin kaldırılması, karar tamamlama/yeniden açma/geçmiş, eski açık kararlar, sayfalama, geçersiz tarihler ve API durum kodları sınandı. Mevcut plan, tamamlanan görev ve kazanım bağlantılarının görüntüleri karşılaştırıldı. Geçiş ileri/geri çalıştırıldı; modelde bekleyen değişiklik yok. Dışa aktarım ve öğrenci/öğretmen hesabı silme kapsamı da sınandı.
- Öğrenci arayüzü regresyonu: **97 test başarılı**.
- Öğretmen arayüzü regresyonu: **85 test başarılı**. Ortak haftalık bileşen, API istemcisi ve gelişim ekranı testleri personel derlemesinde de çalıştırılır.
- Öğrenci ve öğretmen üretim derlemeleri başarılı. İlk paketler sırasıyla **813,08 kB** ve **991,24 kB**. Öğretmen paketi mevcut 1 MB hata sınırının altında. Önceden bulunan stil bütçesi ve şablon uyarıları devam ediyor.
- Gerçek bileşenin örnek verili yerel tarayıcı önizlemesinde öğrenci değerlendirmesinin kaydı, öğretmende okunması, sonraki hafta kararı oluşturma, tamamlandı durumuna geçiş ve öğrencide görünmesi doğrulandı. Bu önizleme canlı servis bağlantısı kullanmaz; gerçek veritabanı davranışı PostgreSQL testlerinde sınandı.
- Ek regresyonda bulunan, hafta değişimi iptal edildiğinde tarih kutusunun yeni tarihi göstermesi hatası düzeltildi. Formlar ve görünüm aynı haftada kalır.

Test komutları: `node tools/test-coaching-development.mjs`; PostgreSQL için `COACHING_CURRICULUM_TEST_POSTGRES` yalnız localhost test veritabanını göstermelidir. CI koçluk regresyonuna haftalık testler eklendi. Sonuçlar `artifacts/coaching-weekly-regression.log`, `artifacts/coaching-weekly-ui-tests-final.log`, üretim derleme günlükleri ve `weekly-review-regression.trx` dosyasında bulunur.

Yerel görseller: `artifacts/coaching-weekly-student.png`, `artifacts/coaching-weekly-decisions.png`. Coaching servisi, iki portal ve eklemeli geçiş aynı gün birlikte yayımlandı; öğrenci/öğretmen akışı canlı oturumlarda da doğrulandı. Canlı kanıtlar `artifacts/coaching-weekly-live-20261020/` dizinindedir.
