# Karar sorumluluğu devri ve gerekçeli erteleme

24–25 Ekim 2026. Yerel uygulama ve doğrulama; canlı dağıtım yapılmadı.

## Öğretmen değişikliği

Kararın mevcut `TeacherId` alanı kararı oluşturan öğretmeni korur. Yeni `ResponsibleTeacherId` güncel sorumludur. Kurum öğrenci düzenlemesinde öğretmen değiştirildiğinde aynı kapsamdaki açık (`Planned`/`InProgress`) kararlar yeni öğretmene devredilir. Bağlantı sonlandırıldıktan sonra yeni bağımsız öğretmen davetinin kabul edilmesi de aynı devri uygular.

Ek bir aktif öğretmen atamak, hâlâ öğrenciye bağlı olan öğretmenin kararlarını devralmaz. Yeni öğretmen belirtilmeden bağlantı sonlandırılırsa kararlar korunur; sonraki açık atamada uygun kararlar devredilir. Kapalı kararlar, başka öğrenciler ve başka kurum kapsamındaki kararlar taşınmaz.

Devir aynı karar kimliğinde gerçekleşir. İlk öğretmen, değerlendirme haftası, ilk son tarih, önceki geçmiş, bağlı çalışma/ödev kimlikleri ve bu işlerin içerikleri korunur. Geçmişe işlemi yapan kişi, eski/yeni sorumlu ve zaman eklenir. Yeni öğretmen kararı düzenleyebilir, gerekçeyle erteleyebilir ve bağlı işlerin durumunu görebilir. Önceki öğretmenin ataması sona erdiğinde eski sekmeden yazması da sunucuda reddedilir. Bağlı eski ödevin öğretmenliği değiştirilmez; yeni oluşturulan ödev güncel sorumluya aittir.

Öğretmen değişikliği, atama ve karar kayıtlarını aynı işlemde günceller. Öğrenci bazlı veritabanı kilidi karar yazma/erteleme işlemleriyle ortaktır. Devir bildirimi öğrenci ve yeni sorumluya, mevcut haftalık karar bağlantısıyla gider. Bildirim gövdesine özel karar metni veya erteleme gerekçesi konmaz.

## Gerekçeli erteleme

Yetkili güncel sorumlu, gecikmiş açık kararda **Gerekçeyle ertele** düğmesini kullanır. Yeni tarih bugünden ve mevcut son tarihten ileride olmalıdır. Boşlukları temizlendikten sonra 5–1000 karakter gerekçe zorunludur. İlk son tarih ayrı alanda korunur; eski/yeni tarih, gerekçe, işlem yapan kişi ve zaman geçmişe eklenir.

Erteleme kararın ilk haftasıyla sınırlı değildir; `ReviewWeekStart` değiştirilmez. Gecikmiş veya daha önce hafta dışına ertelenmiş son tarih normal düzenlemeyle değiştirilemez. Henüz gecikmemiş kararların ilk hafta içindeki mevcut tarih düzenlemesi korunur. Ertelenen kararın başlığı, takip notu ve durumu sonradan normal biçimde düzenlenebilir.

Kararın takip tarihini değiştirmek bağlı çalışma veya ödevin tarihini değiştirmez. Form bunu açıklar. Bağlı işin takvimi mevcut önizleme/onay akışından ayrıca düzenlenir. Yeni son tarih ve ilk son tarih, öğrenci ve öğretmenin ortak karar görünümünde gösterilir.

Sürüm uyuşmazlığı 409, yetkisiz/eski sorumlu erişimi 403, geçersiz tarih/gerekçe 400 döner. Aynı başarılı ertelemenin aynı sürüm, tarih ve gerekçeyle hemen tekrar gönderilmesi geçmiş veya bildirim çoğaltmaz. Çakışmada arayüz girdileri korur; sorumluluk/yetki kaybedilirse eski düzenleme kapatılır.

## Mevcut verinin korunması

Geçiş: `20261024203951_DecisionResponsibilityAndPostponement`.

- Dört alan ekler: `ResponsibleTeacherId`, `InitialDueOn`, `InstitutionId`, `HasAssignmentScope`.
- Güncel sorumluyu eski öğretmen kimliğinden başlatır. İlk son tarihi geçmişin ilk tarihinden, geçmiş boşsa mevcut tarihten alır.
- Eski atama kayıtlarından tek bir kapsam belirlenebiliyorsa kurum/bağımsız kapsamı kaydeder. Kaynağı olmayan veya birden fazla olası kapsamı bulunan eski kayıtları tahminle başka öğretmene taşımaz.
- Eski karar kimliklerini, öğretmenini, haftasını, tarihini, durumunu, geçmiş JSON'unu, bağlantılarını, oluşturulma zamanını ve sürümünü değiştirmez.
- İlk iki düzeltmedeki plan geçmişi ve tamamlanan kazanım kredisi davranışını korur.

Yeni tarih eski şemaya sığmıyorsa veya güncel sorumlu ilk öğretmenden farklıysa eski şemaya dönüş durdurulur; geri alma sırasında tarih veya sorumlu sessizce değiştirilmez. Geçiş, yeni API'den önce mevcut tek seferlik migration akışıyla uygulanmalıdır. API, bildirim tüketicisi ve iki portal birlikte yayımlanmalıdır.

## Test sonuçları

- PostgreSQL etkin geniş koçluk regresyonu: **216 başarılı, 0 başarısız, 0 atlanan**.
- Profil/kurum/davet ve bildirim tüketicisi grubu: **17 başarılı**.
- Öğrenci arayüzü geliştirme grubu: **147 başarılı**.
- Öğretmen portalında etkilenen ortak haftalık akış: **55 başarılı**. Öğrenci/öğretmen ortak senaryoları farklı senaryolar gibi toplanmamalıdır.
- Geniş öğretmen arayüz grubu: **133 başarılı, 1 başarısız**. Bu görevde değişmeyen `CoachingTeacherTrackingComponent` testinde `Europe/Istanbul` metni bekleniyor; mevcut görünüm bu metni göstermiyor. Yeni haftalık karar ve erteleme testleri bu grupta da geçiyor. Bu bağımsız metin beklentisi düzeltilmedi.
- Coaching API Release: **0 hata, 0 uyarı**. Notification API Release başarılı; mevcut bağımlılık uyarıları var. İki portalın üretim derlemesi başarılı; mevcut stil/paket bütçe uyarıları sürüyor.

Yeni PostgreSQL testleri kurum içi devir, ek öğretmenin kararları almaması, öğretmensiz ara durum, bağımsız davet kabulü, eski/yeni öğretmen yetkileri, eski bağlı işlerin korunması, takip merkezinde yeni sorumlu, gerekçe/tarih/sürüm reddi, tekrar gönderim, veri geçişinin eski sütunları koruması ve geri alma engelini kapsar. İlişki, karar ve outbox birlikte geri alınır; aynı servis kapsamındaki tekrar denemede kayıt/bildirim çoğalmaması hem kurum değişikliğinde hem davet kabulünde gerçek PostgreSQL ve EF Bus Outbox ile doğrulandı.

Kanıtlar: `artifacts/coaching-decision-handoff-20261024/` altındaki sunucu TRX ve günlükleri, profil/bildirim ve arayüz günlükleri, Release ve üretim derleme günlükleri. Testler yalnız geçici yerel PostgreSQL veritabanlarında çalıştırıldı.
