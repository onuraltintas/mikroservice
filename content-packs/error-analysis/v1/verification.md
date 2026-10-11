# Hata Analizi — doğrulama raporu

Tarih: 11 Ekim 2026. Durum: yerel geliştirme ve doğrulama tamamlandı; GitHub'a gönderilmedi, canlıya uygulanmadı.

## Tamamlanan kapsam

- Dört yaş grubunun mevcut 4–5. seviyeleri korundu. Uzunluk, süre ve hata sayısı tablosu [README](README.md) içindedir; bunlar ürün ayarlarıdır, bilimsel yaş normları değildir.
- Sekiz yaş/seviye hücresine beşer, toplam 40 metin seçkisi hazırlandı. Bunlar 40 bağımsız hikâye değildir. Kaynak metinlerin ve başka egzersizlerin sorularının içeriği değiştirilmedi.
- Rastgele harf silme yerine metinle birebir doğrulanan, önceden hazırlanmış hata çiftleri kullanılır. Seviye 5'te en az iki harf sırası hatası bulunur. Eksik veya değişmiş içerikte egzersiz güvenli biçimde başlatılmaz.
- Metin aynı egzersize, yaşa, seviyeye ve uzunluk bandına ait olmalıdır. Öğrenciye hatasız kaynak veya hedef anahtarı gönderilmez; öğrenci katalog ve bağımsız okuma yollarından da kaynak cevaba erişemez. Yetkili personelin salt okunur önizlemesi korunur.
- Önizleme gerçek hazırlanmış metinleri kullanır; sonuç kaydetmez. Özel ayarlarda süre, hazırlanmış hedef sınırı içinde hata sayısı ve yazı boyutu değiştirilebilir. Eski süre alanlarının özel süreyi ezmesi giderildi.
- Sunucu puanlama, yanlış seçim, ipucu, tekrar gönderim ve sonuç kaydı doğrulandı. Bu egzersiz okuma hızı veya anlama ölçümü olarak raporlanmaz.
- Eski başlamış oturum snapshotları ve sonuçlar korundu. Mevcut statik hata yapılandırmaları çalışmaya devam eder. Yeni tablo veya şema migration'ı eklenmedi.

## Test sonuçları

| Kontrol | Sonuç |
|---|---|
| Hızlı Okuma arka uç birim testleri | 1188 / 1188 geçti |
| Universal Player ön yüz testleri | 568 / 568 geçti |
| İçerik sözleşmeleri | 6 / 6 geçti |
| Gerçek PostgreSQL içerik/geçiş sözleşmeleri | 12 / 12 geçti |
| Gerçek PostgreSQL servis entegrasyonu | 2 / 2 geçti |
| Masaüstü/mobil tarayıcı akışları | 10 / 10 geçti |
| Üretim ön yüz derlemesi | Geçti |
| C# ve genel/TypeScript kod incelemeleri | Onaylandı |

Tarayıcı testleri API fixture'larıyla yürütüldü; canlı VPS veya gerçek Identity oturumu kabul testi değildir. PostgreSQL servis testi sekiz hücrenin başlangıç, gizli hedefler, tamamlanma, tekrar gönderim ve kalıcılık davranışlarını kapsar. SQL testleri tekrar çalıştırılabilirliği, değişmiş ayar/içerikte işlemin tümüyle geri alınmasını ve geçmişin korunmasını kontrol eder.

Testlerin tamamının geçmesi, kod kapsamının %100 olduğu anlamına gelmez. Universal Player test koşusunun toplam satır kapsamı %68,23'tür. Hata Analizi motorunun satır kapsamı %99,09, özel önizleme ayarlarının %99,39'dur. Bu koşuda arka uç kod kapsamı ölçülmedi.

Üretim derlemesinde mevcut `exercise-player.component.scss` dosyasının 120 kB bütçesini yaklaşık 8,98 kB aşması uyarısı vardır. Bu görevde SCSS değiştirilmedi; derleme başarılıdır.

### Tekrar çalıştırma

Depo kökünden:

```powershell
dotnet test tests/Unit/SpeedReading.Application.UnitTests/SpeedReading.Application.UnitTests.csproj --no-restore --verbosity minimal
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~SpeedReadingErrorAnalysis" --verbosity minimal
node --test tests/E2E/error-analysis-catalog.test.mjs
node --test tests/E2E/error-analysis-catalog.postgres.test.mjs
```

Ön yüz ve tarayıcı komutları için proje çalışma dizini ile mevcut test yapılandırmaları kullanılmalıdır. PostgreSQL testleri çalışan Docker motoru, tarayıcı kontrolleri kurulmuş Edge gerektirir.

## Yayın sınırı

Bu rapor canlı yayın onayı veya canlı doğrulama değildir. Yayından önce yalnız Hızlı Okuma veritabanının doğrulanmış yedeği alınmalı; API kontrollü durdurulmalı, sekiz canlı ayar kayıtlı tabanla karşılaştırılmalı ve `apply.sql` hata halinde duracak şekilde çalıştırılmalıdır. Ayar sapmasında zorla üzerine yazılmamalıdır. API, ön yüz ve içerik paketi birlikte yayımlanmalı; ardından yetkili oturumla canlı akışlar doğrulanmalıdır. Geri dönüş yayın öncesi yedek ve önceki uygulama sürümüdür.
