# Takvim menüsü: canlı dağıtım ve doğrulama

21 Ekim 2026. Öğrenci ve öğretmen canlı hesaplarında istenen takvim akışları doğrulandı. Testte bulunan süre varsayılanı ve yakın ders renkleri sorunları düzeltildi ve yeniden yayımlandı.

## Canlı sürüm

- Coaching API: `coaching-calendar-menu-20261021-r2`.
- Öğrenci ve öğretmen arayüzleri: `coaching-calendar-menu-20261021-r4`.
- Sunucu: `/var/lib/eduivme/releases/coaching-calendar-menu-20261021-r4`.
- Geri dönüş: aynı dizindeki `rollback.compose.json`. API ve diğer servisler son iki arayüz güncellemesinde değiştirilmedi.
- Veritabanı geçişi yok. Coaching, Identity ve Notification yedekleri sunucuda saklanıyor. Şemalar dağıtım öncesiyle eşleşti.

İlk dağıtım dört API ve dokuz arayüz dosyasıyla sınırlandı. Ortak sözleşme DLL/PDB dosyaları canlı sürümde korundu; PDB kaynak kontrolünde beklenmeyen fark yok. İlk paket ortak derleme özeti uyuşmadığı için servis değiştirmeden durduruldu; gerçek canlı dosyalar korunarak r2 hazırlandı. Son r4 paketinin r3 üzerine gerçek kaynak farkı yalnız renk yardımcısındadır.

## Canlı akışlar

| Kontrol | Sonuç |
| --- | --- |
| Öğrenci boş saatte sağ tık | Tıklanan gün ve yarım saat ekleme formuna geçti; dersler güncel LGS kataloğundan geldi |
| Öğrenci ekleme, düzenleme, taşıma | Yeni test çalışması oluşturuldu, başlık/süre değiştirildi ve başka güne taşındı |
| Öğretmen ekleme, düzenleme, taşıma | Aynı öğrencinin yeni test çalışmasında doğrulandı; öğrenci yenilemede gördü |
| Dolu saate taşıma | Çakışma mesajıyla engellendi; mevcut çalışma değiştirilmedi |
| Öğrenci tamamlama | 55 dakika ve 20 soru kaydedildi; kartta ✓ ve 1/1 tamamlandı göründü |
| Öğretmenin tamamlanan çalışmayı görmesi | Plan yenilendiğinde aynı tamamlanma bilgisi göründü |
| Tamamlanan kartı koruma | Düzenleme, taşıma, silme ve tekrar tamamlama kapalı |
| Öğretmen yetkisi | Öğrenci adına tamamlama menüsü sunulmuyor |
| Silme | Kullanıcının açıkça onayladığı yalnız iki yeni bekleyen test çalışması silindi; sıfır eşleşme veritabanından doğrulandı |
| Gün / hafta / ay | İki rolde geçişler ve aylıktan gün açma çalıştı |
| Açık / koyu ve mobil | İki rolde 390×844 kontrolü; sayfa genişliği ve scrollWidth 390, sayfa taşması yok |
| Mobil menü | ⋯ düğmesi çalıştı; tamamlanan çalışmanın koruması aynı |
| Ders kartları | 30/60 dakika yaklaşık 58/118 piksel; aynı ders aynı renkte, altı LGS dersi altı ayrı renk ailesinde |
| Form ve onaylar | Uygulama içi pencereler ve merkezi onay/toaster; tarayıcı dialog kullanılmadı |

Öğretmen sayfası gerçek personel adresi `https://onuraltintas.net/staff/` üzerinden test edildi. Başlangıçtaki giriş/reCAPTCHA sorunu sonrası kullanıcı iki oturumu açtı. Son arayüz dağıtımlarından sonra oturumlar yenilemeyle korundu.

## Bulunan ve düzeltilen sorunlar

1. Planlanan süre değişince dokunulmamış gerçek-süre varsayılanı eski değerde kalıyor, gereksiz kaydedilmemiş değişiklik uyarısı oluşturuyordu. Eski varsayılan ile gerçek kullanıcı girdisi ayrıldı. Otomatik test önce 30/60 farkında başarısız oldu, düzeltmeden sonra geçti. Canlı öğretmen çalışması 60→30 dakika değiştirildi; plan uyarı vermeden yenilendi. Gerçek kullanıcı girdisi olan 17 dakikanın korunması da otomatik testte doğrulandı.
2. Kimlik özetiyle üretilen yakın tonlar bazı dersleri birbirine benzetiyordu. Altı ayrı renk ailesi kullanıldı; derssiz özel çalışmaların gerçek derslerin renk sırasını etkilemesi önlendi. Canlı DOM ve görsel kontrolünde altı LGS rengi doğrulandı.

