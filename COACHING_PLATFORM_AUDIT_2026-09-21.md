# Koçluk Platformu Teknik, Ürün ve Bilimsel Olgunluk Raporu

**İnceleme tarihi:** 21 Eylül 2026  
**Kapsam:** `coaching-service`, koçlukla ilişkili Identity/Notification akışları, Angular yönetim ve kullanıcı portalları, testler, CI/CD, ürün belgeleri ve güncel pazar/akademik kaynaklar  
**Rapor türü:** Kod tabanı ve çalıştırılabilir doğrulama temelli durum tespiti; hukuk veya klinik uygunluk görüşü değildir.

## 1. Yönetici özeti

Platform, basit bir prototip değildir. Ayrı bir koçluk bounded context'i, rol ve kurum kapsamı, hedef/ödev/sınav/seans modelleri, öğrenci-öğretmen-veli arayüzleri, bildirim olayları, denetim kaydı, idempotency, dosya eki tarama ve kurumsal raporlama altyapısı vardır. Teknik temel, erken aşama yerli koçluk uygulamalarından daha güçlü; uluslararası olgun koçluk SaaS ürünlerinden ise ürün işletimi, etik yaşam döngüsü ve kanıt üretimi yönlerinde geridedir.

Bugünkü kanıta dayalı değerlendirme:

| Boyut | Puan | Durum | Kısa hüküm |
|---|---:|---|---|
| Teknik mimari | 78/100 | Güçlü beta | Servis sınırları ve güvenlik yaklaşımı iyi; sürüm uyumu ve üretim kanıtı eksik |
| Kod kalitesi ve test | 72/100 | Orta-iyi | 173 frontend testi geçti; backend seçkisinde iki gerçek uyumsuzluk hatası bulundu |
| Güvenlik ve mahremiyet | 68/100 | Orta | RBAC/tenant/MFA/audit güçlü; CORS, veri yaşam döngüsü ve çocuk güvenliği eksik |
| Ürün kapsamı | 61/100 | İşlevsel MVP+ | Akademik takip güçlü; profesyonel koçluk operasyonları ve self-service zayıf |
| Bilimsel dayanak | 52/100 | Kavramsal olarak uyumlu, etki kanıtı yok | Hedef ve izleme doğru yönde; davranış protokolü ve deneysel doğrulama yok |
| Operasyonel/üretim hazırlığı | 64/100 | Kontrollü pilot | CI/observability var; gerçek ortam SLO, restore, soak ve DR kanıtı tamam değil |
| **Genel ağırlıklı olgunluk** | **66/100** | **Pilot için uygun, genel kullanıma “%100 hazır” değil** | Önce P0 teknik/etik kapılar, sonra kontrollü pilot |

**En önemli sonuç:** Platformun ana eksiği daha fazla CRUD ekranı değildir. Eksik olan; koçluk ilişkisinin açık sözleşmesi, öğrenci öz-düzenleme döngüsü, koç kalite sistemi, ölçüm bilimi, çocuk koruma/KVKK yaşam döngüsü ve üretim güvenilirliğinin kanıtlanmasıdır.

## 2. İnceleme yöntemi ve kanıt seviyesi

İnceleme dört kanıt katmanıyla yapıldı:

1. **Kod kanıtı:** Domain modelleri, controller/handler'lar, yetkilendirme, veri modeli, istemci rotaları ve CI iş akışları okundu.
2. **Çalıştırma kanıtı:** Frontend test/build ve koçluk odaklı backend test seçkisi çalıştırıldı.
3. **Belge kanıtı:** Mevcut progress/implementation/research belgeleri güncel kodla karşılaştırıldı.
4. **Dış kıyas:** Güncel rakiplerin resmi özellik sayfaları, ICF etik kodu, KVKK yayınları ve hakemli araştırmalar incelendi.

“Var” ifadesi kodda görülen yeteneği; “doğrulandı” ifadesi çalıştırılan testi; “kanıt yok” ifadesi ise özelliğin etkisiz olduğunu değil, bu depoda güvenilir ölçüm bulunmadığını anlatır.

## 3. Mevcut ürün envanteri

### 3.1 Uygulanmış çekirdek yetenekler

