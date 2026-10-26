# TYT, AYT ve İngilizce YDT aktarımı — 19 Ekim 2026

## Sonuç

Ortak kazanım/kapsam modeli, TYT, AYT ve yalnız İngilizce YDT verileri canlıya alındı. Paket onaylı eşleştirmeler ve yayımlanmış çalışma süresi tahminleri içeriyor. Arapça ders, program ve öğrenci dil bağlamı sayıları sıfırdı; silinecek Arapça kaydı bulunmadı. İngilizce dışındaki YDT seçimleri de sunucuda engelleniyor.

Canlı sürüm: `coaching-yks-20261019-r3`. Geçiş: `20261018222725_YksExamPreparationCatalog`.

Son canlı regresyon: [24 öğrenci/öğretmen manuel ve otomatik yayımlama senaryosu](final-plan-regression-20261019.md) tamamlandı. Sözel ders kapsamı düzeltmesiyle portal sürümü `coaching-wizard-coverage-20261019` oldu; backend ve katalog sürümü yukarıdaki R3 olarak kaldı.

## Kaynak ve geçerlilik

- [TTKB 2026 YKS kapsam duyurusu](https://ttkb.meb.gov.tr/www/osym-tarafindan-2026-yilinda-gerceklestirilecek-quotyuksekogretim-kurumlari-sinavi-yksquotna-esas-derslere-ait-konu-ve-kazanimlar/icerik/831).
- [2026 resmî konu ve kazanım belgesi](https://ttkb.meb.gov.tr/meb_iys_dosyalar/2025_11/26164023_2026_yks.pdf): 581 sayfa; SHA256 `f52502743b6372e5df7de42d741b02a0b18eec087eac785b50a1fe3dd4a6d536`.
- [ÖSYM 2026 YKS kılavuzu](https://dokuman.osym.gov.tr/pdfdokuman/2026/YKS/basvuru_kilavuz06022026.pdf): oturum, test ve puan türü kapsamı.
- [MEBİ](https://ogmmateryal.eba.gov.tr/mebi): güncel ders, üst konu, alt konu ve konuya bağlı öğrenme birimleri. YKS kategori 1 / sınıf düzeyi 0 üzerinden 1.122 TYT/AYT/İngilizce alt konu sorgulandı. Arapça dışarıda bırakıldı.

İkincil konu listeleri karşılaştırma amacıyla incelendi; resmî belgeye aykırı eski baraj ve sabit soru dağılımı iddiaları veri olarak alınmadı.

**Bu paket 2026 resmî kapsamına dayanır.** 2026–2027 hazırlığında kullanılabilir; doğrulanmış bir 2027 resmî kapsamı olduğu iddia edilmez. Yeni resmî kapsam yayımlandığında yeni inceleme/paket hazırlanmalıdır. 2028 Maarif kapsamı bu pakete karıştırılmadı.

## Aktarılan veri

| Veri | Sayı / durum |
|---|---:|
| TYT / AYT / İngilizce YDT dersleri | 9 / 11 / 1 |
| Ortak kanonik hazırlık programı | 14 |
| Resmî kod ve sayfası doğrulanan `Official` kayıt | 492 |
| MEBİ kaynaklı sınav becerisi / mikro öğrenme birimi `ExamSkill` | 2.692 |
| Toplam öğrenme birimi ve yayımlanmış süre tahmini | 3.184 |
| Kanonik alt konu | 1.075 |
| Aynı kapsamı tekrar ettirmeyen eşdeğer TYT/AYT konu çifti | 47 |
| Kapsanan MEBİ alt konusu | 1.122 |
| Onaylı konu–kazanım eşleştirmesi ve süre payı | 3.249 / 3.249 |
| Sınav / puan türü / test kapsam satırı | 3.333 |
| Kanonik toplam ilk çalışma bütçesi | 68.058 dakika |
| Arapça YDT | 0 |

`ExamSkill` kayıtları resmî kazanım gibi sunulmaz; `PREP.*` kodu, kaynak adresi ve açıklaması ayrı tutulur. Bunlara uydurma PDF sayfası veya resmî ders saati verilmez. Kaynağı boş dönen bir konu için hazırlanan konu becerisi de açıklamada türetilmiş kayıt olarak belirtilir.

Ortak TYT/AYT içeriğinin kaynak kimlikleri korunur. 47 eşdeğer konu, programlama sırasında aynı içeriği iki kez çalıştırmaz; katalog satırları silinmez.

## Puan türü ve program davranışı

| Öğrenci hedefi | Kullanılabilir ders sayısı |
|---|---:|
| TYT | 9 |
| SAY | 13 |
| EA | 13 |
| SÖZ | 16 |
| DİL — İngilizce | 10 |

TYT dersleri ilgili AYT/DİL hedeflerine de dahildir. SÖZ hedefi Edebiyat–Sosyal Bilimler-1 ve Sosyal Bilimler-2 kapsamlarının birleşimini kullanır. Coğrafya-1 belgesindeki 20 kapsam dışı kod yalnız ilgili testi sınırlar; Coğrafya-2 için gerekli kazanımı yanlışlıkla kaldırmaz. Biyomlar, kapsam dışı olan ekosistem koduyla karıştırılmadı.

## Süreler nasıl kullanılır?

Her öğrenme biriminin öğrenme, pratik ve kontrol dakikaları ayrıdır. Ortak kazanım birden fazla konuya bağlıysa bu bileşenler konu paylarına bölünür; toplam bütçe korunur. Plan motoru onaylı ve yayımlanmış sürüm/payları seçer. Oluşmuş çalışma kayıtları kendi süre/sürüm bağlantılarını korur.

Tahminler `ReviewedStudy`, güven düzeyi `Low` olarak yayımlandı. Kullanıcı aktarımı onayladı; bunlar ölçülmüş öğrenci öğrenme süreleri veya MEB'in resmî bireysel öğrenme dakikaları değildir. Video süresi kullanılmadı. Gerçek çalışma verileri biriktikçe yeni tahmin sürümleriyle iyileştirilebilir.

Tüm kapsam için ilk çalışma bütçeleri yaklaşık TYT 517,5 saat, SAY 856,5 saat, EA 767,6 saat, SÖZ 712,7 saat ve İngilizce DİL 600 saattir. Bu değerler öğrencinin bildiği konular, seçtiği dersler ve süre paylarıyla azalabilir; molalar dahil değildir.

## Model ve servis değişiklikleri

- `StudyCatalogYksScope`: konu, sınav, puan türü, test, yabancı dil, kapsam kanıtı ve teste özel dışlanan kazanım kimlikleri.
- `OfficialCurriculumLessonBinding`: kanonik programın birden fazla TYT/AYT dersine bağlanması.
- Kazanımda kaynak türü/adresi; resmî sayfa ve resmî konu ders saati gerektiğinde boş kalabilir.
- Eğitim kurulumu, yayımlanmış kazanım okuması ve yıllık plan motoru aynı YKS kapsam filtresini kullanır.
- Onaylı süre payı bulunan kayıtların bütçeyi bozacak kalıcı silinmesi engellenir; arşivleme kullanılabilir.
- Aktarım genel yönetici yetkisi ister, herkese açık HTTP aktarım uç noktası eklenmedi. Paket parmak izi, tamlık ve sahiplik kontrolleri ile tekrar aktarım kayıt çoğaltmaz.

## Bulunan ve düzeltilen sorunlar

1. PostgreSQL sorgularında C# 14'ün `ReadOnlySpan.Contains` seçimi çeviri hatasına neden oluyordu; sorgular açık `Enumerable.Contains` kullanıyor.
2. SÖZ öğrencisinde bir testte dışlanan kazanımın diğer testte de kaybolması, izin verilen kapsamların birleşimiyle düzeltildi.
3. Onaylı süre paylarının güvenli silme kontrolüne dahil edilmesi ve kaynak türünün yönetim ekranında gösterilmesi sağlandı.
4. YDT'de henüz hazırlanmış veri olmayan dillerin seçilmesi engellendi.
5. Eski katalogda üst konu sıra numaraları tekrarlanıyordu. MEBİ kaynak sırası 1.454 konu/alt konu satırı için doğrulandı; farklı olan 274 sıra düzeltildi. Matematik TYT artık Sayı Kümeleri, Kimya TYT Simyadan Kimyaya, Felsefe Felsefenin Anlamı ile başlar. Var olan planların görev sıraları değiştirilmedi.
6. İzole sürüm provasındaki ilgisiz ödev eki modeli bu dağıtımdan ayrıldı; o taslağı silen bir geçiş uygulanmadı.
7. Dağıtım sağlık kontrolünde panelin gerçek 4000 portu kullanıldı. İlk kontrol hatası arayüzleri güvenli biçimde önceki sürüme döndürdü; ardından veri karşılaştırmalarıyla geçiş tamamlandı.

## Test kanıtı

- Gerçek PostgreSQL üzerinde 119 test başarılı; başarısız veya atlanan test yok.
- Yönetim / kazanım / öğrenci kurulum ekranlarının hedefli 27 testi başarılı.
- Öğrenci ve personel üretim derlemeleri başarılı.
- İzole canlı kaynakta EF model farkı yok.
- Canlı yedeğinden ayrı prova veritabanına geçiş ve tam aktarım iki kez uygulandı; kayıt çoğalmadı, eski LGS/iş kayıtları değişmedi.
- TYT, SAY, EA, SÖZ ve İngilizce DİL ders sayıları ve yayımlanmış süre erişimleri PostgreSQL testinde doğrulandı. SÖZ Coğrafya birleşimi ve öğrenci/atanmış öğretmen EA yıllık önizleme/taslak işlemleri de bu test kapsamındadır.

### Canlı tarayıcı testi

Canlı sayfalarda uçtan uca doğrulanan temsilî kapsam TYT'dir:

1. Öğrenci eğitim bağlamını TYT seçti; dokuz ders dinamik listelendi.
2. Önceki LGS haftalık taslağıyla TYT önerisini birleştirme isteği kapsam korumasıyla reddedildi. Aktif LGS planı değişmedi.
3. Test için yedeklenen haftalık taslak arayüzden boşaltılıp TYT sihirbaz önerisi kaydedildi.
4. Haftalık 14 saat ile 26 Ekim 2026–1 Haziran 2027 önizlemesinde 26.280 dakika yerleşti, 4.768 dakika sığmadı. Ders bitişleri, eksik süreler ve saat artırma önerileri gösterildi.
5. Önerilen 11 Temmuz 2027 tarihine uzatma sonrasında yerleşmeyen süre sıfırlandı; öğrenci yıllık taslağı kaydetti ve takvimi açıldı.
6. Atanmış öğretmen toplu takipten öğrenciyi seçip sihirbazı açtı. Ek haftalık saat önerisini kaydetti; 1 Haziran önizlemesinde 31.048 dakika yerleşti, kalan süre sıfırlandı.
7. Aynı anda ikinci taslak oluşturma isteği sunucuda mevcut taslak korumasıyla reddedildi.
8. İlk test taslağı arşivlendikten sonra öğretmen yıllık taslağı oluşturdu; öğrencinin plan listesinde göründü.

Yeni test taslakları yayımlanmadı; var olan aktif LGS planı değiştirilmedi.

### Devam oturumu: AYT ve İngilizce YDT

- EA hedefinde 13 dersin dinamik listelendiği canlı ekranda doğrulandı; AYT Matematik, Tarih, Coğrafya ve Edebiyat dersleri seçildi.
- Sayısal hedefinde 13, Sözel hedefinde 16 ders canlı kurulum ekranında ayrıca doğrulandı. Sözel Coğrafya'da 34, EA Coğrafya'da 31 uygun alt konu gösterildi; teste özgü kapsam filtresi arayüze de yansıdı.
- Öğrenci sihirbazında iki öneriyle haftalık 28 saatlik test düzeni kaydedildi. Öğrenci ve öğretmen aynı haftayı önizledi: 26 Ekim 2026–1 Haziran 2027 aralığına 46.058 dakika yerleşti, 6.502 dakika kapasite kaldı, sığmayan süre sıfırdı.
- Öğrenci EA taslağı `1cd18468-07b8-41a4-835e-9a68666aa410` olarak oluşturuldu; öğretmen takviminden açıldı, AYT kartları görüldü ve ekran kanıtı kaydedildi. Bu test taslağı da arşivlendi.
- DİL hedefindeki yabancı dil alanında yalnız İngilizce bulundu; on ders (dokuz TYT ve İngilizce YDT) canlı ekranda seçildi.
- Öğrenci YDT sihirbazı ve 28 saatlik hafta önizlemesinde 35.998 dakika yerleşti, 16.562 dakika kapasite kaldı, sığmayan süre sıfırdı. İngilizce dersi 4.950 dakika ve 24 Mart 2027 tahmini bitişle gösterildi.
- Öğrenci YDT taslağı `8ec1e3bb-8471-47e9-b074-560ae5022b87` olarak kaydedildi. Haftalık takvimde İngilizce kartı açılıp Subject/Object/Possessive/Reflexive Pronouns gibi `PREP.ENG.*` öğrenme birimlerinin saat aralıkları, dakikaları ve süre sürümü 1 bağlantıları görüldü. Test taslağı geçmişi korunarak arşivlendi.
- Öğretmen İngilizce YDT sihirbazında haftalık önerileri oluşturup kaydetti. 26 Ekim 2026–1 Haziran 2027 önizlemesinde öğrenciyle aynı 35.998 dakika yerleşti, 16.562 dakika kapasite kaldı, sığmayan süre sıfırdı. İngilizce için tahmini bitiş yine 24 Mart 2027 oldu.
- Öğretmen YDT taslağı `8d405b24-3c41-4305-a15e-c00f71236f35` olarak kaydedildi. Öğrenci plan listesinde göründü ve ayrıntı/takvim sayfasından açıldı. Böylece kalan öğretmen YDT kaydı ve öğrenciye yansıma kontrolü de tamamlandı.
- Son temizlikte öğrenci tekrar orijinal LGS bağlamına ve 12 kartlı haftasına döndürüldü. Beş YKS test planı arşivdedir; eski iş satırları, hesaplar ve bildirimler dağıtım öncesi parmak izleriyle yeniden aynı bulundu. Arşiv temizliği son aşamalarda sunucu betiğiyle yapıldı; yeni tarayıcı onay kutusu açılmadı.

### Test sonrası koruma ve temizlik

| Plan | Kimlik | Son durum |
|---|---|---|
| Eski LGS planı | `a4cea155-5485-4db6-9f52-1ec4df8080c3` | Aktif, korunmuş |
| Öğrenci TYT test taslağı | `828f80e4-a4ce-4f3e-8447-d5f61378d716` | Arşiv, 1.831 çalışma parçası korunmuş |
| Öğretmen TYT test taslağı | `eb3b5aec-7ae8-46dd-aac9-a3a47f2391b9` | Arşiv, 1.830 çalışma parçası korunmuş |
| Öğrenci EA test taslağı | `1cd18468-07b8-41a4-835e-9a68666aa410` | Arşiv, çalışmalar korunmuş |
| Öğrenci İngilizce YDT test taslağı | `8ec1e3bb-8471-47e9-b074-560ae5022b87` | Arşiv, çalışmalar korunmuş |
| Öğretmen İngilizce YDT test taslağı | `8d405b24-3c41-4305-a15e-c00f71236f35` | Arşiv, çalışmalar korunmuş |

Test öğrencisinin eğitim bağlamı, ders/konu seçimleri, müsaitliği ve 12 kartlı LGS haftalık taslağı test öncesi yedeğinden aynen geri getirildi. Temizlik öncesi ayrıca yedek alındı. Test taslakları silinmedi.

Son parmak izi karşılaştırmasında **dağıtım öncesindeki bütün iş satırları, hesaplar ve bildirimler aynıydı**. Eski LGS planında 560 çalışma, bir tamamlanmış çalışma/55 gerçek dakika ve 560 kazanım bağlantısı korundu. Tek aktif plan LGS'dir.

## Dosyalar ve işletim

- Veri paketi: `data/coaching-outcomes/yks-2026-basis-v1.json`.
- Kaynak konu sırası: `data/coaching-outcomes/yks-topic-order-20261019.json`.
- Sıra uygulama/doğrulama: `tools/releases/coaching-yks-20261019/prepare-order.py`, `order-apply.sql`, `order-apply.sh`, `order-verify.sh`. Yeni ortamda ana aktarımdan sonra sıra adımı da uygulanmalıdır.
- Sürüm betikleri: `tools/releases/coaching-yks-20261019/`.
- Araştırma, test günlükleri ve ekran kanıtları: `artifacts/mebi-yks-research-20261019/`.
- Sunucuda özel yedek/kanıt dizini: `/var/lib/eduivme/releases/coaching-yks-20261019-r3`.
- Mühürlü R3 arşiv SHA256: `f2a134e5f6f5410318f8691657b9c18038e48ecad99cbd188bf38488782cdabe`. Sonradan uygulanan katalog sıra düzeltmesi ayrı SQL adımıdır.

Çalışma alanındaki ilgisiz ödev eki, öğretmen ve diğer ürün değişiklikleri geri alınmadı. Canlı paket mevcut canlı kaynaklardan izole hazırlanmıştır; çalışma alanının bütün kirli farkları topluca dağıtılmamıştır.
