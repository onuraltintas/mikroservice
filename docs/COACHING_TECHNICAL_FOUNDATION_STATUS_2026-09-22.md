# Koçluk teknik temel: doğrulanmış durum (22 Eylül 2026)

Bu belge yalnız mevcut çalışma ağacında çalıştırılmış kontrolleri ve henüz
kanıtlanmamış kapıları ayırır. “%100” veya production onayı anlamına gelmez.

## Doğrulanmış

- `dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj
  --configuration Release`: 611 başarılı, 0 başarısız. Gateway'in çalışan
  process gerektiren 2 testi bu koşuda atlandı; ayrı smoke kapısı CI'de var.
- Disposable Docker veri gizliliği E2E: Identity, Coaching, Notification ve
  Speed Reading ayrı veritabanlarındaki hesap silme, tekrar teslim tekilliği
  ve aktif legal hold altında silmeme; 5 test başarılı.
- İzole MinIO container'lı entegrasyon testi: Coaching silme servisi DB ek
  kaydını ve gerçek nesneyi kaldırdı. Bu, staging'deki tam dağıtık zincirin
  yerine geçmez.
- MinIO imajının mevcut Docker Hub adresi çekilemedi; aynı sabit sürümün resmi
  Quay adresi indirildi. Compose adresi düzeltildi ve config kontrolü geçti.
- CI, .NET 10 SDK ile `net10.0` projelerini derleyecek şekilde eşitlendi.
  Dağıtık gizlilik E2E işi container build için zorunlu bağımlılık yapıldı.
- Disposable Koçluk backup/restore testi, mevcut veritabanından yalnız şemayı
  alıp sentetik bir hedefi yedekledi ve yeni veritabanına geri yükledi. Geçici
  veritabanları temizlendi; CI gizlilik işine aynı prova eklendi.
- Yönetim paneli testleri: 181/181 başarılı; production browser/SSR build'i
  tamamlandı. Build'de Hızlı Okuma katalog stil bütçesi uyarısı var; Koçluk
  doğrulaması adına bu ayrı platformun değişikliklerine dokunulmadı.

## Kapsam ölçümü

`--collect:"XPlat Code Coverage"` ile 608 testin başarılı olduğu önceki koşudan
alınan Cobertura raporu: toplam instrumented assembly satır kapsamı %11,34;
Coaching.API %3,23, Coaching.Application %51,72, Coaching.Domain %73,02,
Coaching.Infrastructure %8,48. Bu oranlar **yalnız bu entegrasyon test projesinin**
kapsamıdır; ayrı unit/UI/E2E koşularıyla birleştirilmiş ürün kapsamı değildir.
Özellikle `DataPrivacyController` %0, MinIO adaptörü %64,86 görünürken
Coaching erasure execution servisi bu raporda %100'dür. Yüksek test sayısı,
genel %80 kapsam hedefini kanıtlamaz.

## Kapanması gereken kapılar

1. Koçluk export, yetkilendirme ve tenant sınırı için HTTP seviyesinde
   olumlu/olumsuz E2E; kısmi servis kesintisi ve retry senaryoları.
2. Kullanıcının kendi VPS'inde çalışacak, güvenlik bakımından desteklenen S3
   uyumlu depoda gerçek yükle–oku–sil ve staging'deki broker + PostgreSQL +
   nesne depolama zinciri; erişim, TLS, bucket yetkileri, kimlik bilgisi
   rotasyonu, mevcut nesnelerin taşınması ve VPS dışı yedek onayı. Garage bir
   adaydır; canlı MinIO değişimi henüz onaylanıp yapılmadı.
3. CI işinin GitHub Actions'ta yeşil çalıştığına dair kayıt. Yerel YAML ve
   Compose doğrulaması bu kanıtın yerine geçmez.
4. Staging migration-only/canary, gerçek veri kapsamlı yedek–geri yükleme ve
   yeniden-imha provası; hata oranı, gecikme ve alarm alıcılarının gözlenen
   sonucu. Yerel şema/sentetik kayıt provası bunun yerine geçmez.
5. Dış güvenlik testi ve bulguların kapatılması. Eski MinIO OSS sürümünün
   production güvenlik onayı verilmedi; depolamanın VPS'te kalması seçildi.

Bu kapılar kapanmadan teknik temel “%100” veya production-ready olarak
işaretlenmez.