- Bireysel/grup seansı planlama, güncelleme, iptal, katılım ve notlar
- Öğrenciye/gruba ödev, kitap ödevi ayrıntıları, teslim, puanlama ve öğretmen geri bildirimi
- Dosya eki, boyut/tür politikası, yerel/MinIO depolama ve ClamAV tarama adaptörü
- Akademik hedef, hedef tarihi/puanı, ilerleme yüzdesi ve tamamlama
- Sınav ve sonuç yönetimi; doğru/yanlış/boş, ders kırılımı, sıralama ve öğretmen notu
- Öğrenci ilerleme özeti, kurum karşılaştırması ve kural tabanlı erken uyarı
- Öğretmen ve öğrenci için iCalendar feed'i
- Öğrenci, öğretmen, veli ve sistem/kurum yöneticisi görünümleri
- SignalR/in-app bildirim ve RabbitMQ/MassTransit olay akışları
- Admin MFA, permission tabanlı yönetim ve append-only denetim kaydı
- Yazma işlemlerinde idempotency ve outbox yaklaşımı

### 3.2 Ürün olarak eksik veya zayıf yetenekler

- Koç profili, yetkinlik/sertifika doğrulama, uzmanlık ve kapasite yönetimi
- Koç-öğrenci eşleştirme, tercih, görüşme ve yeniden eşleştirme akışı
- Koçluk paketi/programı, seans hakkı, üyelik, ödeme, fatura ve e-sözleşme
- Google/Outlook takvim çift yönlü senkronizasyonu ve otomatik video bağlantısı
- Öğrencinin self-service seans rezervasyonu ve iptal politikası
- Güvenli koç-öğrenci mesajlaşması; moderasyon/escalation akışı
- Intake formu, başlangıç değerlendirmesi, periyodik pulse/CSAT ve seans değerlendirmesi
- Hedef milestone'ları, eylem planı, alışkanlık/rutin, günlük/haftalık öz-izleme ve yansıtma
- Kaynak/şablon kütüphanesi ve yapılandırılmış koçluk programları
- Koç süpervizyonu, kalite puanı, vaka yükü, hizmet süresi ve sonuç adaleti panoları
- Veri dışa aktarma, koçluk ilişkisinden ayrılma, veri silme/anonimleştirme ve saklama politikası
- Çocuk koruma bildirimi, risk/escalation, veli–öğrenci–koç gizlilik sınırları ve şikâyet yönetimi
- Native mobil uygulama ve çevrimdışı/erişilebilirlik kanıtı

## 4. Rakip platformlara göre durum

Karşılaştırma “aynı pazarda birebir rakip” iddiası değildir. Uluslararası ürünler, koçluk yazılımında kullanıcı beklentisinin ulaştığı seviyeyi gösteren referanslardır.

| Yetkinlik | Bu platform | Coaching.com | CoachAccountable | Paperbell | Değerlendirme |
|---|---|---|---|---|---|
| Seans ve takvim | Temel seans + ICS | Gelişmiş planlama | Tekrarlı randevu + takvim | Self-service + takvim/video | Geride |
| Hedef/aksiyon | Hedef ve yüzde | Hedef/milestone/tema | Action plan, metric, routine | Temel istemci akışı | Orta |
| Ödev/görev | Akademik ödev güçlü | Assignment | Worksheet/project/routine | Form ve içerik | Akademik nişte güçlü |
| Sınav/akademik veri | Güçlü ve özgün | Genel koçluk odaklı | Genel metrik | Yok/sınırlı | Belirgin farklılaştırıcı |
| Öğrenci/veli portalı | Var | Client portal | Client portal | Client portal | Güçlü; veli rolü avantaj |
| Form/anket/intake | Belirgin değil | Gelişmiş | Fillable form/worksheet | Otomatik intake | Büyük açık |
| Mesajlaşma | Bildirim var, sohbet yok | Var | Takım iletişimi | E-posta otomasyonu | Açık |
| Sözleşme/ödeme/paket | Yok | Billing/invoice | Contract/payment/package | Temel güçlü yönü | İş modeli açığı |
| Koç havuzu/eşleştirme | Yok | Enterprise coach pool/matching | Pairing/team | Tek koç odaklı | Kurumsal büyüme açığı |
| Analitik | Progress, kıyas, erken uyarı | 10+ standart dashboard | Aktivite/sonuç raporları | Operasyonel | Temel var, derinlik eksik |
| Etik/mahremiyet akışı | Teknik güvenlik var | Enterprise kontroller | Granüler roller | Sözleşme/portal | Yaşam döngüsü açığı |

Rakip kaynakları:

