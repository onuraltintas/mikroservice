# Öğretmen Takip merkezi

20 Ekim 2026. Canlıya dağıtıldı; gerçek öğretmen ve öğrenci oturumlarında uçtan uca doğrulandı. [Canlı doğrulama ve veri koruma raporu](ogretmen-takip-merkezi-canli-20261020.md).

## Kullanım ve kapsam

Personel portalında **Koçluk → Öğretmen paneli → Takip merkezi** açılır. Mevcut Çalışma takibi ve Ödevler ekranları kullanılmaya devam eder.

- Tarih aralığı, öğrenci/çalışma/ödev başlığında arama ve kayıt türü filtreleri vardır.
- Varsayılan dönem bugünden 30 gün öncesinden bugüne kadardır. Daha eski gecikmeler için başlangıç tarihi geriye alınır. En fazla 366 günlük dönem seçilebilir.
- Üç özet sayı seçilen tarih ve aramayla eşleşen tüm sayfaları kapsar. Kayıt türü seçimi yalnız listeyi daraltır; diğer türlerin özetini gizlemez.
- Liste 25 kayıtlık sunucu sayfalaması kullanır. Filtreler sayfalamadan önce uygulanır; yalnız ilk öğrenci sayfasında arama yapılmaz.
- Liste yenileme, yükleniyor, boş sonuç, hata ve tekrar deneme durumları vardır. Yeni sorgu eski isteği iptal eder; eski satırlar ve sayaçlar hata sonrasında görünür bırakılmaz.

| Tür | Dahil edilme kuralı | Tarih filtresi |
|---|---|---|
| Geciken çalışma | Aktif plandaki tamamlanmamış çalışma. Saatliyse bitiş anında, saatsizse ertesi gün gecikir. | Planın saat dilimindeki çalışma günü |
| Geciken ödev | Öğretmenin mevcut kurum kapsamındaki aktif ödevi; teslim edilmemiş, Assigned/InProgress durumunda ve son tarih geçmiş. | Türkiye saat dilimindeki son teslim günü |
| Değerlendirme bekleyen | İptal edilmemiş öğretmen ödevindeki Submitted durumunda, teslim tarihi bulunan öğrenci kaydı. Graded durumu dahil edilmez. | Türkiye saat dilimindeki teslim günü |

Veri yokluğu öğrencinin çalışmadığı şeklinde yorumlanmaz. Otomatik risk puanı, ders saati artırma veya plan değişikliği yapılmaz.

## İlgili kaydı açma

- Çalışma satırı öğrenci kimliği, tam plan sürümü kimliği ve çalışma kimliğiyle mevcut plan bileşenini açar. İlgili çalışma ayrıntısı seçilir; yıllık planın ilk 50 çalışması içinde olması gerekmez.
- Ödev veya teslim satırı mevcut ödev değerlendirme bileşenini tam ödev kimliğiyle açar. Seçilen öğrencinin teslimi gösterilir; diğer öğrencilerin puanları değiştirilmez.
- Seçilen öğrenci artık ödevde görünmüyorsa başka öğrenciye geçilmez; açıklayıcı boş durum gösterilir.
- Listeye dönüşte filtre ve sayfa korunur, sunucu verisi yenilenir. Değerlendirilen teslim ve taşınan gecikmiş çalışma güncel durumuna göre listeden çıkar.
- Ayrıntıdan çıkış ve öğretmen çalışma alanında sekme/bildirim geçişleri mevcut kaydedilmemiş plan/değerlendirme ve süren işlem kontrollerini kullanır.

## API ve kayıt koruma

Yeni okuma ucu:

```text
GET /api/coaching/teachers/me/tracking
  ?fromDate=2026-10-01&toDate=2026-10-20
  &mode=all&search=&pageNumber=1&pageSize=25
```

`mode`: `all`, `study`, `assignment`, `review`. Sayfa boyutu 1–100, sayfa numarası 1–1000, arama en fazla 200 karakterdir.

