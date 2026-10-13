# Koçluk canlı yayını — 13 Ekim 2026

## Sonuç ve doğrulama sınırı

Koçluk tamamlayıcı değişiklikleri canlıya alındı. Beş uygulama `coaching-completion-20261013-ac014834` imaj etiketinde çalışıyor. Ana uygulama yayını 17:36:17 UTC / 20:36:17 İstanbul saatinde tamamlandı. Son smoke kontrolü 17:58:36 UTC'de geçti: ana yayından 22 dakika, son edge geçişinden 15 dakika geçmişti. Üç API hazır, iki panel sağlıklı, yeniden başlama ve uygulama hata sayıları sıfırdı.

Gerçek SystemAdmin oturumunda koçluk finansı, katalog filtreleme ve operasyon listeleme test edildi. **Öğretmen–öğrenci–kurum haftalık plan döngüsünün canlı oturumlu testi tamamlanmadı.** Canlı kullanıcı listesinde iki Student ve bir SystemAdmin bulunuyor; Teacher, InstitutionAdmin veya InstitutionOwner hesabı yok. Kullanıcıdan test hesapları ve test kurumu bilgisi istendi. Sağlık veya anonim 401 kontrolleri bu eksik rol testlerinin yerine sayılmıyor.

## Kaynak ve yayın kapsamı

- Koçluk kaynak revizyonu: `ac014834ef5f1df74753530889b325026cc8fd76`.
- Canlıya çıkan servisler: Identity, Notification, Coaching, admin paneli, öğretmen/kurum personel portalı.
- Identity giriş kaynakları, canlı `google-direct-20261013` sürümünün portable PDB kaynak özetleriyle karşılaştırıldı. Altı giriş/yetkilendirme kaynağı korunuyor. Application tarafındaki gerekli kurum-öğrenci sorgu arayüzü eklemesi dışında eşleşme sağlandı.
- Personel panelinin koçluk dışındaki mevcut giriş kabuğu `0bffbe36`; admin menü/route temeli `6f315ff3` üzerinden korundu. Koçluk bileşenleri seçilen tamamlayıcı revizyondan alındı.
- Henüz yayımlanmamış Kurs Yönetimi kaynakları ve çalışma ağacındaki diğer değişiklikler bu pakete alınmadı. Üretim derlemeleri başarılı; paket `.env` dosyası içermiyor ve 563 dosyalı SHA256 manifestiyle doğrulanıyor. Son arşiv: `coaching-live-ac014834-verified-v2.tar.gz`; yerel ve sunucu SHA256 eşleşmesi `7fcc3757dfabbd096bc9c41db6d7c8496f6b0c752a64c521723a2b63528b249e`.
- Hızlı Okuma API/frontend imajları ve Gateway değiştirilmedi. Eski koçluk dış bağlantıları aşağıdaki edge yönlendirmeleriyle düzeltildi. Hızlı Okuma SPA içindeki eski modül bu yayında yeniden derlenmedi.

## Yedekler ve migration sırası

Sunucudaki özel yayın dizini: `/var/lib/eduivme/releases/coaching-completion-20261013-ac014834`. Dizindeki yedekler, gerçek ortam değişkenleri ve geri dönüş dosyaları yalnız root erişimindedir; Git'e veya yerel rapora alınmadı.

Identity, Notification ve Coaching veritabanları PostgreSQL custom-format ile yedeklendi, ayrı geçici veritabanlarına geri yüklendi ve yeni Linux imajlarının `--migrate-only` moduyla prova edildi. Ardından canlıda aynı bağımlılık sırası kullanıldı: **Identity → Notification → Coaching**. Yeni iç Identity sözleşmesi, Koçluk servisi geçişinden önce yayımlandı. Bildirim tüketicisi ve Koçluk şeması hazır olduktan sonra hatırlatma worker'ı etkinleştirildi.

Koçlukta uygulanan dört yeni migration:

1. `20261013121249_AddStudyTaskQuestionTracking`
2. `20261013123111_GuardCoachingSessionOverlap`
3. `20261013130901_AddCoachingReminderReceipts`
4. `20261013140909_AddCoachingFeeLedger`

Canlı geçmiş 34 kayıttan 38'e çıktı. Kaynakta bulunmayan, daha önce canlıda uygulanmış `20260924152005_AddAcademicGoalInstitutionScope` kaydı korundu. Kontrol yalnız başlangıç geçmişindeki bu tek eski kaydı kabul eder; diğer sapmaları reddeder. Kaynak listesindeki 37 kayıt ve bu eski kayıt birlikte kopya/canlı geçmişle birebir karşılaştırıldı. Son canlı kontrolde dört çizelgeleme/çakışma trigger'ı ve dört finans/hatırlatma tablosu ayrıca doğrulandı. Üç geçici prova veritabanı 17:58:39 UTC'de temizlendi; üretim yedekleri korundu.

