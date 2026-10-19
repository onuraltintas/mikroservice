# Türkiye geneli 2026 LGS verisi — ikinci canlı aktarım

19 Ekim 2026. Türkiye geneli kapsam henüz tamamlanmadı. Bu işlem önceki kayıtları silmeden yeni resmî kanıtlar ekler ve dört bekleyen kimlik eşleşmesini çözer.

## Canlı sonuç

| Ölçüm | Önce | Sonra |
| --- | ---: | ---: |
| Kaynak/okul-program-aşama kayıtları | 432 | 701 |
| Aranabilir, doğrulanmış eşleşmeler | 274 | 434 |
| Birden fazla eski okul kimliğine çıkan kayıtlar | 147 | 260 |
| Bekleyen kimlikler | 9 | 5 |
| Kaynak kimlik çelişkileri | 2 | 2 |
| Okul kimlikleri | 3096 | 3111 |
| Yılı ve aşaması doğrulanmış, eşleşmiş puan kayıtları | 2 | 148 |

Bunlar tekil okul sayısı değildir. Aynı okulun farklı dili, alanı ve yerleştirme aşaması ayrı kayıt olabilir. Belirsiz sayısındaki artış, yeni belgelerdeki 113 satırın eski katalogda birden fazla kimliğe çıkmasındandır; bu satırlar aramaya açılmadı.

Yeni 269 satırın 156'sı doğrulandı. Kayseri'deki dört eski bekleyen kaydın çözümüyle toplam artış 160 oldu. Altı yeni kaynak paketi eklendi; toplam paket sayısı 14.

## Kaynak ve sütun kuralları

