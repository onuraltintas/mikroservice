# 2026 hedef kataloğu — geliştirme ve kaynak doğrulama

## Kapsam

Yalnız 2026 ve sonrası; eski yıl verisi aktarılmadı. Ortak model, resmî kaynak ayrıştırma, belirsiz eşleşme izolasyonu ve öğrenci/öğretmenin ortak hedef ekranına filtreli katalog araması geliştirildi. **Canlıya dağıtılmadı ve canlı veritabanına aktarım yapılmadı.** Tam veri aktarımı yerel PostgreSQL provasında gerçekleştirildi.

LGS'nin yıllık resmî veri kaynağı henüz tam olarak elde edilemediği için bütün çalışma tamamlandı sayılmaz. Aşağıda somut kaynak engeli ve hazır olan işler ayrılmıştır.

## Ortak model

Mevcut okul/program/hedef tablolarını yeniden kurmak yerine iki ek tablo kullanıldı:

- `target_admission_batches`: sınav, yıl, yerleştirme aşaması, resmî HTTPS kaynak URL'si, kaynak paketi SHA256, alınma/yayımlanma zamanı.
- `target_admission_records`: kaynağın program/kabul kodu, kurum/program adı, il/ilçe, kurum türü, derece, dil, burs, öğretim biçimi, fakülte/alan, genel kontenjan/yerleşen, taban/tavan puan, başarı sırası/yüzdelik, mevcut okul veya program FK'si, eşleşme durumu ve inceleme gerekçesi.

Bu ilk fazda ayrı kurum/kabul seçeneği ana tablolarını çoğaltmak yerine, yıl ve kaynak kapsamındaki kabul satırı yeni tabloda temsil edildi. Mevcut iç kimlikler kanonik hedef kimliği olarak kaldı. Yeni tablo kaydı, mevcut hedef bağlantısını kırmadan doğru kaynak/yıl verisini gösteriyor. Birden fazla yıl veya kaynak düzeltmesinin yönetimi genişletilirken kurum ve kabul seçeneği ana tabloları ayrıca çıkarılabilir.

`Pending`, `Ambiguous`, `Conflict` satırları korunur ama aramada seçilebilir sonuç olmaz. Doğrulanmış bir satır tam olarak bir mevcut okul/programa bağlanmalıdır. Silme FK'leri Restrict; aralık ve tekillik kontrolleri veritabanında da var. Puan 5 ondalıkla saklanır. 2026 öncesi paketler, resmî olmayan/HTTP kaynak adresleri reddedilir.

Migration: `20261019095937_TargetAdmissions2026`. Up yalnız iki yeni tablo ve indeksleri ekler; mevcut kullanıcı, hedef, plan veya çalışma satırlarını değiştirmez. Migration'ın Down işlemi rutin canlı geri dönüş olarak kullanılmamalı; eklenmiş kaynak geçmişini siler.

## YKS kaynakları ve tam paket

