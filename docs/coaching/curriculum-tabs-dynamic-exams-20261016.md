# Sekmeli ders ve müfredat yönetimi — 16 Ekim 2026

Uygulama kontrol noktası: `ddc089ef`.

Durum: Yerel geliştirme ve doğrulama tamamlandı. **Canlıya alınmadı.** Önceki ağaç görünümünün yerine sekmeler kullanılır. Bu rapor, `curriculum-management-20261016.md` belgesindeki ekran ve sabit sınav kapsamı açıklamalarını günceller.

## Kullanıcı açısından değişenler

Yönetici yolu: `/dashboard/coaching/curriculum`.

| Sekme | Yönetilen kayıt | Üst kayıt / filtre |
|---|---|---|
| Sınav türleri | Kod, ad, sıra, aktiflik | Kök kayıt |
| Dersler | Ad, sınıf, sınav türü, ders programı, aktiflik | Sınav türü |
| Üniteler | Ad, sıra, aktiflik | Ders |
| Konular | Ad, sıra, önerilen çalışma dakikası, aktiflik | Ünite |
| Alt konular | Ad, sıra, önerilen çalışma dakikası, aktiflik | Ana konu |
| Kazanımlar | Kod, metin, kaynak, konu eşleştirmesi, çalışma süreleri | Konu / alt konu |
| Kaynak programlar | Ders adı, program kodu, sınav, isteğe bağlı sınıf, yıllar, belge ve kapsam dayanağı | Kök kayıt |
| Program konu grupları | Kaynak programdaki konu kodu, adı, kazanım sayısı, ders saati bütçesi | Kaynak program |

Her sekmede doğrudan **Ekle**, kayıt listesi ve düzenleme formu vardır. Üst kayıt filtresi ve kayıt araması bulunur. Üst kayıt sayısı 25'i geçtiğinde arama/sayfalama açılır; seçilmiş üst kayıt arama sayfası değişse de korunur. Alt konular ayrı listelenir. Kazanım oluştururken yalnız en alt düzeydeki konular sunulur.

Ders grubu bu ekranın kullanımından kaldırılmıştır. Eski `GroupId` ve grup tablosu uyumluluk için veritabanında tutulur; mevcut grupların otomatik olarak sınav olduğu varsayılmaz. Yeni ders bağlantısı sınav tablosundaki kod üzerinden kurulur. Daha önce sınavı boş olan okul dersleri silinmez; Dersler sekmesinde “Tüm kayıtlar” ile erişilebilir.

### Ders ekleme örneği

1. Sınav türleri sekmesinden mevcut bir türü kullanın veya örneğin `KPSS` koduyla yeni sınav ekleyin.
2. Dersler sekmesine geçin. Sınav filtresinden KPSS'yi seçin ve **Ders ekle** düğmesine basın. Formdaki üst kayıt seçicisinden de sınav seçilebilir.
3. Ders adını yazın. Sınıf isteğe bağlıdır. Öğrenci kurulumunda kullanılması için uygun ders programını seçin; henüz program yoksa daha sonra bağlanabilir.
4. İşlem gerekçesini yazıp kaydedin. **Kaydettikten sonra etkinleştir** işaretliyse oluşturma ve etkinleştirme ekrandan tek işlem olarak yürür.

Etkinleştirme üst kayıt pasif olduğu için başarısız olursa, oluşturulan kayıt formda açılır; kullanıcıya yeni bir kayıt oluşturması söylenmez. Gerekçe ve durum düğmesi formun görünür bölümündedir. Kullanılmış kayıtlarda kalıcı silme engelleri gösterilir; güvenli silme ayrı kapalı bölümdedir.

### Kazanım ve süre

Kazanımlar sekmesinden kaynak program ve programdaki konu grubu seçilir. Kaynak programın **Ad** alanı ilgili ders adıdır; örneğin Matematik. Ders, sınav ve varsa sınıf kapsamı konu ile uyuşmalıdır. Yeni konu eşleştirmesi ayrıca incelenip onaylanır.

Öğrenme, alıştırma ve kısa kontrol dakikaları girilir. Toplam 1–1440 dakikadır. Programdaki ders saati, bireysel çalışma dakikasından ayrı tutulur; konu bütçesi değişince kazanım dakikaları veya eski planlar kendiliğinden değiştirilmez.

