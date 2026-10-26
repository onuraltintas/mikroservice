# Kayıtlı plan takvimi: kartlar ve sağ tık işlemleri

## Kapsam
Öğrenci ve öğretmenin paylaştığı kayıtlı çalışma planı takvimi düzenlendi. İlk geliştirme kontrollerinden sonra canlı dağıtım ve gerçek hesap testleri yapıldı; ayrıntılar [canlı doğrulama raporunda](takvim-sag-tik-canli-20261021.md). Şema değişikliği yoktur.

- Zaman çizelgesi dakika başına 2 piksel; 30 dakikalık kart yaklaşık 60 piksel, 60 dakikalık kart yaklaşık 120 piksel. Sürükleme hesabı, önizleme ve dolu saate odaklanma aynı ölçeği kullanır.
- Dersler plan içindeki benzersiz ders kimliğine göre ayrı renk alır. Ayrıntı kartları aynı paleti kullanır. Ders adı, saat ve tamamlanma bilgisi renk olmadan da okunabilir.
- Boş saatte sağ tık, ilgili gün ve yarım saat başlangıcını ekleme formuna taşır. Ay görünümü gün seçer; başlangıç saati formda düzenlenir.
- Ders kartının menüsünde ayrıntı, ek çalışma, düzenleme, taşıma, silme ve öğrenci için tamamlama bulunur. Dokunmatik kullanımda ayrı, gerçek bir ⋯ düğmesi vardır. Shift F10, yön tuşları, Home/End ve Escape desteklenir.
- Birleştirilmiş kartlarda düzenleme/silme/tamamlama için çalışılacak kazanım parçası seçilir. Tüm blok sessizce tamamlanmaz. Taşıma mevcut bitişik blok davranışını korur.
- Yeni çalışma dersi öğrencinin eğitim kataloğundan gelir. Öğretmen ilgili öğrencinin kataloğunu kullanır. Başlık konu tekrarı, soru çözümü veya deneme analizi gibi bir çalışma olabilir; ödev ve kararlar kendi kayıt ekranlarından yönetilir.
- Kontrol düğmeleri renkli ve gölgeli; ayrıntı bölümü ders rozeti, durum ve renkli kartlarla düzenlendi.

## Veri güvenliği ve API
Öğrenci: POST /api/coaching/study-planning/plans/{id}/tasks, PUT/DELETE aynı kökte /tasks/{taskId}.
Öğretmen: aynı işlemler /api/coaching/staff/students/{studentId}/study/plans kökünde.

Yeni işlemler mevcut öğrenci kilidini, atama yetkisini, beklenen sürüm kontrolünü ve işlem bütünlüğünü kullanır. Öğretmenin aktif plan değişiklikleri mevcut plan değişikliği bildirimini üretir.

Tamamlanan ve arşiv planındaki çalışmalar düzenlenemez/silinemez. Saat çakışmaları, gece yarısını aşma ve günlük toplam 24 saat sınırı sunucuda da kontrol edilir. Düzenleme kimliği, konuyu, kaynağı, planlanan soru sayısını, sabitlemeyi ve kazanım/süre sürümü bağlantılarını korur. Kazanım payı bulunan çalışmanın süresi değiştirilemez; ek süre için yeni çalışma oluşturulur. Silme yalnız seçilen bekleyen çalışma ve onun planlanan kazanım paylarını kaldırır. Katalog kazanımları ve gerçekleşen çalışma geçmişi silinmez. Bir karara bağlı çalışma önce öğretmenin bağlantıyı kaldırmasını gerektirir; karar ve geçmişi korunur.

Merkezi toaster ve merkezi onay kullanılır; tarayıcı confirm/alert yoktur. Düzenleme formunun katmanı merkezi onayın altında kalır. Hata durumunda form açık kalır. Form açıkken sayfadan ayrılma koruması ve dış yenileme koruması vardır.

## Doğrulama
- Angular: 55 test geçti (takvim, taşıma, öğrenci planı, öğretmen planı, HTTP servis yönlendirmesi).
- Yerel PostgreSQL: 17 test geçti (ekle/düzenle/sil, sürüm, çakışma, atama yetkisi, kazanım ve tamamlanan kayıt koruması, karar bağlantıları, MVC kayıt doğrulaması).
- Mevcut plan API regresyonları: 7 test geçti.
- admin-panel ve staff-portal üretim derlemeleri başarılı. Mevcut stil/paket uyarıları sürer; hata sınırı artırılmadı.
- Gerçek bileşen ve örnek servislerle yerel Chrome kontrolü: açık/koyu tema, 1440 ve 390 piksel, gün/hafta/ay, sağ tık, boş saatin forma aktarımı, ekleme, düzenleme, tamamlama, silme ve merkezi onayı iptal etme.
- Mobil belge genişliği 390 piksel, yatay taşma yok. 30/60 dakika kartları 58/118 piksel içerik alanı; ayrı ders renkleri doğrulandı. Konsol hatası yok.

Kanıtlar: artifacts/calendar-menu-front-final.log, calendar-edit-backend-final.log, calendar-plan-api-regression.log, calendar-menu-student-build.log, calendar-menu-staff-build.log, calendar-menu-light-desktop.png, calendar-menu-dark-mobile.png, calendar-detail-light.png.