- [Coaching.com resmi özellik sayfası](https://www.coaching.com/coaching-management-software): seans, not/kaynak, hedef, form, grup koçluğu, portal, analitik, faturalama ve enterprise yetkiler.
- [Coaching.com raporlama dokümanı](https://help.coaching.com/en/articles/339541-coaching-com-reporting-analytics-insights-standard-dashboards-data-sources): engagement, session, coach, coachee, goal, assignment ve matching dashboard'ları.
- [CoachAccountable enterprise özellikleri](https://www.coachaccountable.com/enterprise): action plan, metrics, worksheet, routine, contract, payment, course ve takım yönetimi.
- [Paperbell resmi zamanlama sayfası](https://paperbell.com/scheduling-software/): self-service rezervasyon, ödeme, sözleşme, paket, grup koçluğu ve portal.

### 4.1 Pazardaki konumlandırma önerisi

Platformu “her koça her şeyi yapan genel koçluk SaaS” olarak konumlandırmak zayıf olur; bu kategoride ödeme, sözleşme, takvim ve CRM açığı büyüktür. Daha savunulabilir konum:

> **K-12/LGS/YKS kurumları için akademik koçluk işletim ve ölçüm platformu; hızlı okuma verisi, sınav performansı, veli görünürlüğü ve kurum çapı erken uyarıyı tek yerde birleştirir.**

Bu konumda sınav/ödev/veli/kurum analitiği rakiplere karşı avantajdır. Ancak “akademik başarıyı artırır” iddiası bugün kanıtlanmış değildir; önce pilot etki çalışması gerekir.

## 5. Bilimsel ve akademik değerlendirme

### 5.1 Bilimle uyumlu mevcut unsurlar

Platformun hedef belirleme, planlama, görev tamamlama, geri bildirim ve ilerleme görünürlüğü; öz-düzenlemeli öğrenme (self-regulated learning, SRL) literatürüyle genel olarak uyumludur. 2025 sistematik derleme; hedef belirleme, zaman yönetimi, izleme, öz-yansıtma ve yardım aramayı temel SRL stratejileri arasında saymaktadır ([ERIC kaydı](https://eric.ed.gov/?id=EJ1477329)). Üniversite öğrencilerindeki SRL eğitimlerinin performans, strateji ve özellikle öz-yeterliği geliştirdiğini bildiren bir meta-analiz de vardır ([Contemporary Educational Psychology, 2021](https://doi.org/10.1016/J.CEDPSYCH.2021.101976)).

Öz-izleme araçları üzerine meta-analiz, akademik sonuçlarda yaklaşık orta büyüklükte etkiler bildirmekle beraber sonuçların yaş, süre, geri bildirim ve uygulama tasarımına göre heterojen olduğunu vurgular ([Educational Psychology Review](https://link.springer.com/article/10.1007/s10648-023-09718-4)). Bu nedenle yalnızca bir progress bar koymak bilimsel müdahale sayılmaz.

### 5.2 Bilimsel açıdan temel boşluklar

1. **Teori → özellik eşlemesi tanımlı değil.** Her ekranın hangi değişim mekanizmasını hedeflediği yazılmamış: hedef netliği, öz-izleme, geri bildirim, uygulama niyeti, yardım arama, yansıtma vb.
2. **“Doz” tanımlı değil.** Kaç hafta, kaç seans, hangi sıklık, hangi öğrenci grubunda hangi protokol uygulanacağı yok.
3. **Hedef kalitesi ölçülmüyor.** Yalnız başlık/hedef tarih/ilerleme yüzdesi; milestone, davranışsal ölçüt, başlangıç değeri, kanıt kaynağı ve güven düzeyi yok.
4. **İlerleme çoğunlukla öznel yüzde.** Öğrencinin veya koçun girdiği yüzde ile sınav/ödev/katılım verisinin ayrımı ve provenance'ı açık değil.
5. **Yansıtma döngüsü eksik.** Planla → uygula → izle → yansıt → yeniden planla döngüsünün son iki adımı ürünleşmemiş.
6. **Geçerli ölçekler ve ölçüm planı yok.** Öz-yeterlik, akademik öz-düzenleme, aidiyet/iyi oluş gibi sonuçlar için yaşa/dile uygun, lisans ve geçerlik durumu belli araçlar seçilmemiş.
7. **Etki değerlendirmesi yok.** Baseline, karşılaştırma grubu, ön-kayıtlı birincil sonuç, kayıp veri planı ve takip ölçümü bulunmuyor.
8. **Erken uyarı validasyonu yok.** Kural tabanlı uyarı yararlı bir operasyon aracıdır; duyarlılık, özgüllük, yanlış pozitif, alt grup adaleti ve müdahale sonucu ölçülmeden “risk tahmini” diye pazarlanmamalı.
9. **Koç etkisi karıştırıcı değişken.** Öğrenci başlangıç düzeyi ve vaka karması düzeltilmeden koçları ham başarı artışıyla sıralamak bilimsel ve etik olarak sorunludur.

### 5.3 Kaçınılması gereken bilimsel özellikler

- Mevcut eski araştırma belgesinde önerilen **VARK/öğrenme stili eşleştirmesi** uygulanmamalıdır. Tercih sorulabilir; ancak öğretimi “stile göre eşleştirmenin” etkililiğini destekleyen yeterli kanıt yoktur ([Frontiers in Psychology değerlendirmesi](https://www.frontiersin.org/journals/psychology/articles/10.3389/fpsyg.2017.00444/pdf)).
- “AI öğrenciyi tanır ve başarıyı garanti eder”, “koçluk puanı yükseltir” gibi nedensel iddialar kontrollü çalışma olmadan kullanılmamalıdır.
- Risk skoru tanı, klinik değerlendirme veya psikolojik profil gibi sunulmamalıdır.

### 5.4 Önerilen bilimsel ürün modeli

Her öğrenci için görünür bir SRL döngüsü kurulmalıdır:

1. **Tanıla:** Akademik geçmiş, görev davranışı, öğrenci hedefi ve engeller.
2. **Ortak hedef koy:** Öğrenci katılımıyla ölçülebilir sonuç + davranış hedefi.
3. **Eylem planla:** Haftalık küçük adımlar, zaman/mekân planı ve beklenen engel.
4. **Uygula ve izle:** Sistem verisi + öğrenci check-in'i; kaynağı etiketli ölçüm.
5. **Geri bildirim ver:** Koç geri bildirimi, çözüm odaklı soru ve gerektiğinde destek.
6. **Yansıt:** Ne işe yaradı, ne engelledi, güven/öz-yeterlik nasıl değişti?
7. **Uyarlayıp yeniden planla:** Hedefi kolayca artırma/azaltma değil, gerekçeli revizyon.

Bu akış, platformu CRUD takip sisteminden davranış değişikliği ve öğrenme bilimi aracına taşır.

## 6. Etik, çocuk güvenliği ve KVKK

ICF'nin 1 Nisan 2025'te yürürlüğe giren etik kodu; koçluk başlamadan roller, sorumluluklar, gizlilik ve finansal koşulların açık anlaşmasını; kayıtların güvenli saklanması/imhasını ve teknoloji/AI kullanılırken de etik yükümlülükleri ister ([ICF Code of Ethics](https://coachingfederation.org/credentialing/coaching-ethics/icf-code-of-ethics/)). Platformda teknik erişim kontrolü güçlü olsa da koçluk anlaşması ve bilgi paylaşımı sınırları ürün akışı olarak görünmüyor.

KVKK'nın çocuk verileri yayınları çocukların çevrimiçi hizmetlerde riskleri yetişkinler kadar değerlendiremeyebileceğini, çocuk yüksek yararının ve yaşa uygun bilgilendirmenin önemini vurgular ([KVKK seçilmiş düzenlemeler](https://kvkk.gov.tr/SharedFolderServer/CMSFiles/95d1f4bc-de91-4703-82c1-8757387a3850.pdf)). Uzaktan eğitim duyurusu da ses/görüntü ve yurt dışı bulut aktarımı riskine dikkat çeker ([KVKK duyurusu](https://www.kvkk.gov.tr/Icerik/6723/Uzaktan-Egitim-Platformlari-Hakkinda-Kamuoyu-Duyurusu)).

Üretim öncesi zorunlu ürün kontrolleri:

- Yaşa uygun öğrenci aydınlatması + veli/yasal temsilci akışı + kurum/veri sorumlusu rolleri
- Koçluk anlaşması: amaç, kapsam, gizlilik, velinin/kurumun göreceği alanlar, fesih, şikâyet
- Öğrenci özel notu / koç özel notu / veliye açık not / kurum raporu için ayrı görünürlük sınıfları
- Veri envanteri, işleme şartı, saklama süresi, silme/anonimleştirme ve erişim talebi akışı
- Dosya, mesaj, seans notu ve risk olayları için çocuk koruma/escalation prosedürü
- Kendine/başkasına zarar, istismar veya yasa dışı faaliyet sinyalinde açık insan müdahalesi
- AI eklenirse açık bildirim, insan denetimi, açıklanabilir öneri, eğitim verisi dışlama ve DPIA benzeri etki analizi
- Yurt dışı hizmet sağlayıcıları için aktarım mekanizması ve tedarikçi sözleşmeleri

## 7. Teknik inceleme

### 7.1 Güçlü yönler

- DDD/CQRS katmanları ve aggregate davranışları basit CRUD'dan daha iyi sınırlar kuruyor.
- Identity, Coaching ve Notification sorumlulukları ayrılmış; kullanıcı kişisel verisinin Coaching'e kopyalanmaması doğru yön.
- Kurum/öğrenci erişimi istemciden gelen tenant kimliğine kör güvenmek yerine Identity ile doğrulanıyor.
- Yönetici mutasyonlarında SystemAdmin + MFA + permission ve audit kombinasyonu güçlü.
- MassTransit EF outbox, retry ve idempotency olay/yazma güvenilirliğini artırıyor.
- Dosya eki için tarama ve nesne depolama adaptörleri güvenlik açısından olgun bir tercih.
- Migration-only çalışma modu rolling deployment'ta migration yarışını azaltıyor.
- OpenTelemetry, Prometheus/Grafana/Alertmanager/Tempo ve CI iş akışları mevcut.
- API versioning ve sayfalama ortak sözleşmeleri var.

### 7.2 Doğrulamada bulunan sorunlar

#### P0 — .NET 10 / EF Core 9 çalışma zamanı uyumsuzluğu

Koçluk test seçkisinde karşılaştırmalı rapor ve erken uyarı sorguları şu hata ile kırıldı:

`ReadOnlySpan<Guid>` dönüş türünün expression interpreter generic constraint'ini ihlal etmesi.

Kod izleri:

- `Coaching.Infrastructure/Repositories/Repositories.cs:566`
- `Coaching.Infrastructure/Repositories/Repositories.cs:686`

Servis `net10.0` hedefliyor; EF Core/Npgsql paketleri 9.0.2. Derleme çıktısı üretildi ancak bu iki sorgu çalışma zamanında hata verdi. Çözüm, tüm EF ailesini hedef framework ile uyumlu tek majör sürüme yükseltmek ve `IReadOnlyCollection<Guid>.Contains` ifadelerini provider üzerinde gerçek PostgreSQL testiyle doğrulamaktır.

#### P0 — Entegrasyon kapısı yerel ortamda yeşil değil

Koçluk filtresiyle 143 test çalıştı: **130 geçti, 13 başarısız**. En az iki başarısızlık yukarıdaki gerçek runtime problemi; diğerlerinin çoğu Docker engine kapalı olduğu için Testcontainers fixture'larının kurulamamasından kaynaklandı. Test altyapısının “Docker yoksa kırık” olması tek başına ürün hatası değildir; fakat release kanıtı olarak yeşil CI sonucu saklanmalıdır.

#### P1 — CORS `AllowAnyOrigin/Method/Header`

`Coaching.API/Program.cs` içindeki açık CORS politikası üretimde gereksiz geniştir. Gateway dışı servis erişimi ağ seviyesinde kapalı olsa bile savunma derinliği için environment bazlı allowlist kullanılmalıdır.

#### P1 — Health endpoint'leri aynı sinyali veriyor

`/health`, `/health/ready`, `/health/live` aynı health-check kümesini map ediyor. Liveness'ın veritabanına bağlı olması, geçici DB arızasında pod/container restart döngüsü yaratabilir. Liveness sadece proses; readiness DB/RabbitMQ/Identity kritik bağımlılıklarını ölçmelidir.

#### P1 — Paket sürümü tutarlılığı

`net10.0` projelerinde `Microsoft.EntityFrameworkCore`, relational, design ve Npgsql 9.0.2; bazı `Microsoft.Extensions.*` 9.x paketleri var. Framework/package compatibility matrisi CI kapısı yapılmalı ve merkezi paket yönetimi düşünülmelidir.

#### P1 — Koçluk veri yaşam döngüsü yok

Audit/log retention parçaları var; ancak seans notu, ödev eki, sınav sonucu, hedef ve bildirim için koçluk-domain saklama/silme/anonimleştirme politikası kodda görünmüyor.

#### P2 — Controller hata davranışı tekrarlı

Controller'larda çok sayıda tekrar eden `try/catch` ve farklı şekilli `BadRequest/Forbidden/NotFound` yanıtı var. Ortak problem-details altyapısı kullanılmasına rağmen yerel catch blokları sözleşme sapması riski yaratıyor.

#### P2 — Derleme bütçesi uyarısı

Admin production build başarılı; ancak `speed-reading-catalog` component style bütçesi 4 KB sınırını 3.31 KB aşıyor. Koçluk modülünü doğrudan engellemez, fakat performans bütçesinin uyarıda kalabildiğini gösterir.

#### P2 — Eski belgeler güvenilir değil

`services/coaching-service/PROGRESS_REPORT.md` testleri `%0`, API'yi `%95` gösteriyor; güncel kod ve testlerle çelişiyor. Eski araştırma raporu VARK gibi uygulanmaması gereken öneriler ve artık geçersiz teknoloji/takvim/maliyet varsayımları içeriyor. Belgeler “arşiv/obsolete” olarak işaretlenmeli veya güncellenmelidir.

### 7.3 Çalıştırılan doğrulamalar

| Kontrol | Sonuç |
|---|---|
| Admin Angular testleri | **173/173 geçti**, 38 test dosyası |
| Admin production SSR/browser build | **Başarılı**, 12 route prerender; 1 style budget uyarısı |
| Koçluk filtreli backend testleri | **130/143 geçti**; 2 gerçek runtime uyumsuzluğu, kalanların çoğu Docker unavailable |
| Backend compile | Test çalışması bütün ilgili assembly'leri `net10.0` için üretti |
| Ayrı `dotnet build` | Sandbox/MSBuild temp davranışında sonuçsuz exit 1; hata/uyarı üretmedi, bu yüzden bağımsız kanıt sayılmadı |

## 8. Önceliklendirilmiş teknik borç

Formül: **(Etki + Risk) × (6 − Efor)**; her boyut 1–5, yüksek puan önce.

| Sıra | Borç | Etki | Risk | Efor | Puan | İş gerekçesi |
|---:|---|---:|---:|---:|---:|---|
| 1 | EF/.NET sürüm uyumu ve kırık rapor sorguları | 5 | 5 | 2 | 40 | Erken uyarı/kurum raporu runtime'da kırılıyor |
| 2 | Koçluk anlaşması, çocuk koruma ve veri yaşam döngüsü | 5 | 5 | 3 | 30 | Hukuki/etik ve itibar riski; pilot ön koşulu |
| 3 | CI'da yeşil, saklanan entegrasyon/release kanıtı | 5 | 5 | 3 | 30 | “Production ready” iddiasının temel kanıtı |
| 4 | CORS ve health check ayrımı | 4 | 4 | 2 | 32 | Güvenlik ve operasyonel kararlılık |
| 5 | Bilimsel ölçüm protokolü ve veri sözlüğü | 5 | 4 | 3 | 27 | Ürün vaadini kanıtlanabilir hale getirir |
| 6 | Milestone/check-in/reflection SRL döngüsü | 5 | 4 | 4 | 18 | Koçluk etkisini taşıyan çekirdek ürün eksikliği |
| 7 | Takvim/self-service/video entegrasyonu | 4 | 3 | 3 | 21 | Operasyon yükünü ve no-show'u azaltır |
| 8 | Form/intake/pulse/CSAT sistemi | 4 | 3 | 3 | 21 | Kişiselleştirme ve kalite ölçümü sağlar |
| 9 | Koç profili, kalite ve eşleştirme | 4 | 4 | 4 | 16 | Kurumsal ölçekte koç operasyonunun temeli |
| 10 | Controller hata sözleşmesi sadeleştirmesi | 3 | 3 | 2 | 24 | Tutarlılık ve bakım maliyeti |

## 9. “%100 tamam” tanımı

Yazılımda mutlak %100 yoktur. Bu raporda `%100`, aşağıdaki sürüm kabul kriterlerinin tamamının kanıtlı olmasıdır:

### Ürün tamamlanmışlık kapısı

- Öğrenci, veli, koç/öğretmen, kurum yöneticisi ve sistem yöneticisi kritik yolculukları uçtan uca çalışır.
- Intake → anlaşma/onam → hedef → eylem → check-in → seans → yansıtma → rapor → kapanış akışı eksiksizdir.
- Takvim/self-service rezervasyon, hatırlatma ve yeniden planlama çalışır.
- Koç profili/eşleştirme ve koç kalite süreci vardır.
- Ücretli model seçilecekse paket/ödeme/fatura/sözleşme; kurum lisans modeli seçilecekse lisans/kota/teklif akışı tamamdır.

### Bilimsel kapı

- Teori-of-change, ölçüm sözlüğü ve müdahale protokolü sürümlenmiştir.
- En az bir kontrollü pilot ön-kayıtlı sonuçlarla tamamlanmıştır.
- Birincil sonuç, takip süresi, kayıp veri ve alt grup analizi raporlanmıştır.
- Erken uyarı için calibration, precision/recall, yanlış pozitif ve adalet raporu vardır.
- Pazarlama iddiaları yalnız ölçülen etki sınırında yazılmıştır.

### Teknik/operasyon kapısı

- Tüm unit/integration/contract/E2E testleri release commit'inde yeşildir.
- Yük, spike, 2–8 saat soak, bağımlılık arızası ve restore testleri hedefleri karşılar.
- SLO/SLI, alarm, runbook, on-call ve rollback tatbikatı vardır.
- Kritik/yüksek dependency ve container açığı yoktur veya risk kabulü belgelidir.
- RPO/RTO hedefleri gerçek restore tatbikatıyla kanıtlanmıştır.

### Güvenlik/etik kapısı

- Threat model, tenant negatif test matrisi, yetki matrisi ve dış pentest tamamdır.
- KVKK veri envanteri, işleme şartı, saklama/imha ve ilgili kişi talepleri işler.
- Çocuk yüksek yararı, yaşa uygun metin, veli onayı ve safeguarding/escalation süreçleri işler.
- Koçluk anlaşması ve not görünürlük sınırları kullanıcıya açıktır.
- AI varsa insan denetimi, açıklama, itiraz ve veri kullanım kontrolleri vardır.

## 10. Tamamlama yol haritası

Süreler, 1 ürün yöneticisi/alan uzmanı, 2 backend, 2 frontend, 1 QA, part-time DevOps, KVKK hukuk desteği ve araştırma danışmanı varsayımıyla yaklaşık verilmiştir. Tek kişiyle süre 2–3 katına çıkabilir.

### Faz 0 — Go/no-go düzeltmeleri (1–2 hafta)

**Amaç:** Pilot öncesi kırık ve yüksek riskli noktaları kapatmak.

- EF Core/Npgsql/.NET sürüm hizalaması; iki kırık query için PostgreSQL regression test
- Tüm koçluk backend testlerini Docker'lı CI'da yeşile getirme
- Production CORS allowlist
- Liveness/readiness ayrımı; RabbitMQ ve Identity dependency readiness tasarımı
- Eski progress/research belgelerini obsolete olarak işaretleme
- Koçluk verisi sınıflandırma ve çocuk güvenliği çalışma grubu

**Çıkış kriteri:** release commit'i yeşil; P0 runtime hatası yok; pilot ortamında health/rollback çalışıyor.

### Faz 1 — Etik ve kanıt temeli (2–4 hafta)

**Amaç:** Platformun neyi, kimin için ve hangi sınırlarla yaptığını netleştirmek.

- Koçluk anlaşması/onam versiyonlama ve kabul kaydı
- Yaşa uygun öğrenci aydınlatması, veli/kurum rolleri, not görünürlük sınıfları
- Saklama/imha, export, hesap/ilişki kapatma ve anonimleştirme akışları
- Safeguarding risk kaydı, yetkili insan escalation ve vaka audit'i
- Theory-of-change, outcome/data dictionary ve koçluk protokolü v1
- Bilim kurulu/alan uzmanı incelemesi; VARK önerisinin kaldırılması

**Çıkış kriteri:** KVKK/etik kontrol listesi imzalı; her veri alanının sahibi, amacı, erişimi ve süresi belli.

### Faz 2 — Bilim-temelli çekirdek ürün (4–6 hafta)

**Amaç:** Takip sistemini gerçek koçluk/SRL döngüsüne dönüştürmek.

- Baseline ve intake form builder
- Sonuç hedefi + davranış hedefi + milestone + kanıt kaynağı
- Haftalık check-in, öz-izleme, engel ve yardım talebi
- Seans hazırlığı ve seans sonrası öğrenci yansıtması
- Plan revizyon geçmişi ve “neden değişti” kaydı
- Yaşa uygun, lisans/geçerlik kontrolü yapılmış kısa ölçümler
- Koç dashboard'unda müdahale önerisi; otomatik karar değil, gerekçeli karar desteği

**Çıkış kriteri:** uçtan uca SRL döngüsü E2E testli; metriklerin provenance'ı görünür.

### Faz 3 — Pazar paritesi ve koç operasyonu (4–8 hafta)

**Amaç:** Günlük operasyonu rakip seviyesine yaklaştırmak.

- Koç profili, sertifika/uzmanlık, kapasite ve müsaitlik
- Koç-öğrenci eşleştirme ve yeniden eşleştirme
- Self-service rezervasyon, iptal/no-show politikası
- Google/Outlook takvim OAuth, token vault ve otomatik Meet/Teams/Zoom/Jitsi bağlantısı
- Güvenli mesajlaşma veya net biçimde sınırlandırılmış iletişim entegrasyonu
- Kaynak/şablon/form kütüphanesi
- Seans/engagement/goal/assignment/CSAT/coach kalite dashboard'ları

**Çıkış kriteri:** pilot kurum koç operasyonunu harici tablo/mesajlaşma olmadan yürütebilir.

### Faz 4 — Ticari model (3–6 hafta, iş modeline bağlı)

- B2C ise paket, seans hakkı, ödeme, iade, fatura ve e-sözleşme
- B2B ise kurum lisansı, kullanıcı/koç kotası, sözleşme dönemi, kullanım ve SLA raporu
- Dönüşüm hunisi, onboarding, activation, retention ve churn ölçümü
- Destek/şikâyet/incident SLA'ları

**Çıkış kriteri:** seçilen gelir modeli baştan sona muhasebeleştirilebilir ve denetlenebilir.

### Faz 5 — Kontrollü pilot ve bilimsel değerlendirme (8–16 hafta)

- 2–5 kurum, tanımlı örneklem ve ön-kayıtlı protokol
- Baseline + müdahale + mümkünse karşılaştırma grubu + 4/8/12 haftalık takip
- Akademik sonuç yanında süreç ölçütleri: devam, görev tamamlama, hedef kalitesi, öz-yeterlik
- Kullanılabilirlik, no-show, destek yükü ve koç sadakati
- Alt grup ve adalet analizi; zarar/istenmeyen etki kaydı
- Sonuçlara göre ürün/protokol revizyonu

**Çıkış kriteri:** etki ve güvenlik raporu; hangi öğrenci grubunda ne kadar güvenle işe yaradığı belli.

### Faz 6 — Production GA sertifikasyonu (2–4 hafta)

- Bağımsız pentest ve düzeltmeler
- Load/spike/soak/chaos ve backup restore tatbikatı
- SLO: ör. aylık erişilebilirlik, p95 latency, hata oranı, event lag, bildirim gecikmesi
- Blue/green veya canary release, rollback ve migration recovery
- Erişilebilirlik WCAG 2.2 AA incelemesi
- DPA/tedarikçi envanteri, operasyon runbook'u ve destek eğitimleri

**Çıkış kriteri:** ürün, bilim, güvenlik ve operasyon kapılarının tamamına bağlı resmi go/no-go kararı.

## 11. İlk 30 gün için kesin eylem listesi

1. EF/.NET uyumsuzluğunu düzelt ve iki query regression testini yeşile getir.
2. CI'da Docker'lı tam koçluk entegrasyon paketi sonucu üret ve artifact olarak sakla.
3. Production CORS ile liveness/readiness ayrımını düzelt.
4. Koçluk anlaşması, not görünürlüğü, veli/öğrenci mahremiyeti ve safeguarding kararlarını yazılı ürün gereksinimine dönüştür.
5. Eski `%85` progress raporunu kaldırma değil, tarihsel/arşiv olarak etiketleme; tek güncel durum belgesi belirleme.
6. Theory-of-change ve ölçüm sözlüğü workshop'u yap; her metriğe kaynak ve yorum sınırı ekle.
7. Milestone + haftalık check-in + reflection için UX prototipi ve kabul testleri hazırla.
8. 2 pilot kurum ve 10–20 koçla discovery; mevcut harici araç kullanımını ve en pahalı iş akışını ölç.

## 12. Son karar

**Bugün:** Platform teknik olarak ciddi ve işlevsel bir MVP+ / güçlü beta seviyesindedir. Kontrollü, düşük hacimli ve yakından izlenen pilot yapılabilir; ancak P0 sürüm uyumsuzluğu giderilmeden karşılaştırma/erken uyarı özelliği güvenilir kabul edilmemelidir.

**Bilimsel olarak:** Tasarımın hedef–görev–geri bildirim omurgası literatürle uyumludur fakat ürünün öğrenci başarısına neden olduğu henüz gösterilmemiştir. “Bilim-temelli özellikler var” denebilir; “bilimsel olarak etkinliği kanıtlandı” denemez.

**Pazar açısından:** Akademik sınav/ödev/veli/kurum bağlamı güçlü farklılaştırıcıdır. Genel koçluk SaaS'ları karşısında rezervasyon, formlar, koç profili/eşleştirme, sözleşme/ödeme ve koç kalite yönetimi eksiktir.

**Öneri:** Önce güvenli ve ölçülebilir akademik koçluk çekirdeğini tamamlayın; genel CRM/ödeme özelliklerini seçilen B2B/B2C iş modeline göre sonra ekleyin. En yüksek yatırım getirisi, “daha çok ekran” değil; koçluk protokolü + öğrenci öz-düzenleme döngüsü + güvenilir ölçüm + etik veri yaşam döngüsüdür.
