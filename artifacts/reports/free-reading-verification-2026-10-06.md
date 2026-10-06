# Serbest Okuma — düzeltme ve doğrulama

6 Ekim 2026. Çalışma yereldir; GitHub'a gönderilmedi, canlıya alınmadı. Migration gerekmedi.

## Tamamlanan kapsam

1. Serbest Okuma'nın bitirme düğmesi motora bağlandı. Yazı boyutu ve kaydırma bilgisi motora/ekrana doğru aktarılır. Sorusuz pratikte düğme “Okumayı Bitir”, sorulu değerlendirmede “Okudum, Sorulara Geç” der.
2. Özel önizlemede yazı boyutu, satır aralığı ve minimum/maksimum okuma süresi ayarlanabilir. Önizleme normal ilerlemeyi kalıcı değiştirmez.
3. Motor yeniden açılırken eski zamanlayıcı temizlenir. Başlamamış, durmuş veya yok edilmiş motor duraklatılamaz/devam ettirilemez. Görünen motor adı Serbest Okuma'dır.
4. Normal metin kaynağı Hızlı Okuma ReadingTexts kataloğudur. Otomatik seçim aynı zorluk seviyesinde, uygun yaş/egzersiz bağlantısında ve min/max kelime sınırında yapılır. Doğrudan metin kimliği verilen istekte de seviye ve uzunluk sınırları doğrulanır. Uygun metin yoksa başka seviyeye sessiz geçiş yapılmaz.
5. Metinler öğrencinin önceki egzersiz oturumlarına göre daha az kullanılmış içerik önceliğiyle seçilir. Havuz tükendiğinde yeniden kullanım mümkündür.
6. Sunucu minimum/maksimum süreyi doğrular. Yeni Serbest Okuma oturumlarında kelime sayısı gerçek içerikten hesaplanır; aktif okuma zamanı milisaniye hassasiyetinde, duraklama süresi düşülerek WPM hesabında kullanılır. Formül kelime sayısı × 60 / aktif saniyedir. Üç saniyeden kısa veya 20–1500 WPM dışında kalan değerler geçerli hız üretmez.
7. Sonuç sunucudan alınır; tek WPM kartı gösterilir. Sorusuz pratikte “Anlama ölçülmedi; tempo, okumayı bitirdiğiniz beyanına dayanır” açıklaması bulunur. Tempo tek başına anlama başarısı değildir. Varsayılan pratik sorusuzdur; evaluation seçimi mevcut anlama akışını kullanır.

## Test sonuçları

- TDD: öğrenci bağlantısı/ayarlar için 2 başarısız test → 2 geçti; motor yaşam döngüsünde 2 başarısız test → ortak motor 16 geçti; sunucu seçim/süre için 3 başarısız test → 3 geçti; doğrudan kimlik uzunluk sınırı için 2 başarısız test → 2 geçti.
- Tam ön yüz: 623/623 geçti.
- Tam Hızlı Okuma sunucu birim/regresyon paketi: 900/900 geçti.
- Gerçek PostgreSQL: 1/1 geçti; kullanıcı izolasyonu, minimum süre, 30.25 saniye aktif süre için 198.35 WPM, tek sonuç/okuma geçmişi kaydı ve sonraki metin rotasyonu doğrulandı. Geçici Testcontainers veritabanıdır, canlı değildir.
- Masaüstü ve Pixel 5 mobil: başarılı kayıt ve 503 kayıt hatası senaryoları, 4/4 geçti. Başarı akışı başlangıç/bitiş olaylarını ve tek kayıt isteğini, fontu, sonuç temposunu ve açıklamayı doğrular. Testte yakalanan mükerrer sonuç kartı kaldırıldı.
- Son Angular production build geçti.
- Motor kapsamı: statements %88.63, branches %85.18, functions %84, lines %88.61. Bu ilgili ortak okuma motorudur, tüm uygulamanın kapsaması değildir.
- Bağımsız C# ve TypeScript incelemelerinde son düzeltmelerden sonra engelleyici bulgu bulunmadı.

## Sınırlar

- Tarayıcı testleri gerçek Angular arayüzünü kontrollü API/Identity yanıtlarıyla çalıştırır; canlı kullanıcı oturumuyla test değildir. PostgreSQL testi gerçek persistence servisini kullanır.
- Uzunluk sorgusu kataloğun WordCount alanını kullanır; oturum hızının kelime sayısı gerçek metinden yeniden sayılır. Eski kataloglarda hatalı WordCount varsa katalog düzeltmesi ayrıca gerekir.
- Mevcut eski oturumlar önceki saniye hassasiyetini taşıyabilir; yeni oturum açılması yeni kuralları uygular.
- Ortak exercise-player SCSS için önceden bulunan 120 kB bütçesine karşı 128.56 kB uyarısı sürer. İlgisiz TextFade çalışma ağacı değişiklikleri korunmuştur.
