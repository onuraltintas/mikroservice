# Admin kullanıcı rollerinde servis kapsamı

15 Ekim 2026 tarihinde yalnız admin paneli güncellendi. Kaynak commit: `98438809b44811c48f67acbfc81388453457c4b9`. Canlı geçiş: `2026-10-15T10:54:40Z` (Türkiye 13:54).

## Sorun ve düzeltme

Veritabanında kullanıcı rolü `(kullanıcı, rol, servis)` kapsamında tutuluyor. Test öğrenci, öğretmen ve kurum hesaplarının her biri tek hesaptır; her iki serviste aynı adlı birer rolleri vardır. Kullanıcı listesi yalnız `roles` dizisindeki adları gösterdiği için iki adet Teacher/Student/InstitutionAdmin birbirinin kopyası gibi görünüyordu. API zaten `productRoles` alanında kapsamı gönderiyordu.

- Liste ve rol yönetimi artık `Teacher · Koçluk`, `Teacher · Hızlı Okuma` gibi atamaları gösterir.
- Tekli ve toplu rol eklemede servis seçimi zorunludur. Bir servisteki mevcut rol, diğer serviste aynı rolün eklenmesini gizlemez.
- Rol kaldırma tıklanan atamanın servisli DELETE uç noktasına gider. Ekleme/silmede gizli Koçluk varsayımı kaldırıldı.
- Toplu işlemin onayı servis adını, mevcut roller kaldırılacaksa bunun yalnız seçilen servis için geçerli olduğunu belirtir. Onaydan önce alınan seçimler istekte korunur.
- Başarılı işlemden sonra `roles`, `productRoles` ve `productAccesses` birlikte yenilenir. Yenileme başarısızsa tekrar yükleme tamamlanana kadar yeni değişiklik engellenir. Kapsam bilgisi eksik kayıtlarda servis tahmin edilmez.

`SystemAdmin · Tüm servisler` bilinçli genel roldür. Mevcut genel atama global DELETE ile yönetilir; MFA ve son aktif yönetici koruması sunucuda sürer. SystemAdmin servis rolü ekleme seçeneklerinden çıkarıldı ve bu durum ekranda açıklandı: mevcut ürünlü POST, SystemAdmin için genel rolün yanında seçilen servise erişim de açtığından bunu sıradan servis rolü gibi sunmak yanıltıcıydı. Genel SystemAdmin ataması bu servis rolü ekranından yapılmaz; bu yayın yeni genel atama API'si eklemez.

## Giriş nasıl ayrılıyor?

1. `/api/auth/{coaching|speed-reading}/login` route'u servisi belirler; gövdedeki ürün değeri bunu değiştiremez. Hesap doğrulamasından sonra seçilen servise aktif erişim aranır. Genel SystemAdmin ayrı istisnadır.
2. `GetRolesForProductScope` yalnız seçilen servis rollerini ve genel SystemAdmin'i seçer. JWT `platform_product` bilgisini ve yalnız bu kapsamın rol/izinlerini taşır; izinler ayrıca `PlatformPermissionScope` ile süzülür.
3. Hızlı Okuma ve Koçluk API'lerinin `ProductScopeMiddleware` kontrolü karşı servisin kullanıcı JWT'sini 403 ile reddeder. Endpoint rol/izin ve ilgili kurum kapsamı kontrolleri ayrıca çalışır. Refresh sırasında ürün korunur; servis değişiminde erişim ve personel rolü yeniden doğrulanır.

Rol ekleme, seçilen servis erişimini de tanımlar/yeniden etkinleştirir. Rol kaldırma diğer servis rolünü, aboneliği veya kurum bağlantısını silmez. Abonelik çalışma erişiminde ayrıca değerlendirilir. Servis kapsamlı rol değişimi yalnız o servisin refresh oturumlarını iptal eder. Önceden verilmiş kısa ömürlü access JWT'nin anında iptal edildiği iddia edilmez; mevcut token süresi dolana kadar önceki claim'leri taşıyabilir.

## Doğrulama

- Admin için 32 seçili servis/bileşen testi geçti: iki servis endpoint'leri, genel SystemAdmin kaldırma, kapsam seçimi, refresh hatası, başarısız kaldırmada mevcut rollerin korunması, toplu işlem onayı.
- Giriş/ürün erişimi, ürün rol ataması, ürün middleware'leri, personel servis geçişi, token süresi, oturum iptali ve SystemAdmin MFA için 82 mevcut backend testi geçti; 0 başarısız/atlanan. Sandbox testhost IPC zaman aşımı sonrası aynı derleme sandbox dışında `--no-build` ile doğrulandı. Kanıt: `artifacts/identity-product-role-review-20261015/identity-product-role-scope-confirmed.trx`.
- Canlının önceki admin kaynağı `639cb289` ve aynı sürüm `clients/shared` temelinden, yalnız bu görevin on admin kaynak/test dosyası taşınarak üretim derlemesi alındı. Mevcut ilgisiz çalışma değişiklikleri pakete alınmadı. Derleme geçti; önceki kapsam dışı SCSS boyut uyarıları devam ediyor.
- Yerel üretim derlemesinde 20, canlıdan sunulan arayüzde 20 masaüstü/mobil tarayıcı kontrolü geçti. Liste, tekli ekleme/kaldırma, diğer servis rolünün korunması, 403 sonrası durum, toplu servis seçimi/onayı ve istek kapsamı doğrulandı. **Tüm API/oturum çağrıları fixture ile karşılandı; bu kontroller gerçek CAPTCHA/giriş veya canlı kullanıcı rolü değiştirme testi değildir.** Kanıtlar: `artifacts/admin-role-services-local/`, `artifacts/admin-role-services-live/`.
- Canlı veritabanı salt okunur kontrolü: üç test e-postasının her biri tek hesap; her birinin Coaching ve SpeedReading rol atamaları birer adet. Yinelenen veritabanı ataması yok.

## Yayın

Etiket: `admin-role-services-20261015`. Önceki admin imajı `sha256:73781855149307958d0ab7417952a30eb19a5550d5bc8e41721580b6614e4ffd`; yeni admin imajı `sha256:2efe839ffaa4158d71ec4b9585b80f4272ebafb676d95683e2521e374d06aff5`.

Migration ve backend yayını yok. Mevcut gerçek ortam ve diğer servis imajları sabitlendi. Aday imaj ağsız önizlemede açıldı; promotion imaj/paket eşleşmesi, readiness ve kamu girişini kontrol etti. Başarısız geçişte doğrulamalı önceki admin imajına dönüş hazırdır. Yayın sonrası admin running/restart 0; diğer konteyner kimlikleri ve Koçluk abonelik ayarlarının hash'i değişmedi.

Yayın betikleri ve tarayıcı doğrulaması: `tools/releases/admin-role-services-20261015/`. Sunucudaki yayın dizini `/var/lib/eduivme/releases/admin-role-services-20261015`; ortam ve geri dönüş dosyaları yalnız root erişimlidir.
