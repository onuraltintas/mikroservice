# Haftalık kazanım planı canlı yayını

İlk uygulama kaynağı `119fc711`, önceki canlı taban `12951c5a`.
Son düzeltme `5411fc01`, yayın dizini `coaching-weekly-outcomes-20261017-r2`.
Kapsam yalnız Coaching API ve admin-panel içindeki öğrenci ekranlarıdır.

`prepare.mjs` önceki doğrulanmış kaynak kopyasına yalnız iki ürün yolundaki commit
farkını uygular; ilgisiz çalışma klasörü değişiklikleri alınmaz. Dört Coaching DLL/PDB
ve Angular SSR çıktısı paketlenir. Shared bağımlılıklar mevcut canlı imajdan korunur.
İlk arayüz paketi `playwright.coaching-weekly-outcome-release.config.mjs` ile 18 testten
geçti. R2 yalnız konu hiyerarşisi sırasını değiştirir; arayüz paketi aynıdır.

Sıra: kaynak hazırlama/derleme/test → paket/özet → stage/yükleme/unpack →
`deploy.sh prepare`, `build`, `drill`, `promote`, `validate` → tarayıcı/SQL doğrulaması
ve 15 dakikalık `monitor.sh`.

Yükleme bilinen SSH anahtarıyla, `StrictHostKeyChecking=yes` kullanılarak yapılır.
`remote.mjs` önceki `coaching-outcomes-20261016` dizinindeki gözden geçirilmiş araçtır.
Betikler aynı iki dağıtım kilidini alır, mevcut imaj kimliklerini kontrol eder, yalnız
iki hedef servisi değiştirir ve diğer container kimliklerinin aynılığını sınar.

İlk yayında eklenen dört nullable sütun eski satır özetinden çıkarılır. Mevcut
`study_weekly_drafts` verisi geçiş özetine dâhildir. R2'de yeni sütunlar da mevcut
veri olduğundan özetlerin hiçbir alanı çıkarılmaz. Üretim kopyasında geçiş iki kez
uygulanır. Canlıda Identity/Coaching özel yedekleri alınıp geçiş öncesi/sonrası bütün
iş tabloları karşılaştırılır. Tarayıcı testinden sonra yeni test planlarının eklenmesine
izin verilerek bütün önceki satır özetlerinin kaldığı doğrulanır.

İlk test taslağı konu sırası düzeltmesi öncesinde arşivlenmiştir. R2 tabanı bu arşiv
durumunu içerir. Son test taslağı ayrı kimlikle kalır; aktif plan yayımlanmaz.
Test öğrencisinin haftalık düzeni 300 dk Matematik olacak şekilde bilinçli güncellenir.
Haftalık satırın bu değişikliği tarayıcı sonrası eski satır karşılaştırmasından ayrı tutulur.

Geri dönüş tetikleri: readiness kaybı, yeniden başlatma, eski kayıt özetinde fark veya
öğrenci akışının çalışmaması. Hata tuzağı önceki uygulama imajlarını geri getirir;
şemayı, yeni taslakları veya yedekleri silmez. Otomatik Down veya veri geri yükleme yoktur.
Özel compose/ortam/yedek dosyaları sunucuda kalır, Git'e alınmaz.

Sonuçlar: [canlı raporu](../../../docs/coaching/weekly-outcome-live-20261017.md).
