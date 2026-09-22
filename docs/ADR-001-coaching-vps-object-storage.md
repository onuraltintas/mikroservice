# ADR-001: Koçluk eklerini VPS üzerinde sınırlı yetkili S3 depoda tutma

**Durum:** Kabul edildi

**Tarih:** 22 Eylül 2026

## Bağlam

Koçluk öğrencilerinin yüklediği ekler, Hızlı Okuma'dan bağımsız Koçluk
verisidir. Kullanıcı dosyaların kendi VPS'inde kalmasını seçti. Uygulamanın
MinIO root hesabıyla çalışması, bucket dışına erişim ve credential rotasyonu
için gereksiz yetki verir. Tek VPS'teki Docker volume tek başına yedek değildir.

## Karar

Koçluk ekleri, VPS'te yalnız Docker iç ağında çalışan MinIO bucket'ında tutulur.
Production Compose, MinIO verisini `ATTACHMENT_MINIO_DATA_HOST_PATH` ile
şifreli VPS filesystem'ine bağlar. `minio-provision` işi bucket'ı oluşturur ve
Koçluk için root hesabından farklı, yalnız o bucket üzerinde listeleme/okuma/
yazma/silme izni olan uygulama hesabını tanımlar. Koçluk servisi provisioning
başarılı olmadan başlamaz.

## Alternatifler

| Seçenek | Sonuç |
| --- | --- |
| Uygulamanın MinIO root hesabı | Reddedildi: en az yetki ilkesini ihlal eder. |
| Uygulama container'ında yerel volume | Reddedildi: replica ve silme/toparlanma akışları için uygun değil. |
| Yönetilen S3 | Ertelendi: kullanıcı dosyaların VPS'te kalmasını seçti. |
| VPS MinIO + sınırlı uygulama hesabı | Seçildi: mevcut sistemle uyumlu, private network ve kontrollü geçiş sağlar. |

## Sonuçlar ve zorunlu kapılar

- `MINIO_ROOT_*` ile `ATTACHMENT_MINIO_*` değerleri farklı, rastgele ve repo
  dışı secret store'da tutulur.
- VPS'teki hedef dizin şifreli disk üzerinde, `root:root` sahipliğinde ve `0700`
  izinli olmalıdır; MinIO API/console portları public yayınlanmaz.
- Yedek, aynı VPS'te ikinci kopya olamaz. Şifreli ve erişim kontrollü harici
  hedef seçilmeden production onayı verilmez.
- Her credential rotasyonu önce staging'de yeni uygulama hesabı, yükle/oku/sil
  smoke testi ve eski hesabın kaldırılmasıyla doğrulanır.
- Bu karar tek VPS için dayanıklılık sağlar; yüksek erişilebilirlik veya farklı
  fiziksel arıza alanı sağlamaz.
