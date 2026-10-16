# Hızlı Okuma · Türkçe raporlar · 16.10.2026

Yalnız personel portalı ve öğrenci arayüzü yayımlanır. API, veritabanı şeması, abonelik ve rol kayıtları değiştirilmez. Uygulama revizyonu `985e7597d658098a2329f65cf91846fb551e22f3`; doğrulama ve ekran kanıtları [yayın raporunda](../../../docs/reports/speed-reading-report-localization-20261016.md).

Kaynaklar önceki canlı `no-measurement-reports-20261016` izole derlemelerinden türetildi. Personel tarafında yalnız dört, öğrenci tarafında sekiz runtime dosyası değişti; iki derleme aynı yeni `clients/shared/utils/speed-reading-report-labels.ts` dosyasını kullanır. İlgisiz çalışma ağacı değişiklikleri pakete alınmaz. Personel derlemesi `/staff/` base-href kullanır.

`prepare → build → preview → promote → validate` sırası uygulanır. Bu tamamlanmış yayında `promote` tekrar çalıştırılmaz. Prepare üç mevcut taban imajını önceki yayın kayıtlarıyla karşılaştırır. Önceki çalışan compose kullanılır; personel/öğrenci geri dönüş imajları SHA ile sabitlenir. Çözülmüş compose ortamı gerçek konteyner Env değerleriyle birebir eşleşmelidir. Ortam/compose dosyaları erişimi kısıtlı sunucu dizininde kalır.

Dockerfile'lar önceki canlı imajların üstüne yalnız statik derlemeleri ekler. Eski hashli varlıklar açık sekmeler için tutulur; eski index sıkıştırmaları ve öğrenci eski servis çalışanı manifesti kaldırılır. Manifest 154 payload dosyasının SHA-256 değerini doğrular. Önizleme iki hedefte HTML, doğru base-href ve ana JS için kesin HTTP 200 ister.

Promote iki yayın kilidini alır ve yalnız iki hedefi `--no-deps` ile sırayla günceller. API dahil diğer üretim konteynerlerinin kimlikleri korunmalıdır. Hata halinde her iki eski imajın geri yüklenmesi ve sağlıkları doğrulanır. Veritabanı bağlantısı veya migration adımı yoktur. Veri koruma ayrı salt okunur test öğrencisi başlangıç/bitiş JSON karşılaştırmasıyla doğrulanır.

`observe.sh` salt okunur sağlık, imaj, diğer konteyner kimlikleri, dış HTTP ve API hata/gecikmesini kontrol eder. `monitor.sh` yayın işaretinden itibaren en az 15 dakika gözlem yapar; başarılı sonunda `monitoring-complete.txt` yazar. Sunucu dizini `/var/lib/eduivme/releases/report-localization-20261016`.