## Testler ve derleme

- İlk geliştirmede 55 Angular, 17 PostgreSQL/HTTP/karar bağlantısı ve 7 mevcut API regresyon testi geçti.
- Süre/renk düzeltmelerinde iki ilgili dosyada 45 test geçti; son renk sıralaması sonrasında takvim dosyasındaki 11 test yeniden geçti. Bunlar örtüşen çalıştırmalardır, toplam olarak toplanmamalıdır.
- Üretim yedeğinin ayrı veritabanına geri yüklendiği provada öğrenci/öğretmen CRUD, taşıma, tamamlama, atama yetkisi, çakışma ve tamamlanan çalışma koruması geçti. Eski çalışma/kazanım ilişkileri değişmedi; geçici veritabanı kaldırıldı.
- İki arayüzün üretim derlemeleri geçti. Önceden mevcut stil bütçesi uyarıları kaldı; bütçeler gevşetilmedi.
- Son canlı JS/CSS istekleri 200 ve doğru MIME türlerinde. Üç servis çalışıyor, yeniden başlama sayıları 0. Dağıtım sonrası API günlüklerinde ERR/FTL veya işlenmemiş istisna yok.

## Kayıt koruma sonucu

Başlangıç: 44 plan, 62.017 çalışma, 2 tamamlanan çalışma, 61.979 kazanım bağlantısı; aktif test planı sürüm 10.

Sonuç: **44 plan, 62.018 çalışma, 3 tamamlanan çalışma, 61.979 kazanım bağlantısı**. Aktif plan aynı: `a4cea155-5485-4db6-9f52-1ec4df8080c3`, sürüm 21. Artış yalnız yeni tamamlanan test çalışmasıdır.

Korunan yeni tamamlanan çalışma: `3d07e795-46c5-478c-a7d7-ef192f5493e7`, “CANLI TAKVİM 21.10 · öğrenci düzenlendi”, 24 Ekim 09:00–10:00, gerçek süre 55 dakika, 20 soru.

Kullanıcı onayıyla silinen yeni bekleyen çalışmalar:
- “CANLI TAKVİM 21.10 · silme kontrolü”
- “CANLI TAKVİM 21.10 · öğretmen düzenlendi”

Eski iki tamamlanan çalışma 55 ve 25 dakikalık değerleriyle korundu. Dağıtım öncesindeki tüm iş satırları içerik özetleriyle yeniden karşılaştırıldı; eksik/değişmiş eski satır yok. Yalnız aktif test planının sürüm/güncelleme zamanı ve iki test hesabının doğal giriş metadata alanları ayrı tutuldu. Hesapların diğer alanları, mevcut karar bağlantıları ve eski bildirimler karşılaştırmaya dahil edildi. Doğrulama betiğinde gerçek `identity.users` tablo adı kullanıldı.

## Kanıtlar

- `artifacts/coaching-calendar-menu-live-20261021/final-records.log`
- `artifacts/coaching-calendar-menu-live-20261021/r4-promotion.log`
- `artifacts/coaching-calendar-menu-live-20261021/server-r4/`
- `artifacts/coaching-calendar-menu-live-20261021/server/drill-probe.log`
- `artifacts/calendar-duration-default-red-retry.log`
- `artifacts/calendar-hotfix-green.log`
- `artifacts/calendar-palette-final.log`
- `artifacts/coaching-calendar-menu-live-20261021/student-final-desktop.png`
- `artifacts/coaching-calendar-menu-live-20261021/student-final-mobile-dark.png`
- `artifacts/coaching-calendar-menu-live-20261021/teacher-final-desktop.png`
- `artifacts/coaching-calendar-menu-live-20261021/teacher-final-mobile-dark.png`

Son tarayıcı görünümü açık temaya ve normal masaüstü boyutuna geri alındı. Eski plan veya çalışma silinmedi.