Süre geçmişi **Önceki süreler ve değişiklik geçmişi** altında kapalıdır. Teknik sürüm numaraları geçmiş için korunur. Yeni süre kaydedildiğinde eski planların süre kimliği, ayrılmış dakika ve kazanım metni korunur. Kullanılan süre silinemez; yayımdan kaldırılabilir.

## Veritabanı ve servisler

### Sınav kataloğu

Yeni `coaching.study_catalog_exams`:

- `Id`: UUID birincil anahtar.
- `Code`: benzersiz, 1–10 karakter; A–Z, 0–9, alt çizgi. Bağlantıların devamlılığı için oluşturulduktan sonra değişmez.
- `Name`: değiştirilebilir görünen ad; en fazla 300 karakter.
- `DisplayOrder`: sıfır veya pozitif sıra.
- `IsActive`: gelecek seçimlerin kullanım durumu.

Başlangıç kayıtları LGS, TYT, AYT, YDT ve TDP'dir. Bunlar yeni kayıt eklemeye engel bir izin listesi değildir. Geçiş, mevcut ders/programlarda başka sınav kodu varsa onu da tabloya ekler. Dersler ve kaynak programlar `ExamCode → study_catalog_exams.Code` üzerinden `Restrict` yabancı anahtarla bağlanır. Sınavın ders veya program bağlantısı varsa kalıcı silme engellenir. Aktif dersleri olan sınav pasife alınamaz.

Yeni `GET /api/coaching/catalog/exams`, oturum açmış kullanıcılara sınav seçeneklerini veritabanından döndürür. Yeni yönetim ekranı ve eski katalog liste/düzenleme ekranındaki sabit seçenekler bu kaynağı kullanır. Konu arama servisi de yeni kayıtlı sınav kodlarını kabul eder; bilinmeyen kodları reddeder.

`/api/coaching-admin/curriculum-management` mevcut CRUD, taşıma, durum ve güvenli silme işlemleri `exams`, `programs`, `programTopics` türleriyle genişletildi. Konu listeleri `level=root`, `level=sub`, `level=leaf` ile ayrılır; sayfalama 25 kayıttır. Kurum kapsamı bu ortak müfredatı değiştiremez. Yazma işlemleri global yönetici kapsamı, içerik yönetimi yetkisi, güncellik kontrolü, işlem gerekçesi ve denetim kaydıyla korunur.

### Kaynak programlar

Kaynak programın yalnız LGS / 8. sınıf olma kısıtı kaldırıldı. Kayıtlı diğer sınavlar kullanılabilir. Sınıf isteğe bağlıdır; belirtilirse 1–12 aralığında olmalıdır. Böylece sınıftan bağımsız sınavlar için yapay bir “12. sınıf” değeri gerekmiyor. JSON aktarımında da sınıf boş olabilir.

Belge adresi, kapsam dayanağı HTTPS olmalıdır; belge SHA256 özeti zorunludur. Yönetici kaydı eklemek, belge içeriğinin resmî doğrulaması olarak sunulmaz. Kazanımı olan kaynak programın kapsamı/kaynağı değiştirilmek istendiğinde yeni program kaydı istenir. Program konu adları ve bütçeleri yönetilebilir; kullanılan program/konu silinemez.

Dersin başka sınava taşınması ders kimliğini korur. Bağlı kazanımın kaynak kapsamıyla çelişen değişiklik reddedilir. Ünite/konu taşımasının önceki kopyalama ve pasife alma davranışı sürer; eski planlar önceki kayıtları kullanır.

### Geçişler

1. `20261016195911_DynamicCurriculumExams`: sınav tablosu, başlangıç/backfill kayıtları, ders/program yabancı anahtarları, genel program kapsamı.
2. `20261016201006_GradeIndependentCurriculum`: kaynak program sınıfını nullable yapar; mevcut sınıf değerlerini korur.

Önceki canlıya alınmamış `20261016185534_CurriculumManagementHierarchy` geçişi bu ikisinden önce uygulanmalıdır. Identity hesaplarına veya çalışma kayıtlarına yönelik silme/güncelleme yoktur. Yeni kapsamlı program veya sınıfsız programlar bulunuyorsa eski şemaya geri geçiş veri kaybı doğuracak biçimde yapılmaz; geçişler bunu engeller. Dağıtımdan önce yedek ve üretim kopyası provası gerekir; bu çalışma kapsamında canlı veritabanına bağlanılmadı.

