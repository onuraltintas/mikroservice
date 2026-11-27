---
name: eduivme-design
description: Tasarım kuralları — eduivme, maxkoç (Koçluk) ve Master Hızlı Okuma öğrenci ekranlarını tasarlarken, yeniden düzenlerken ya da gözden geçirirken kullan. Renk, yazı, biçim, hareket, erişilebilirlik ve marka kuralları burada.
---

# eduivme tasarım kuralları

Kaynak: kullanıcı kararları (18.11.2026) ve tasarım tuvali "Öğrenci panelleri — tasarım kimliği önerisi"
(https://claude.ai/artifact/DYYb8JY4f8G2VEQRxnMnbv). Bu dosya ile tuval çelişirse kullanıcıya sor.

## Marka mimarisi
- **eduivme** üst marka. Öğretmen, kurum ve yönetim panelleri eduivme adını ve simgesini taşır.
- **maxkoç** Koçluk ürünü (maxkoc.com, veli.maxkoc.com).
- **Master Hızlı Okuma** okuma ürünü (masterhizliokuma.com).
- Üç işaret de noktası yukarı sıçrayan küçük "i"yi paylaşır. Ürünü zemin rengi ve "i"nin altındaki küçük çizgi ayırır:
  - maxkoç: fosforlu kalem çizgisi.
  - Hızlı Okuma: okunan satır ve odak çizgisi.
- Alt markalarda (maxkoç, Master Hızlı Okuma, maxkampüs) "eduivme" yalnız sayfanın en altındaki tek satırda geçer: "maxkoç bir eduivme markasıdır. © 2026 ONAL Yazılım ve Otomasyon. Tüm hakları saklıdır." (Master Hızlı Okuma: "Master Hızlı Okuma bir eduivme markasıdır. © 2026 ONAL Yazılım ve Otomasyon. Tüm hakları saklıdır."; maxkampüs: "© 2026 maxkampüs, bir eduivme markası.") Logonun yanında, başlıklarda, sekme başlığında ve plan açıklamalarında geçmez; logonun altındaki küçük satır panelin ya da sayfanın adıdır ("Öğrenci paneli", "Veli paneli", "Öğrenci koçluğu"). ONAL logolarda yer almaz. (Kullanıcı kararı, 19.11.2026.)
- İşaretler `tools/brand/marks.py` içinde; dosyaları `python3 tools/brand/build.py` üretir. Simgeyi elle çizme; üreticiyi değiştir.
- Adres → marka: `maxkoc.com` ve `veli.maxkoc.com` maxkoç; diğer adresler eduivme (`brandForHost`, `applyBrand`).

## İki ürünün kimliği
| | maxkoç | Master Hızlı Okuma |
|---|---|---|
| Fikir | Defter ve fosforlu kalem: planlı, sakin | Odak çizgisi: canlı, yormayan |
| Yazı | IBM Plex Sans; rakamlar `tabular-nums` | Lexend; okunan metin 18–20 px, satır aralığı 1,6–1,7 |
| Ana renk | Mürekkep `#1F4E8C` (koyu `#7FB0F0`) | Çam `#0B7A64` (koyu `#3CC9A8`) |
| Vurgu | Fosforlu `#FFE066` (koyu `#E6C84A`), yalnız "bugün" ve "şimdi" | Turuncu `#E8730C` (koyu `#FF9A3D`), yalnız grafik işaret; yazı rengi olarak değil |
| Zemin, açık | `#EDF2F8`, kart `#FFFFFF` | `#EAF4F0`, kart `#FFFFFF` |
| Zemin, koyu | `#0F141B`, kart `#161D27` | `#0C1413`, kart `#13201E` |
| Yazı | `#18222F`, ikincil `#4F5D6F` (koyu `#E8EDF3`, `#A3B0C0`) | `#10201D`, ikincil `#4A5F5B` (koyu `#E6F0EE`, `#9FB5B0`) |
| Köşe | 8–10 px | 14 px, ana düğme tam yuvarlak |
| Sol menü (açık) | Saydam: `rgba(31,78,140,0.08)`; seçili öğe beyaz, hafif gölgeli, mavi çizgi | Saydam: `rgba(11,122,100,0.09)`; seçili öğe beyaz, turuncu odak |

Ders renkleri (maxkoç, açık temada soluk zemin + koyu yazı etiketi):
- Matematik `#E6EEFB`/`#1E4FA3`
- Türkçe `#FDEBDD`/`#9A3412`
- Fen `#E3F3E8`/`#166534`
- İnkılap `#F1E8FB`/`#6B21A8`

Koyu temada ders rengi yalnız küçük işaret olarak kullanılır.

**maxkoç güncellemesi (21.11.2026, kullanıcı kararı):** Büyük yüzeylerde fosforlu sarı yerine bal rengi kullanılır: `--coaching-honey` `#F5C842` (koyu `#E6C84A`), soluk `--coaching-honey-soft` `#FFF3CC`, koyu ton `--coaching-honey-deep` `#D89A00`. Fosforlu `#FFE066` yalnız küçük "bugün / şimdi" işaretinde kalır. Kart köşesi 16 px, denetim köşesi 10 px.

## Canlılık (açık tema) ve ölçüsü
21.11.2026 kararı: paneller (maxkampüs, maxkoç öğrenci ve veli, öğretmen ve kurum panelleri) "B" dilinde, renkli ve hareketli ama abartısız.
Referans: maxkampüs tuvali https://claude.ai/artifact/Y1TFFms6NBZzQQKEnxhNR4, maxkoç öğrenci tuvali https://claude.ai/artifact/TryF7d3VSQayKom8eCsSvT.

Açık temada şunlar kullanılır:
- hafif renkli sayfa zemini (`--ds-page-tint`), üstünde beyaz kartlar
- ürün rengine çalan iki katlı ince gölge: `0 1px 2px rgba(…,0.06), 0 8px 20px -10px rgba(…,0.18)` (`--ds-shadow`, `--ds-shadow-raised`)
- özet kutuları anlamının soluk renginde: bilgi mavi (`--ds-info-soft`), başarı yeşil, sınav ve hedef mor (`--ds-violet-soft`), seans ve uyarı turuncu (`--ds-orange-soft`), gecikme kırmızı; sayı aynı tonun koyusunda
- renkli ders, seri ve XP etiketleri; baş harf yuvarlakları sırayla farklı ders renklerinde
- grafikler: sayıların yanında oran çubuğu, ölçüm dizilerinde sütun grafik, parçalı bütünde dağılım çubuğu (`staff-portal/src/app/shared/staff-charts.ts`: `staff-bars`, `staff-meter`, `staff-split`); veri tablosu "Tablo olarak gör" altında kalır
- grafik çizgisinin altında %10–12 dolgu
- öğretmen ve kurum panellerinde grafit karşılama kartı ve amber halka; menüde hap biçiminde sekmeler, grup renk noktaları

Kullanılmaz:
- gradyanlı arka plan ya da parlama
- her şeye gölge
- her ders için ayrı parlak kart
- okunan metnin ve soruların altında renk; okuma ve soru alanı düz ve sakin kalır

Koyu tema sade kalır, gölge yok.

## Halka açık siteler: tema ve hareket (kullanıcı kararı, 27.11.2026)
- **Tema:** eduivme.com, maxkoc.com ve maxkampus.com varsayılan olarak **açık** açılır; cihazın koyu ayarı tek başına siteyi koyu yapmaz. Her sitenin üst çubuğunda koyu/açık geçiş düğmesi var (`app-theme-toggle`, maxkampüs'te Görünüm menüsü); seçim `theme` anahtarında saklanır, "Cihaz ayarına göre" ancak seçilirse uygulanır.
- **Renk ve canlılık (halka açık sayfalar):** giriş bölümünde ürün renklerinde yumuşak, bulanık ışık lekeleri ve renkli kartlar kullanılabilir; açık temada giriş bölümü de açıktır. Koyu tema sade kalır (lekeler soluk, büyük renkli paneller yüzey rengine döner).
- **Hareket:** bloklar ilk kez göründüğünde yukarı doğru belirir (`publicReveal`), basılabilir kartlar üzerine gelince hafifçe kalkar (`public-lift`), giriş bölümündeki örnek kart yavaşça süzülür. İçerik varsayılan olarak görünürdür (arama motoru ve JS'siz ziyaretçi için); `prefers-reduced-motion` açıksa hiç hareket yok. Ortak stiller `clients/shared/styles/_public-motion.scss`.
- Bu kurallar uygulama içi paneller için değil; panellerde yukarıdaki sade ölçü geçerli.

## Kural: sabit renk yok
- Bileşenlerde ham hex yazma; anlamlı değişkenleri kullan (`--sp-*`, `--coaching-*` ya da yeni tasarım değişkenleri).
- Her yüzey açık ve koyu temada birlikte tasarlanır. Beyaz zemin üstünde temadan gelen açık yazı rengi kalmasın; 18.11.2026 hatası buydu: koyu temada egzersiz metni beyaz zeminde beyaz yazıyla çıkıyordu.
- Yazı tipleri yerelden (`@fontsource`); Google'dan ya da CDN'den hiçbir şey yüklenmez (`self-hosted-fonts.test.mjs`).

## Erişilebilirlik (pazarlık yok)
- **Okunurluk:** yazı en az 4,5:1; büyük yazı (24 px üstü) en az 3:1.
- **Grafik ve sınırlar:** etkileşimli kenarlık ve grafik öğeleri zemine karşı en az 3:1. Onay kutusu kenarlığı açık temada `#7D8B9C`/`#6F8A84`, koyu temada `#5E6E82`/`#5C7871`.
- **Odak halkası:** görünür olsun. maxkoç'ta açık temada mavi, koyu temada sarı; Hızlı Okuma'da turuncu, renkli zemin üstünde beyaz.
- **Dokunma alanı:** en az 44×44 px.
- **Anlam yalnız renkle verilmez:** simge ya da metin de olsun. "✓" yazı karakteri yerine SVG kullan, yanında ekran okuyucu metni olsun.
- **Menü:** seçili öğede `aria-current="page"`.
- **Grafikler:** açıklamada değerler yazsın (`aria-label`). Az veri varsa değerleri doğrudan çiz. Çubuk eksenleri 0'dan başlar.
- **Hareket (21.11.2026 kararı: canlı ama ölçülü):** `prefers-reduced-motion` açıksa hiç hareket yok. Eski "ekran başına tek hareket" kuralının yerine:
  - Ekran açılınca kartlar ve bölümler bir kez, sırayla belirir (0,5 s, en çok yaklaşık 300 ms gecikme).
  - Tıklanabilir kart ve sekme üstüne gelince 2–3 px yükselir.
  - Grafikte çubuklar sıfırdan büyür, oran çubukları soldan dolar, dağılım parçaları sırayla açılır.
  - Seçili menü öğesinin küçük bir işareti olur (maxkoç: bal rengi kıvılcım; eduivme: amber nokta); okunmamış sayısı hafifçe nabız atar.
  - Sürekli süren tek hareket yavaş "nefes"tir (karşılama halkası); hızlı yanıp sönme, zıplama, parlama yok.
  - Okunan metin, soru ve egzersiz alanı sabit kalır; hareket bir şey anlatır ya da dikkati doğru yere çeker, süs için yapılmaz.
  - Hızlı Okuma ana sayfasında odak çizgisi sıradaki egzersize kayar; maxkoç'ta görev tamamlanınca onay işareti çizilir.
- **Egzersizlerde klavye:** kelimeler arasında ok tuşlarıyla gezinilir; her kelime ayrı sekme durağı olmaz. Süreli egzersizde duraklat düğmesi olur.

## Yazı ve dil
- **Hitap:** öğrenciye ve veliye "sen", öğretmene ve kuruma "siz".
- **Sözcük seçimi:**
  - Düğmeler ne olacağını söyler: "Egzersize başla", "Bitirdim".
  - İngilizce ya da teknik ad görünmez: WPM yerine "kelime/dk", "Dashboard" yerine "Ana sayfa".
  - Yüzde işareti önde yazılır: %86.
- **Biçim:**
  - Büyük harfli etiket kullanılmaz; orta noktalı bilgi satırı ve "→" eklenmiş bağlantı da kullanılmaz.
- **Boş ve hata durumları:**
  - Boş ekran ne yapılacağını söyler.
  - Hata ne olduğunu ve nasıl düzeleceğini söyler.

## Yeni ekran yaparken kontrol listesi
1. Açık ve koyu tema, 390 px telefon ve 1440 px masaüstü görünümüne bakıldı.
2. Bütün yazı ve zemin çiftleri ölçüldü (4,5:1 ve 3:1).
3. Sabit renk yok; değişkenler kullanıldı.
4. Ekran başına tek ana düğme var; ikincil eylemler geride.
5. Yükleniyor, boş ve hata durumları tasarlandı.
6. Hareket azaltma ayarına uyuldu; hareket yukarıdaki ölçünün içinde (açılışta bir kez, üstüne gelince, grafik büyümesi).
7. Klavyeyle her şeye ulaşılıyor ve odak görünüyor.
8. Türkçe karakterler ve uzun sözcükler taşmıyor.
