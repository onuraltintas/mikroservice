# 🌐 API Gateway Araştırma ve Best Practice Raporu

> Araştırma notu: Bu dosya karar geçmişidir. Güncel gateway route ve deployment
> sözleşmeleri için `docs/BOUNDED_CONTEXT_CONTRACTS.md`, compose dosyaları ve
> `infrastructure/caddy/` altındaki aktif edge konfigürasyonları esas alınır.

## 1. Yönetici Özeti
.NET tabanlı mikroservis mimarilerinde endüstri standardı ve "State of the Art" yaklaşım artık **Microsoft YARP (Yet Another Reverse Proxy)** kütüphanesidir. Eski popüler kütüphane olan *Ocelot* artık aktif geliştirilmemekte ve performans sorunları yaşatmaktadır. Nginx/HAProxy gibi çözümler ise "Ingress/Load Balancer" katmanında kalmalı, "Application Gateway" katmanında .NET entegrasyonu (Auth, Logging, Policy) güçlü olan YARP tercih edilmelidir.

---

## 2. Teknoloji Karşılaştırması

| Özellik | **YARP (Önerilen)** | Ocelot | Nginx |
|---------|---------------------|--------|-------|
| **Geliştirici** | Microsoft (.NET Team) | Topluluk (Aktif Değil) | F5 |
| **Performans** | ⭐⭐⭐⭐⭐ (Çok Yüksek) | ⭐⭐⭐ (Orta) | ⭐⭐⭐⭐⭐ (Çok Yüksek) |
| **.NET Entegrasyonu** | %100 Native | Native | Yok (Lua script gerekir) |
| **Bakım/Destek** | Aktif/Resmi | Yavaşladı | Standart |
| **Protokol** | HTTP/1.1, HTTP/2, gRPC | HTTP/1.1 | Tümü |
| **Özelleştirme** | C# Middleware ile Sınırsız | Sınırlı Konfigürasyon | Konfigürasyon Dosyası |

### Neden YARP?
1.  **Performans:** Kestrel üzerine kurulu olduğu için .NET dünyasının en hızlı reverse proxy'sidir.
2.  **Özelleştirilebilirlik:** Standart bir ASP.NET Core uygulaması olduğu için, Authentication, Authorization, Rate Limiting, CORS gibi tüm middleware'leri olduğu gibi kullanabilirsiniz.
3.  **Destek:** Microsoft tarafından geliştirilmektedir ve Azure App Service'in altyapısını oluşturur.

---

## 3. Sektör Best Practice'leri (En İyi Uygulamalar)

### 3.1. Gateway Offloading (Yük Aktarımı)
Servislerin her birinde tekrar eden "Cross-Cutting Concerns" işlemleri Gateway'e taşınmalıdır:
*   **SSL Termination:** HTTPS -> Gateway -> HTTP -> Microservices.
*   **Authentication (Ön Kontrol):** Gateway'de token'ın geçerliliği (imza kontrolü) yapılmalı, ancak detaylı yetki (Authorization) servise bırakılmalıdır (Zero Trust).
*   **Rate Limiting:** Kötü niyetli veya hatalı istemcilerin servisleri boğmasını engellemek için Gateway'de istek limiti konulmalıdır.

### 3.2. Routing Pattern
API Gateway, istemcileri (Frontend/Mobile) iç yapıdan soyutlamalıdır.
*   **Client Görür:** `api.eduplatform.com/users`
*   **Gateway Yönlendirir:** `identity-service:8080/api/users`

### 3.3. BFF (Backend For Frontend) Pattern
Eğer Mobil ve Web için farklı veri şekilleri gerekiyorsa, tek bir Gateway yerine "Web Gateway" ve "Mobile Gateway" olarak ayrılabilir. (Şimdilik tek Gateway MVP için yeterlidir).

### 3.4. Resiliency (Dayanıklılık)
Şifreli iletişim hatası veya servis çökmesi durumunda Gateway'in tüm sistemi kilitlememesi gerekir.
*   **Timeout:** Servis yanıt vermezse Gateway hemen hatayı dönmelidir.
*   **Circuit Breaker:** Bir servis sürekli hata veriyorsa, Gateway o servise trafiği geçici olarak kesmelidir.

---

## 4. EduPlatform Gateway Mimarisi

```mermaid
graph TD
    Client[Web/Mobile Client] -->|HTTPS| CloudLB[Cloud/Nginx Load Balancer]
    CloudLB -->|HTTP| YarpGW[YARP API Gateway (.NET 9)]
    
    subgraph "Core Services"
        YarpGW -->|/api/auth/*| Identity[Identity Service]
        YarpGW -->|/api/coaching/*| Coaching[Coaching Service]
        YarpGW -->|/api/content/*| Content[Content Service]
    end

    subgraph "Capabilities"
        YarpGW -.->|Validate| Auth[Token Validation]
        YarpGW -.->|Control| RateLimit[Rate Limiting]
        YarpGW -.->|Log| Serilog[Central Logging]
    end
```

---

## 5. Uygulama Planı (Implementation Plan)

### Adım 1: Proje Kurulumu
*   `EduPlatform.Gateway` adında yeni bir boş **ASP.NET Core Web API** projesi oluşturulacak.
*   Nuget: `Yarp.ReverseProxy` paketi eklenecek.

### Adım 2: Konfigürasyon (appsettings.json)
*   **Clusters (Kümeler):** Hedef servisler tanımlanacak (IdentityService, CoachingService).
    *   *Örnek:* `cluster_identity` -> `http://localhost:5001`
*   **Routes (Rotalar):** Gelen isteklerin hangi cluster'a gideceği belirlenecek.
    *   *Örnek:* `/api/auth/{**catch-all}` -> `cluster_identity`

### Adım 3: Middleware Entegrasyonu
*   **Rate Limiting:** `.NET 7+ RateLimiter` middleware'i eklenecek (IP bazlı limit).
*   **CORS:** Frontend'in erişmesi için global CORS politikası.
*   **Health Checks:** Gateway'in sağlığını kontrol eden endpoint.

### Adım 4: Güvenlik (Security Headers)
*   Güvenlik headerları (HSTS, X-Content-Type-Options vb.) eklenecek.

### Adım 5: Servis Bağlantısı (Service Discovery)
*   Docker ortamında servis isimleri (hostname) üzerinden, Local ortamda localhost portları üzerinden çalışacak şekilde yapılandırılacak.
