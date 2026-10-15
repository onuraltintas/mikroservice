# Okuma kataloğu ve günlük ilerleme · 15.10.2026

Ürün kaynağı `2768e42bca405dbabac4ca9747e09bca893f0103`. Kapsam, testler ve canlı kanıtlar [yayın raporunda](../../../docs/reports/reading-content-progress-20261015.md).

Bu dizin özgün operasyon betikleri, hash manifesti, katalog snapshot'ı ve doğrulayıcı kaynağını saklar. Büyük payload, ortam sırları ve yedekler Git'te tutulmaz. Özgün erişimi kısıtlı yayın dizini `/var/lib/eduivme/releases/reading-content-progress-20261015`.

`payload.sha256` yayımlanan arşivin özgün manifestidir ve sunucudaki arşiv içeriği için kullanılır. Depo kopyasında doğrulayıcının proje yolu taşınabilir hale getirildi ve katalog JSON'unun satır sonu boşlukları temizlendi; bu dosyaların depo kopyası özgün arşivle bayt düzeyinde aynı değildir. 103 katalog kimlik/tür/yaş/seviye kaydı değişmedi.

Akış `prepare → build → drill → promote → validate`. Aday gerçek önceki API/öğrenci imajlarından hazırlanır. API'ye yalnız Infrastructure DLL/PDB kopyalanır; diğer API katmanları ve diğer servisler korunur. Frozen compose, iki dağıtım kilidi, manifest/imaj/DLL hashleri, iki servisle sınırlı `--no-deps` geçiş ve tam önceki imajlara doğrulanan geri dönüş korunur. Migration yoktur. Eski hashli JavaScript dosyaları tutulur.

Doğrulayıcı yalnız `READING_PROGRESS_DRILL_CONNECTION` ortamından bağlantı alır; yalnız `reading_content_progress_drill_20261015` adlı ayrı kopyayı kabul eder. 103 katalog kaydı kimlik/tür/yaş/seviye bazında eşleşmelidir. Adayın gerçek `/app/*.dll` dosyaları kullanılır. Yayın kapısı tam 103 başarılı / 0 hata / 6 yanlış yaş reddi ister. Prova kopyası sonra kaldırılır.

Kaynağı izole API ağacından derlemek için `-p:SpeedApiSourceRoot=/path/to/backend/` ve sabit `-p:SourceRevisionId=2768e42bca405dbabac4ca9747e09bca893f0103` kullanılır. API payload'ı ve doğrulayıcı aynı publish çıktısından hazırlanır; DLL eşleşmesi zorunludur. Depodaki proje varsayılan olarak normal depo Infrastructure projesine referans verir.

`observe.sh` salt okunur sürüm/servis sağlığı, public HTTP, restart ve yayın sonrası API hata/gecikme özeti verir; CAPTCHA açık ve 0,5 eşikte olmalıdır. Yayın tamamlandıktan sonra promote tekrar çalıştırılmaz.
