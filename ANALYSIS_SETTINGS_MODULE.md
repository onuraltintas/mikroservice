# Mikroservis Projesi: Gelişmiş Ayarlar ve Yönetim Modülü Analizi

## 1. Genel Bakış
Mevcut proje yapısı (.NET 9 Microservices, Angular, Docker) göz önüne alındığında, Admin Paneli'ne eklenecek "Ayarlar" modülü statik bir formdan ziyade, sistemin canlı durumunu izleyen ve yöneten bir kontrol merkezi olmalıdır.

Bu analiz, sisteme eklenebilecek 5 ana yönetim katmanını ve uygulama stratejilerini önerir.

---

## 2. Önerilen Modüller

### A. Merkezi Log Yönetimi (System Logs) ✅ **TAMAMLANDI**
**Amaç:** Konsol veya dosya loglarına erişmek yerine, tüm servislerin loglarını Admin Panel üzerinden filtrelenebilir şekilde görüntülemek.

*   **Teknik Altyapı:**
    *   **Mevcut:** Serilog ile Console + Seq entegrasyonu.
    *   **Uygulanan Çözüm:** `Serilog.Sinks.Seq` kullanarak logları Seq Server'a (Docker container) yazmak.
    *   **Seq UI:** `http://localhost:5341` adresinden doğrudan erişilebilir.
*   **Admin Paneli Özellikleri:**
    *   Filtreleme: Servis Bazlı (Identity, Notification...), Seviye (Error, Warning), Tarih Aralığı.
    *   Özellik: Log detayına tıklandığında StackTrace'i formatlı gösterme (`Exception` detayı).
    *   İşlev: "Son 1 saatteki Hatalar" widget'ı.
*   **Eklenen Özellikler:**
    *   ✅ **Log Retention Yönetimi:** Admin panelinden log saklama sürelerinin (Gün/Filtre bazlı) yönetilmesi.
    *   ✅ **Oto-Sinyal Oluşturma:** Log seviyesine göre (Information, Warning) otomatik Seq sinyalleri oluşturulup retention politikalarına bağlanıyor.

### B. Audit Logs (İşlem İzleme)
**Amaç:** Yöneticilerin ve kullanıcıların sistem üzerindeki kritik işlemlerini (Ekleme, Silme, Güncelleme) kayıt altına almak. "Log" teknik hataları, "Audit" ise iş süreçlerini takip eder.

*   **Teknik Altyapı:**
    *   Interceptor veya MediatR Pipeline Behavior kullanarak `SaveChanges` öncesi yapılan değişiklikleri yakalamak.
    *   Örnek Kayıt: `User: Admin1, Action: UpdateUser, Target: User2, Changes: { Role: Student -> Admin }`.
*   **Admin Paneli Özellikleri:**
    *   "Kim, neyi, ne zaman değiştirdi?" tablosu.
    *   Eski ve Yeni değerlerin yan yana (Diff View) gösterimi.

### C. Dinamik Konfigürasyon Yönetimi (Dynamic Configuration)
**Amaç:** Projeyi yeniden başlatmadan (Deploy yapmadan) sistem davranışlarını değiştirmek.

*   **Uygulama:**
    *   `Configuration` adında bir tablo ve Key-Value (JSON) yapısı.
    *   Servisler bu ayarları **Redis** üzerinden okur. Admin panelinden güncellenince Redis cache temizlenir ve servisler yeni ayarı alır.
*   **Neler Yönetilebilir?**
    *   **Feature Flags:** "Yeni Dashboard Tasarımı Aktif mi?" (True/False).
    *   **Notification:** "Maksimum mail gönderme hakkı".
    *   **System:** "Bakım Modu" (Tüm API'lerin 503 dönmesini sağlar).
    *   **JWT:** Token (Access/Refresh) süreleri.

### D. Bildirim Şablonu Yönetimi (Notification Templates)
**Amaç:** E-posta HTML şablonlarını kod içinde statik tutmak yerine panelden yönetmek. (Zaten altyapısını kurduk, UI tarafı eksik).

*   **Admin Paneli Özellikleri:**
    *   HTML/Rich Text Editör (Angular Editor veya Monaco Editor).
    *   Placeholder Listesi (`{{UserName}}`, `{{Link}}` gibi kullanılabilir değişkenlerin gösterimi).
    *   "Test Gönder" butonu.

### E. Sistem Sağlığı (Health Checks)
**Amaç:** Mikroservislerin ve altyapı bileşenlerinin ayakta olup olmadığını tek ekrandan görmek.

*   **Teknik Altyapı:**
    *   `.NET HealthChecks` kütüphanesi.
    *   Her servis için `/health` endpoint'i.
*   **Admin Paneli Özellikleri:**
    *   Trafik Lambası Görünümü:
        *   🟢 Database (Connected)
        *   🟢 Redis (Connected)
        *   🔴 RabbitMQ (Disconnected - Alert!)
    *   Disk ve RAM kullanımı (Opsiyonel).

---

## 3. Uygulama Yol Haritası (Implementation Road Map)

### Faz 1: Loglama Altyapısı (Log Viewing) 🚀 **(Önerilen Başlangıç)**
Kullanıcının öncelikli isteği "Logları görmek".
1.  Admin Panel'de `Settings/Logs` sayfası oluşturulacak.
2.  Backend'de (Identity Service veya yeni bir Shared Logging Service) logları veritabanına yazan yapı kurulacak.
3.  Logları sorgulayan performanslı bir API endpoint (`GET /api/logs`) yazılacak.

### Faz 2: Dinamik Ayarlar (Configuration)
1.  `Configurations` tablosu oluşturulacak.
2.  Admin Panel'de Key-Value editör yapılacak.
3.  Backend servislerine `ConfigurationService` entegre edilecek.

### Faz 3: Diğerleri
Notification Template Editörü ve Health Checks sonraki adımlarda eklenebilir.

---
**Karar:** İşe **Faz 1 (Loglama Altyapısı)** ile mi başlayalım, yoksa doğrudan bir **Konfigürasyon Yönetimi** mi istersiniz?