## Korunan kurallar ve kapsam

- Konu → alt konu yapısı iki düzeydir; döngü ve çapraz ders/ünite bağlantısı engellenir. Bunlar yönetici tarafından eklenebilir kayıt listelerinden farklı veri bütünlüğü kurallarıdır.
- Okul sınıfları isteğe bağlı 1–12 aralığındadır; ayrı bir sınıf türü yönetim tablosu eklenmedi.
- Kaynak program ve öğrenci ders kataloğunun `CurriculumVersion` kaydı farklı kavramlardır. Ders programı yayımlanmadıysa yeni dersin öğrenci kurulumunda hemen görünmesi beklenmez. Mevcut program oluşturma/yayınlama ekranına üstteki bağlantıdan ulaşılır.
- Bu değişiklik müfredat yönetimine odaklıdır. Öğrencinin çalışma amacı (LGS/YKS/okul/genel) ve sınav sonucu puanlama türleri ayrı iş kurallarıdır; bu teslimde genel tanım tablolarına çevrilmedi. Yeni bir sınavın hedef ve puanlama otomasyonu ayrıca o sınavın kurallarını gerektirir.
- Sınav kodları ve kullanılan kaynak kapsamı geçmiş bağlantıları korumak için serbestçe yeniden adlandırılmaz; görünen sınav adı düzenlenebilir.

## Doğrulama

| Kontrol | Sonuç / kanıt |
|---|---|
| Müfredat servis ve PostgreSQL testleri | 37 geçti; `artifacts/coaching-dynamic-postgres-complete.log` |
| Son genişletilmiş dinamik katalog testleri | 10 geçti; `artifacts/coaching-dynamic-extended.log` (37'lik kümenin içindedir) |
| Mevcut koçluk regresyonu | 694 geçti; `artifacts/coaching-dynamic-regression-final.log` |
| Arayüz testleri | 41 geçti; `artifacts/coaching-dynamic-ui-ready.log` |
| Masaüstü ve telefon tarayıcı senaryoları | 12 geçti; son paket kanıtı `artifacts/coaching-dynamic-e2e-complete.log` |
| Eski öğrenci/yönetici tarayıcı akışları | 4 geçti; `artifacts/coaching-dynamic-existing-plans-browser.log`; kazanım seçme, süreyi öğrenciye özel değiştirme, planı kaydetme ve çalışmayı tamamlama |
| .NET Release | 0 hata / 0 uyarı; `artifacts/coaching-dynamic-release.log` |
| EF model kontrolü | Bekleyen model değişikliği yok; `artifacts/coaching-dynamic-model-check.log` |
| Angular üretim derlemesi | Başarılı; `artifacts/coaching-dynamic-build-complete.log`; stil boyutu uyarıları mevcut, yönetim ekranı 4,41 kB ile 4 kB uyarı sınırının üzerinde, 8 kB hata sınırının altında |

37'lik kümede 8 gerçek PostgreSQL testi vardır; rasgele isimli geçici test veritabanları oluşturulup kapatılır. İlk geçişlerin tekrarlı uygulanması, önceki kodların backfill edilmesi, yeni sınavla arama, kaynak konu listeleme ve eski plan/çalışma verilerinin korunması doğrulandı. API/form testleri ile tarayıcı testleri birbirini tamamlar: tarayıcı testleri üretim paketini kontrollü HTTP yanıtlarıyla kullanır; canlı sunucuya yazmaz.

Yeni yönetim servisi dosyası satır kapsamı 441/456 (%96,7), sınav varlığı 20/21 (%95,2), seçenek kontrolcüsü 3/3 (%100). Bunlar uygulamanın tamamının kapsama oranı değildir. Kapsam çıktısı `artifacts/coaching-dynamic/ready-coverage/` altındadır.

Ekran görüntüleri: `artifacts/coaching-dynamic/lessons-desktop.png`, `lessons-mobile.png`.

Kod incelemesinde yetki/kapsam sınırları, kayıtlı sınav zorunluluğu, yabancı anahtarlar, eşzamanlı düzenleme, oluşturma sonrası etkinleştirme hatası ve kaynak/süre geçmişinin korunması gözden geçirildi. Başka işlere ait çalışma ağacı değişiklikleri bu teslimin commitlerine dahil edilmedi.
