# .NET 10 LTS Geçiş Planı

Proje şu anda .NET 9 kullanır. Microsoft'un destek tablosuna göre .NET 9'un
destek bitişi 10 Kasım 2026, .NET 10 LTS'in destek bitişi 14 Kasım 2028'dir.
Kaynak: <https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core>.

## Hedef ve kapsam

Hedef, production Docker imajlarını ve tüm `net9.0` proje hedeflerini .NET 10'a
taşımaktır. Geçiş bir framework yükseltmesidir; production veritabanına el ile
şema değişikliği yapmaz. EF migration'lar yalnız olağan release akışındaki
`--migrate-only` servisiyle uygulanır.

## Kapanış kapıları

1. Ayrı bir `codex/dotnet-10-lts` dalında SDK, Docker base image'ları, target
   framework'ler ve merkezi paket sürümleri güncellenir.
2. `dotnet restore`, uyarıları hata kabul eden Release build, Speed Reading
   birim testleri, entegrasyon testleri ve Angular production buildleri geçer.
3. CI'nin EF model drift kontrolü, Compose/monitoring config denetimleri ve
   container build işleri yeşil kalır.
4. Staging ortamında migration-only, giriş, hızlı okuma assessment, admin
   katalog, rapor ve ödeme dışı kritik akışlar doğrulanır.
5. Production'da önce migration servisi, sonra tek servislik canary deploy
   yapılır. Health/readiness, hata oranı ve p95 gecikme en az 15 dakika
   izlenir.
6. Sorunda sadece değişen image tag'leri önceki doğrulanmış sürüme geri alınır;
   veri silme veya rollback SQL'i çalıştırılmaz.

## Zamanlama

- **Eylül 2026:** bağımlılık uyumluluğu ve staging denemesi.
- **Ekim 2026:** canary ve üretim geçişi.
- **10 Kasım 2026'dan önce:** .NET 9 image tag'lerinin aktif production
  rotasından çıkarılması.

Her production geçişinde commit/image tag'i, migration sonucu, health/readiness
çıktısı ve gözlem süresi release kaydına eklenir.