Uç önce mevcut öğretmen hesabını/profilini ve kurumunu doğrular. Yalnız aktif öğretmen–öğrenci atamalarını aday alır; mevcut ürün okuma yetkisi üzerinden hesap, profil, kurum ve öğrencinin paylaşım izni kontrol edilir. Kontrol 100 kişilik gruplarla yapılır. Başka öğretmene veya kuruma ait ödevler dahil edilmez. Ayrıntı ekranlarının mevcut sunucu yetki kontrolleri devam eder.

Yeni tablo, sütun veya migration yoktur. Merkez yalnız okur; düzenleme ve puanlama mevcut komutları kullanır. Çok geniş dönem 10.000'den fazla aday çalışma/ödev satırı içerirse kısaltılmış sonuç ve yanlış toplam gösterilmez; 422 yanıtıyla daha dar dönem istenir. Bu sınır mevcut Çalışma takibi yaklaşımıyla uyumludur.

Canlı hesaplara veya veritabanına bu geliştirme sırasında yazılmadı. İzole localhost PostgreSQL testleri tamamlanan çalışma, süre, kazanım bağlantısı, teslim ve puanların sorgulardan önce/sonra aynı kaldığını doğrular. Test veritabanları test sonunda kaldırıldı; bu görev için başlatılan test kümesi durduruldu.

## Doğrulama

- Gelişim regresyon paketi: öğrenci/ortak bileşenlerde **86**, öğretmen tarafında **67**, toplam **153 başarılı test**. Takip merkezi için 9 yeni arayüz/HTTP testi, ortak planın tam çalışma kimliğiyle açılması ve seçilen öğrenci teslimi kapsamı dahildir.
- Son boş durum düzenlemesinden sonra ilgili ödev ayrıntısı paketi yeniden çalıştırıldı: **8 başarılı test**. Bunlar yukarıdaki 153 kapsamındadır; tekrar sayılmaz.
- Gerçek PostgreSQL üzerinde **7 başarılı, 0 atlanan test**: 4 yeni Takip merkezi testi, 2 mevcut çalışma/ödev takip testi ve 1 ödev kalıcılık regresyonu.
- PostgreSQL kapsamı: tür/arama/tarih filtresi, sayfalamadan önce filtreleme, 103 öğrencide ilk 100'ün dışındaki kayda erişim, paylaşım ve kaldırılan atama, yetkisiz rol, bitiş anı, New York/Türkiye gün sınırı, teslim/değerlendirme sonrasında listenin güncellenmesi ve mevcut kayıtların korunması.
- İki portalın üretim derlemesi başarılı. Personel başlangıç paketi yaklaşık **990 kB**; mevcut 1 MB hata sınırı artırılmadı. Takip merkezi sekme açıldığında yüklenir. Önceden bulunan boyut/stil uyarıları ayrıca devam eder.
- Gerçek Angular bileşeni örnek verili, canlı bağlantısı bulunmayan yerel uygulamada tarayıcıdan incelendi: tür filtresi, seçili öğrenci teslimi, filtre korunarak dönüş, tam çalışma ayrıntısı ve dar ekranda kaydırılabilir tablo doğrulandı.
- `test:coaching-development` paketine yeni arayüz dosyaları ve ortak plan testleri eklendi. Mevcut CI işinin PostgreSQL filtresine Takip merkezi ve eski takip regresyonları eklendi. YAML yerelde ayrıştırıldı; bu oturumda GitHub CI çalıştırması başlatılmadı.
- Kod incelemesinde salt okunur sorgu, öğretmen/kurum/paylaşım sınırı, eski istek iptali, ayrılma kontrolleri ve üretim paket boyutu kontrol edildi.

## Kanıtlar

- `artifacts/coaching-tracking-regression.log`
- `artifacts/coaching-tracking-pg-final.log`
- `artifacts/coaching-tracking-detail-final.log`
- `artifacts/coaching-tracking-build-ui.log`
- `artifacts/coaching-tracking-build-student.log`
- `artifacts/coaching-tracking-desktop.png`
- `artifacts/coaching-tracking-desktop-rows.png`
- `artifacts/coaching-tracking-mobile.png`

Görseller örnek verili yerel ekranlara aittir; canlı doğrulama kanıtı değildir.