Yedek SHA256 özetleri:

| Veritabanı | SHA256 |
| --- | --- |
| coaching_db | `177d18eca0852b5d9b50c6245e4397de784431719cb9c25617b6c385f4886a76` |
| identity_db | `878550970d464d1d0e89263a950cfc8b05a19119cdc44295775bb26dcf240658` |
| notification_db | `b799b92878f23d42ca7825bc070c0b7fe42e3bb37989e40ad1baf2e98c793eb1` |

## Canlı test kanıtları

| Kontrol | Sonuç |
| --- | --- |
| Üç API `/health/ready` | Healthy; uygulama hata sayısı 0 |
| Admin ve personel paneli | Healthy; beş hedef serviste restart 0 |
| Identity iç kurum-öğrenci sözleşmesi | Gerçek servis anahtarıyla HTTP 200; var olmayan test kimliklerinde `isActive=false` |
| Gerçek admin finans oturumu | Açık ücret/kasa sorguları ve yenilemeler çalıştı; mevcut ücret/tahsilat kayıtları boş |
| Gerçek admin katalog oturumu | 169 ders; Matematik filtresinde 20 kayıt |
| Gerçek admin operasyon oturumu | Seans ve sınav sekmeleri boş durumla, hedef sekmesi 13 mevcut kayıtla açıldı |
| Personel giriş ekranı | Koçluk ürünü seçili; öğretmen/kurum girişi görüntülendi; oturumlu personel akışı yapılmadı |
| Anonim öğrenci planı, kendi ücretleri, personel açık ücretleri, admin açık ücretleri, personel planı ve kurum listesi | Altı uç HTTP 401 |
| Öğrenci finans, admin finans, personel girişi, Hızlı Okuma girişi | Dört public adres HTTP 200 |
| Hedef dışı container kimlikleri/imajları | Yayın öncesiyle birebir aynı |

Ekran kanıtı: `artifacts/coaching-live-20261013/admin-finance-live.png`. Canlı `live-smoke-final.log`, migration geçmişleri, yedek özetleri ve zaman işaretleri aynı yerel kanıt dizinine alındı; ortam değişkenleri/üretim veritabanı içerikleri alınmadı. Yerel geliştirme kanıtları önceki tamamlanma raporundadır: 767 backend, 308 öğrenci/admin, 180 personel testi ve 12 farklı E2E senaryosu. Bu sayılar canlıda çalıştırılmış dört rol testi olarak sunulmamalıdır.

Eski `masterhizliokuma.com` adreslerinde beş HTTP 302 ve tam Location eşleşmesi doğrulandı:

- `/student/coaching`, `/student/coaching/` → `https://eduivme.com/coaching-portal`
- `/coaching`, `/coaching/dashboard`, `/coaching/students` → `https://onuraltintas.net/staff/?product=coaching`

Edge değişikliği 17:43:17 UTC'de doğrulandı; mevcut yapılandırma yedeklendi, aday Caddy yapılandırması valide edildi ve aynı aday dosya reload edildi. Ana giriş ve mevcut öğretmen Hızlı Okuma yönlendirmesi korundu.

## Yayın sırasında giderilen kontrol sorunları ve geri dönüş

İlk imaj derlemesi BuildKit'in doğrudan `sha256:...` imaj kimliğini registry adı gibi yorumlaması nedeniyle durdu; kayıtlı önceki imajların kimliği doğrulanmış yerel temel etiketleri kullanıldı. Canlı servisler bu aşamada değişmedi.

İlk geçişte admin SSR iç IP isteğini izinli Host denetimiyle 400 reddetti. Gerçek public alan adı 200 dönüyordu. Yayın otomatik olarak önceki beş imaja geri döndü; migration Down uygulanmadı. `Host: eduivme.com` kontrolü düzeltildi, eski imajlar/readiness ayrı doğrulandı ve tekrar geçiş başarılı oldu. Özgün container audit kayıtları korunuyor.

İlk edge denemeleri public yönlendirme kontrolünden geçmedi ve önceki yapılandırmaya geri döndü. Reload, validate edilen aday dosyaya sabitlendi; iç proxy ve public kontroller ardından geçti. Bu kontrol hataları başarılı rol testleri olarak sayılmıyor.

Otomatik geri dönüş yalnız beş hedef imajı eski sabitlenmiş sürümlere geçirir, komut sonucunu, imaj kimliklerini ve readiness'i denetler. Edge için ayrı doğrulanmış yapılandırma geri dönüşü bulunur. **Finans migration'ının Down işlemi veri tablolarını düşürür; canlı geri dönüşte kullanılmaz.** Yedekler ve önceki imajlar korunur. Test kurum/hesapları hazır olunca haftalık plan yayımlama → öğrenci tamamlaması → öğretmen/kurum raporu ve rol bazlı finans yetkileri ayrıca canlıda doğrulanmalıdır.