[ÖSYM 2026 ana yerleştirme sonuçları](https://www.osym.gov.tr/2026-yks-yerlestirme-sonuclarina-iliskin-sayisal-bilgiler) sayfasındaki Tablo-3 ve Tablo-4 XLSX dosyaları indirildi. Dosya başlığı ve genel kontenjan sütunları doğrulandı; önceki yıl sütunu veya özel kontenjan puanı genel sonuç olarak alınmadı.

[YÖK Atlas](https://yokatlas.yok.gov.tr/) yeni arayüzü tarayıcıda incelendi. Arayüz 2026 yerleştirme verilerinin yayımlandığını bildiriyor. Kendi istemcisinin kullandığı kimlik doğrulamasız `/api/tercih-kilavuz/search` servisinden 215 sayfa alındı. Her sayfada yıl=2026 denetlendi; yalnız ihtiyaç duyulan alanlar saklandı. Eski yıllar için çağrı yapılmadı. Sayım ve kod tekilliği tamamlanmadan paket üretilemiyor.

| Ölçüm | Sonuç |
|---|---:|
| ÖSYM/YÖK ortak ve tekil 2026 program kodu | 21.493 |
| Ön lisans / lisans | 9.254 / 12.239 |
| TYT / SAY / EA / SOZ / DIL | 9.254 / 5.744 / 3.900 / 1.914 / 681 |
| YÖK'te pozitif başarı sırası bulunan | 18.251 |
| ÖSYM genel kontenjanında puanı yayımlanmayan | 756 |
| Eski katalogla aynı program kodu | 19.860 |
| 2026'da olup eski katalogda olmayan kod | 1.633 |
| Eski katalogda olup 2026 paketinde olmayan kod | 1.742 |
| Ortak yayımlanan alanlarda gerçek kaynak çelişkisi | 0 |

Taban puan, kontenjan, yerleşen ve puan türü kod bazında çapraz karşılaştırıldı. YÖK'te 2.486 satırın taban puanı ve iki satırın puan türü yayımlanmıyor; bunlar ÖSYM'nin yayımladığı değere aykırılık değildir. Puan için ÖSYM değeri, sıra ve filtre özellikleri için 2026 YÖK değeri kullanıldı. Bilinmeyen sıra/puan sıfırla veya hesapla doldurulmadı. Eski 1.742 program, kaynakta bulunmadığı için otomatik silinmedi/pasifleştirilmedi.

Kaynak dosya SHA256:

- Tablo-3: `35d2b182b59415567f26f824405a010b8d28f173d82427e5939485920052dad0`
- Tablo-4: `a22173410c03ed51d3f79491b416ad177a082754248378aa5188e85d68586e40`
- Birleşik kaynak manifesti/paket hash: `c2be63ffdc5ed113a31035c05ae5389b9987ddce07edb4f858cb7360ad730832`

Paket: `data/target-catalog/2026/yks-results.json`. Sayfa bazında kaynak yanıt hash'leri ve dosya hash'leri `yks-source-manifest.json` ile saklanır. Kaynak URL'si ve paket hash'i veritabanındaki batch satırında bulunur; tam manifest sürüm paketiyle birlikte tutulmalıdır.

## LGS — tamamlanmayı engelleyen durum

[MEB'in 2026 ilk yerleştirme duyurusu](https://baliseyhiho.meb.k12.tr/icerikler/2026lgsilkyerlestirmesonrasiboskontenjanlarvebasvuruekrani_17644637.html) asıl yıllık sonuç ekranı olan `SNV08008.ASPX` bağlantısını veriyor. Bu bağlantı normal tarayıcıda artık e-Okul ana giriş sayfasına yönleniyor; yıllık okul satırları sunulmuyor.

Diğer açık e-Okul ekranı `OOK06006.aspx` gerçekten çalışıyor. Adana/Fen ve Sosyal Bilimler/Adana Fen Lisesi örneğinde kurum kodu, okul türü ve yabancı dil; sınıf bazında boş kontenjan ve emsal puanlar görüldü. Bunlar güncel nakil bilgileri; ilk yerleştirme sonucu ve yıllık yüzdelik dilim yerine kullanılamaz. Bu nedenle buradan alınan puanlara 2026 yıllık sonuç etiketi verilmedi.

Rota Maarif'in mevcut arayüzünde lise kademesi seçimde yok. Açık okul arama API'si doğrudan çağrıda 403 döndü. Erişim kısıtları aşılmadı. Bazı 2026 başlıklı resmî tercih rehberlerinin içinde 2025 ve daha eski sonuçlar olduğu görüldü; kullanıcı talebi gereği bunlar aktarılmadı.

Sonuç: mevcut 3.010 okul korundu, henüz toplu 2026 LGS yıllık sonucu yayımlanmadı. Gerekli kalan kaynak: yıl ve yerleştirme aşaması açık olan resmî 2026 okul/kabul seçeneği dışa aktarımı. Kaynak erişilince aynı ortak model ve belirsiz eşleşme mekanizması kullanılabilir. [Kaynak durum kaydı](../../data/target-catalog/2026/lgs-source-status.json).

## Filtreler ve ekran

Öğrenci ve öğretmenin ortak hedef bağlantı bileşeninde “2026 resmî katalog filtreleri” açılır. Hedef türü LGS okulu/YKS programıdır. Eski bağlantılar korunur; eski arama açıkça eski katalog olarak ayrı bir details alanında bırakıldı.

Yeni filtreler: veri yılı (en erken 2026), serbest ad/kod araması, il, ilçe, okul/üniversite, kurum türü, puan türü, ön lisans/lisans, eğitim dili, burs/ücret, öğretim biçimi, fakülte/alan, taban puan aralığı, en yüksek sıra/yüzdelik ve sıralama. Seçenekler doğrulanmış katalog satırlarından gelir. Sonuçta veri yılı, yerleştirme aşaması, bilinmeyen alan açıklaması ve resmî kaynak bağlantısı gösterilir.

YÖK Atlas'ın puan türü, şehir, üniversite, kurum türü, derece, burs ve öğretim filtreleri örnek alındı. Bu ilk sürümde kategoriler tek seçimlidir; Atlas'taki çoklu seçim ve ayrı tercih listesi yönetimi henüz eklenmedi. İlçe seçenekleri mevcut sürümde tüm doğrulanmış ilçelerden gelir; il ile birlikte yapılan arama iki koşulu da uygular.

Yeni API: `GET /api/coaching/study-planning/targets/admissions` ve `/filters`. Mevcut hedef katalog endpoint'leri kırılmadı. Yeni API yalnız doğrulanmış, yayımlanmış ve aktif kanonik hedefe bağlı satırları döndürür. Öğrenci/öğretmen giriş yetkisi ve mevcut arama hız sınırı korunur. Belirsiz inceleme satırları bu endpoint'ten öğrenciye açılmaz.

## Doğrulama ve veri koruma

- Ayrıştırıcı: 4 test geçti; yanlış yıl, eksik puan, ondalık hassasiyet ve gerçek resmî dosya örneği kapsandı.
- Dar backend: 11 test geçti; eski kimlik/puanı koruma, tekrar aktarım, mükerrer kod, çoklu okul adayı, kaynak çelişkisi ve filtre sınırları kapsandı.
- Arayüz: 14 test geçti; yeni filtre HTTP parametreleri, yalnız dönen kimliğin seçilmesi, kapsam değişince eski isteğin iptali ve mevcut hedef bileşeni regresyonları.
- Tam 21.493 kayıt, canlıdan salt okunur alınan katalog kopyasıyla ayrı yerel PostgreSQL'e aktarıldı. Eski 21.602 program/3.010 okul satırının içerikleri önce/sonra eşit; ikinci aktarım mükerrer oluşturmuyor. Sonuç: `artifacts/target-catalog-20261019/postgres-final-rehearsal.log`.
- Öğrenci/yönetici üretim derlemesi geçti; mevcut stil bütçesi uyarıları sürüyor.
- Geniş PostgreSQL regresyonu: **131 geçti, 0 başarısız, 0 atlanan**. Yeni migration'ın geri/ileri uygulanması, ikinci kez uygulanması, mevcut çalışma satırının korunması ve filtrelerin gerçek PostgreSQL'de çevrilebilmesi dahil. İlk paralel deneme bu işe özel yerel PostgreSQL'in 20 bağlantı sınırına takıldı; sınır 100'e çıkarılıp testler sırayla tamamlandı. Canlı sunucu ayarı değiştirilmedi.
- Personel portalı üretim derlemesi de geçti; mevcut paket/stil bütçesi uyarıları sürüyor.
- Yeni çekirdek model/aktarım/arama kodlarında ölçülen satır kapsamı **%86,5** (173/200). Coverage çalıştırmasında 12 ilgili test geçti; çıktı `artifacts/target-catalog-20261019/coverage/` altında. Bu oran bütün uygulamanın veya yeni HTTP controller'ın uçtan uca kapsam oranı değildir.

Bu turda canlı hesap/plan/çalışma/hedef tablolarına yazılmadı. Ortak modeli ve doğrulanmış YKS paketini canlıya almadan önce yeni migration ve paket aynı yayın içinde korunmalı, katalog manifesti saklanmalı ve canlı koruma sayım/hash karşılaştırmaları yapılmalıdır. LGS kaynak eksikliği sürerken boş 2026 sonuç ekranı “tamamlandı” olarak sunulmamalıdır.