- [Konak RAM resmî doküman sayfası](https://konakram.meb.k12.tr/icerikler/2026liseleregecissistemilgstercihsurecidokumanlar_17624610.html): 2026 kontenjanları ile 2025 puanları aynı tabloda bulunur. Yalnız **2026 toplam kontenjanı** alındı. 22 sınavlı program aktarıldı; sınavsız/OBP programları ve eski puanlar dışarıda kaldı.
- Aynı sayfadaki **05 Ağustos 2026 ilk yerleştirme** belgesi: 135 satır ayrıştırıldı. Şehit Ahmet Özsoy Fen Lisesi'nin zaten doğrudan okul duyurusundan alınmış sonucu tekrar eklenmedi; 134 yeni satır aktarıldı. Belgedeki boş kontenjan, toplam kontenjana yazılmadı.
- **14 Ağustos 2026 ikinci nakil** belgesi: 136 okul/program satırından ikinci nakil puanı bulunan 108'i alındı. İkinci nakil puanı boş olan 28 satır, yanındaki ilk yerleştirme puanıyla doldurulmadı.
- [Mardin Fen Lisesi](https://mardinfenlisesi.meb.k12.tr/tema/okulumuz_hakkinda.php): açıkça 2026 LGS dokuzuncu sınıf kontenjanı olarak belirtilen 150 alındı. Aynı sayfadaki taban/tavan puanı, yerleştirme aşaması belirtilmediği için aktarılmadı.
- [Zonguldak/Çaycuma Nuran ve Celal Gülşen MTAL](https://nurancelalgulsenmtal.meb.k12.tr/icerikler/lgstercihiyapacakogrencilerimizindikkatine_17626643.html): sınavlı teknik programlardaki elektrik-elektronik ve endüstriyel otomasyon için 30'ar kontenjan hazırlandı. İki satır eski okul kimliği belirsizliğinden incelemede kaldı. Sınavsız alanlar alınmadı.
- [Nevşehir/Ürgüp Ahmet Yiğitoğlu Turizm MTAL](https://ahmetyigitogluturizmmtal.meb.k12.tr/icerikler/lgstercihsureciogrencivevelibilgilendirmekilavuzu_17633074.html): 2026-2027 merkezî sınav kabulü, yiyecek-içecek ve konaklama-seyahat alanları için 30'ar kontenjan doğrulandı ve eşleştirildi.

İzmir'in iki yeni yerleştirme belgesinde yüzdelik sütunu yoktur; **yeni yüzdelik değeri eklenmedi**. Bilinmeyen toplam kontenjanlar `null` kaldı. Yalova okul tanıtımında 2025-2026 başlığı ile 2026-2027 metni karışık olduğu için 90 kontenjanlık bilgi aktarılmadı.

## Çözülen eski eşleşmeler

Kayseri'de aşağıdaki kısaltılmış adlar, güncel resmî okul sayfaları ve tekil mevcut okul kimlikleriyle teyit edildi:

| Kaynak adı | Doğrulanan kurum | İlçe |
| --- | --- | --- |
| Kayseri Kadı Burhaneddin MTAL | [Kayseri Kadı Burhaneddin Mesleki ve Teknik Anadolu Lisesi](https://kadiburhaneddinmtal.meb.k12.tr/) | Melikgazi |
| Atatürk Kız MTAL | [Atatürk Kız Mesleki ve Teknik Anadolu Lisesi](https://ataturkktml.meb.k12.tr/) | Kocasinan |
| Mimar Sinan MTAL | [Mimar Sinan Mesleki ve Teknik Anadolu Lisesi](https://mimarsinanteml.meb.k12.tr/) | Melikgazi |
| Seyyid Burhaneddin MTAL | [Seyyid Burhaneddin Mesleki ve Teknik Anadolu Lisesi](https://sbeml.meb.k12.tr/) | Melikgazi |

Yalnız `SchoolId`, `MatchStatus` ve `Issue` eşleşme alanları değişti. Kaynağın özgün ad, ilçe boşluğu, kontenjan ve puan hücreleri değiştirilmedi. İlçe, bağlı mevcut okul kaydında doğrulandı; ham kaynak satırındaki boş ilçe alanı yeni bilgiyle üstüne yazılmadı. Düzeltme, tam ad/ilçe/kimlik ve tekil aday denetimiyle sınırlandı; tekrar çalıştırılması değişiklik yapmıyor.

Arif Molu ve Türkiye Tekstil kayıtlarında birden fazla eski kimlik bulunduğundan rastgele kimlik seçilmedi. Mehmet Altun Havacılık ve Uzay Teknolojileri kaydının ilçe kanıtı bulundu; eski bekleyen kayda bağlanacak kimlik için ek inceleme gerekiyor. Önceki iki çelişkili kaynak kodu da onaylanmış sayılmadı.

## Testler ve kayıt koruma

- 16 Python veri ayrıştırma/paket testi geçti. Bunlardan beşi yeni belgeleri, yıl/sütun ayrımını, boş nakil puanını ve tekrar eklenmeyen Menemen sonucunu doğrular.
- Gerçek PDF'lerden çıkarılmış tablo kanıtlarıyla yapılan hedefli satır izlemesinde yeni ayrıştırıcının 84 çalıştırılabilir satırından 73'ü (%86,9) kapsandı. Gerçek PDF ayrıştırma testleri ayrıca, önbelleksiz çalıştırıldı.
- Gerçek PostgreSQL üzerinde 16 hedef kataloğu/eşleşme/arama entegrasyon testi geçti.
- Üretim yedeği sunucuda alındı ve aynı sunucuda geçici veritabanına geri yüklendi. Altı paket ve dört kimlik düzeltmesi önce bu kopyada çalıştırıldı.
- Her paket ikinci kez çalıştırılarak aynı paket kimliğinin tekrar kullanıldığı ve tekrar kayıt oluşmadığı doğrulandı. Kimlik düzeltmesi de tekrar çalıştırıldı.
- Canlı uygulamadan önce/sonra bütün korunan Coaching tablolarının sayım ve içerik özetleri karşılaştırıldı. Hedefler, profiller, çalışma planları, görevler, kazanım bağlantıları ve çalışma kayıtları aynı kaldı.
- Aktarım öncesindeki 3096 okul kimliği ile 21602 üniversite programı korundu. Yeni eklenen 15 okul dışında eski okul satırları değişmedi.
- Önceki 432 kabul kaydının özgün kaynak hücreleri aynı kaldı; yalnız yukarıdaki dört eşleşme düzeltildi.
- Katalog aşamasına yanlışlıkla puan/yüzdelik yazılan satır sayısı: **0**. Dört düzeltmenin doğrulanmış bağlantı sayısı: **4**.
- Uygulama imajları ve veritabanı şeması değiştirilmedi. Geçici prova veritabanı kaldırıldı; üretim yedeği sunucuda tutuldu.

## Sonraki veri çalışması

Türkiye geneli tamlık iddiası yoktur. Aranabilir kaynaklar yedi il içeriyor; belirsiz satırlar dahil kaynak havuzu dokuz ile ulaşıyor. Öncelik: diğer illerin yıl açık toplam kontenjan listeleri, mevcut 260 çoklu kimlik eşleşmesi ve kalan beş bekleyen kayıt. Yeni kaynak bulunması mevcut belirsiz kayıtların otomatik onaylanması anlamına gelmez.

Kanıt manifesti: `data/target-catalog/2026/raw/lgs-followup-20261019/sources.json`.
Aktarım paketleri: `data/target-catalog/2026/lgs-followup-reviewed-20261019/`.
Canlı sonuç: `artifacts/lgs-followup-20261019/live-apply.log`.
Sunucudaki yedek/prova/karşılaştırmalar: `/var/lib/eduivme/releases/lgs-followup-20261019/`.
