# Kelime Hazinesi — yerel geliştirme ve doğrulama

Tarih: 6 Ekim 2026. Kapsam: Kelime Hazinesi motoru, öğrenci oynatıcısı, sunucu kelime seçimi ve sonuç kaydı. Bu çalışma GitHub'a gönderilmedi ve canlıya alınmadı. Veritabanı migration'ı gerekmedi.

## Tamamlanan adımlar

1. Duraklatma/devam zamanlayıcısı kalan süreyi korur; duraklama cevap süresine eklenmez. Başlamadan, duraklama sırasında ve durdurma sonrasında cevaplar işlenmez. Geç sunucu cevapları silinmiş/durdurulmuş motoru ilerletemez.
2. Öğrenme ve tekrar cevapları sunucu kaydı onaylanmadan ilerlemez. Hatalı kayıt yeniden denenebilir; yanlış biçimli onay skor üretmez. Duraklama sırasında gelen öğrenme onayı sonraki kelimeyi devam etme anına erteler. Quiz'in Sonraki düğmesi ve motoru da duraklamada ilerlemeyi engeller.
3. Leitner kutusu ve tekrar tarihi normal oturumlarda sunucu tarafından yönetilir. Yerel tarayıcı depolaması bunların kaynağı değildir. Özel önizleme kalıcı öğrenme ilerlemesini değiştirmez.
4. Tekrar modu yalnız oturum sahibinin zamanı gelmiş son ilerleme kayıtlarını seçer. Eski mükerrer ilerleme satırı güncel tekrar tarihini geçersiz kılamaz.
5. Kelimeler Hızlı Okuma'nın merkezi VocabularyItems kataloğundan gelir. Yaş uygunluğu, seçilmiş kimlikler veya kategori/zorluk filtresi uygulanır. Havuz belirtilmemişse merkezi katalog ve mevcut varsayılan zorluk/adet kullanılır. Boş içerik yeni oturum oluşturmadan anlaşılır hata döndürür.
6. Quiz gerçek kelime/anlam seçenekleri kullanır; sahte seçenek üretmez. Belirsiz mükerrer kelime/anlamlar elenir. En az iki farklı seçenek gerekir. Tekrar modu öğrenci arayüzünde görünür.
7. Yeni ve eski Kelime Hazinesi sonuçlarında WPM, okunan kelime sayısı ve ağırlıklı okuma hızı gösterilmez. Bu çalışma kelime öğrenme/tekrar çalışmasıdır; okuma hızı ölçümü değildir. Öğrenmede Biliyorum/Bilmiyorum öz bildirimi, quiz'de sunucunun doğruladığı cevap esas alınır.

## Doğrulama

- Ön yüz: 619/619 test geçti; bunların 24'ü Vocabulary motor regresyon testidir.
- Hızlı Okuma sunucu birim/regresyon paketi: 895/895 geçti.
- Gerçek PostgreSQL: öğrenme, tekrar ve quiz için 3/3 geçti. Başka kullanıcı erişimi, tekrar gönderimde tek kayıt, kutu/tarih ve sonuç ölçümleri doğrulandı. Geçici Testcontainers veritabanı kullanıldı; canlı veriye dokunulmadı.
- Masaüstü ve Pixel 5 mobil tarayıcı: öğrenme, tekrar ve quiz için 6/6 geçti. Her senaryoda dört kelime cevabı, tek sonuç kaydı, WPM göstermeme ve JavaScript çalışma zamanı hatası bulunmaması doğrulandı.
- Angular production build geçti. Önceden bulunan ortak exercise-player SCSS bütçe uyarısı sürüyor (120 kB sınırına karşı 128.56 kB); bu çalışma kapsamında geniş stil refaktörü yapılmadı.
- Vocabulary motor kapsaması: statements %98.73, branches %90.56, functions %98.24, lines %99.64. Bunlar ilgili motorun oranlarıdır; tüm uygulamanın kapsamı değildir.
- Bağımsız C# ve TypeScript incelemelerinde son düzeltmelerden sonra engelleyici bulgu kalmadı.

## Sınırlar

- Kelime adedi üst sınırdır; arayüz gerçek seçilmiş kelime sayısını kullanır. Rastgele en fazla 500 aday aktarılır ve benzersiz içerikler seçilir. Çok mükerrer bir katalog daha kısa bir oturum üretebilir. PostgreSQL rastgele sıralama maliyeti katalog büyüdükçe artar.
- Tarayıcı senaryoları gerçek Angular arayüzü ve kontrollü API/Identity yanıtlarıyla çalışır. Canlı Identity hesabıyla uçtan uca doğrulama yapılmış sayılmaz.
- Bu testler yazılım davranışını doğrular; eğitsel etkililik veya klinik/bilimsel başarı garantisi vermez.
- Diğer egzersizlere ait mevcut çalışma ağacı değişiklikleri ve geçici dosyalar bu çalışmaya dahil edilmedi.
