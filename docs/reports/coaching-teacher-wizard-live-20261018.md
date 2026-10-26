# Öğretmen çalışma planı sihirbazı — canlı doğrulama

18 Ekim 2026, 19:01 Türkiye saatinde `coaching-teacher-wizard-20261018` sürümü Coaching ve personel portalına alındı. Öğrenci portalı, Identity ve diğer çalışan servislerin konteynerleri değiştirilmedi. Veritabanı şeması geçişi yok.

## Canlıda doğrulanan akış

1. Chrome'daki öğretmen hesabında **Çalışma takibi → Canlı Test student → Planı yönet → Sihirbazla otomatik plan hazırla** açıldı.
2. Öğrencinin eğitim bağlamı, Matematik dersi, konu durumları ve mevcut 7 haftalık ders saati yüklendi. Konu durumları öğretmende salt okunur kaldı.
3. Cumartesi 19:00 için 30 dakikalık ders önerisi oluşturuldu. Açık onayla haftalık taslağa eklendi; eski 7 saat korunarak kayıt 8 ders saatine çıktı.
4. Yıllık önizleme varsayılan hedefi **1 Haziran 2027** getirdi. Öğrencinin 52 Matematik kazanımı, 7.215 dakika ve 175 çalışma parçası ile yerleşti. Tahmini bitiş **8 Şubat 2027**, sığmayan süre **0 dakika** olarak gösterildi.
5. Önizleme kontrol kutusu onaylanıp **Öğretmen sihirbazı · canlı doğrulama 18.10.2026** başlığıyla taslak oluşturuldu. Taslak öğretmen plan ayrıntısında açıldı; eski plan bu aşamada aktif kaldı.
6. Ayrı yayımlama onayı verildi. Başarı mesajı görüldü; yeni plan aktif, önceki plan arşiv oldu.
7. Öğrenci hesabında çalışma planları açıldığında yeni plan otomatik seçildi. Kazanım kodu/metni, 60 dakikalık ilk çalışma, saatleri ve süre sürümü 2 görüldü. Tamamlama düğmesi erişilebilir; bu doğrulamada çalışma tamamlanmış olarak işaretlenmedi.
8. Önceki yıllık plan `519310a9-18f4-459c-afb8-3a871689fa5a` arşivden açıldı, takvim ve çalışmalarına erişim doğrulandı. Ekran yeni aktif plana geri döndürüldü.

Yeni aktif plan: `84e6448f-069c-4ec3-ba9b-daa07e3edbb9`.

## Veri koruma

- Dağıtımdan önce Coaching ve Identity için tam PostgreSQL yedekleri alındı. İmaj değişimi öncesi/sonrası tüm seçili iş tablolarının sayıları ve parmak izleri eşleşti.
- Canlı sihirbaz testinden hemen önce ayrıca Coaching yedeği ve mevcut satır parmak izleri alındı.
- Test sonrasında **506 eski çalışma**, **501 eski kazanım bağlantısı**, öğrenci profili ve eski plan içerikleri aynı kaldı. Önceki aktif planın durum/sürüm/yayımlama/arşiv alanları, istenen yayımlama işleminin normal değişiklikleri olarak karşılaştırmadan ayrıldı.
- Tamamlanan **6 çalışma ve 115 gerçek dakika** korundu. Yetim kazanım bağlantısı yok.
- Toplam plan sayısı **8 → 9**, aktif plan sayısı **1**. Yeni plandaki **175 çalışmanın 175'inde** kazanım bağlantısı var; toplam süre **7.215 dakika**, ilk çalışma **19 Ekim 2026**, son çalışma **8 Şubat 2027**.
- Hesap içerikleri, roller ve ürün erişimleri tekrar karşılaştırıldı ve korundu; girişe bağlı geçici sayaç/tarih alanları bu hesap parmak izinden çıkarıldı.
- Yeni test planı aktif bırakıldı; önceki planlar silinmedi. Haftalık taslakta onaylanan cumartesi önerisi kayıtlı kaldı.

## Düzeltilen eksik ve operasyon kaydı

Öğretmen sihirbazındaki öğrenci kurulum/takvim sayfasına giden bağlantılar, öğretmen bağlamında kaldırıldı. Öğrenci görünümündeki bağlantılar korundu. İzole üretim derlemesi başarılı; mevcut paket boyutu uyarıları sürüyor.

### Otomatik testler

- İzole üretim kaynaklarında öğrenci sihirbazı, öğretmen bağlamlı yıllık önizleme ve taslak oluşturma, öğrenciye dönüş davranışı, süre yetersizliği seçenekleri ve hedef öğrenci API yollarını kapsayan **23/23 test** geçti.
- Öğretmen ekranında öğrenci seçimi, sihirbaz açılması, seçili öğrencinin verilerinin yüklenmesi ve öğrenci sayfası bağlantılarının bulunmamasını kapsayan **4/4 test** geçti.
- Coaching müfredat/plan test projesinde **58 test geçti, 25 PostgreSQL ortamı isteyen test atlandı**. Bunların tamamının çalıştığı iddia edilmez; yukarıdaki akış gerçek canlı PostgreSQL verileri ve satır karşılaştırmalarıyla ayrıca doğrulandı.
- Mevcut test beklentileri, öğrenci çağrılarında yeni isteğe bağlı hedef öğrenci parametresinin `undefined` kalmasını ifade edecek şekilde güncellendi.
- Genel çalışma ağacının Angular test derlemesi beklemede kaldığı için durduruldu; hedef testler, dağıtımda kullanılan temiz kaynaklar ve yalnız bu testleri içeren TypeScript yapılandırmasıyla başarıyla çalıştırıldı.

Servislerde yeniden başlama sayısı 0; yayımlamadan sonraki denetimde Coaching/personel loglarında uygulama hata işareti 0. Öğrenci tarayıcı hata kaydı boş. Öğretmen tarayıcısında mesaj kanalı dinleyicisine ilişkin tarayıcı iletişim hataları görüldü; test edilen uygulama akışını engellemedi.

- Paket SHA256: `3cf2bbfb2bdfeaed57882a26cc92215a091b8bc4af2bee1b31153ea6f23cd52c`.
- Coaching imajı: `sha256:d0eff54ceb6ee5c26fd4a8c4301fc8a385e57c210269897e287e05bf444716c9`.
- Personel imajı: `sha256:ebc286d719eae166cd72bb34bdc6df7be0dbc9ac6dcf46af29e2f67d486403a5`.
- Yedekler, imaj geri dönüş dosyaları ve canlı karşılaştırmalar: sunucuda `/var/lib/eduivme/releases/coaching-teacher-wizard-20261018/`.
- Öğrenci ekran kanıtı: `artifacts/coaching-teacher-wizard-live-20261018/proof/student-published.png`.

Bu test, mevcut test öğrencisinin LGS/Matematik bağlamında yapıldı; farklı öğrenci ve müfredat kombinasyonlarının tümü canlıda denendiği anlamına gelmez.
