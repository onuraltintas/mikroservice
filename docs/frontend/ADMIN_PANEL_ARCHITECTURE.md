# EduPlatform Admin Panel - Frontend Architecture & Implementation Guide

> **Version:** 1.0.0
> **Framework:** Angular 19+ (Standalone)
> **UI Layer:** Angular Material 3 + TailwindCSS
> **Rendering:** SSR (Hydration Enabled)

Bu doküman, EduPlatform Admin Panel projesinin mimari kararlarını, kodlama standartlarını ve uygulama yol haritasını içerir. Tüm geliştirmeler bu standartlara uygun yapılmalıdır.

---

## 🏗 1. High-Level Architecture (Üst Düzey Mimari)

Proje, **Modüler Monolitik** bir yapıda, **Feature-Based** (Özellik Bazlı) klasörleme stratejisi izler. "Smart Components" (Container) ve "Dumb Components" (Presentational) ayrımı kesin çizgilerle uygulanır.

### Teknoloji Yığını
*   **Core:** Angular 19 (Standalone Components, Signals, New Control Flow `@if`, `@for`).
*   **State Management:** Angular Signals (Lokal ve Global State için).
*   **Async Operations:** RxJS (Sadece karmaşık streamler ve HTTP istekleri için).
*   **UI Library:** Angular Material (Material 3 Design Tokens).
*   **Styling Strategy:** Hybrid (SCSS + TailwindCSS).
*   **Form Management:** Reactive Forms (Typed).
*   **Validation:** Valibot veya Zod (Tip güvenli şema validasyonu için - opsiyonel) + Angular Validators.
*   **Build Tool:** Esbuild (Vite-based).

---

## 📂 2. Directory Structure (Klasör Yapısı)

Projeyi `src/app` altında aşağıdaki gibi yapılandıracağız:

```text
src/app/
├── core/                       # Uygulamanın 'Singleton' servisleri (Tek instance)
│   ├── auth/                   # Auth servisi, guardlar, interceptorlar
│   ├── config/                 # App config tokens, environment servisi
│   ├── interceptors/           # HTTP, Error, Loading interceptorları
│   └── services/               # Global, state-bağımsız servisler (ThemeService, Logger)
│
├── layout/                     # Uygulamanın ana iskeleti
│   ├── components/             # Sidenav, Toolbar, Footer
│   └── main-layout/            # Router-outlet'i saran ana component
│
├── shared/                     # Birden fazla feature tarafından kullanılan parçalar
│   ├── components/             # Reusable UI componentleri (ConfirmDialog, StatCard)
│   ├── directives/             # Custom direktifler
│   ├── pipes/                  # Custom pipelar
│   ├── models/                 # Paylaşılan DTO'lar, Interface'ler
│   └── utils/                  # Helper fonksiyonlar
│
├── features/                   # İş mantığını barındıran sayfalar (Lazy Loaded)
│   ├── auth/                   # Login, Register, Forgot Password
│   ├── dashboard/              # Ana panel, widgetlar
│   ├── identity/               # Kullanıcı ve Rol yönetimi
│   └── settings/               # Ayarlar
│
├── styles/                     # Global stil dosyaları
    ├── _variables.scss         # Materla 3 tokenlar
    ├── _mixins.scss
    └── _tailwind.scss          # Tailwind importları
```

---

## 🎨 3. Styling Strategy (Stil Stratejisi)

En kritik kuralımız **"Hybrid Approach"** (Hibrit Yaklaşım):

### ✅ Ne, Nereye Yazılır?

| Görev | Araç | Örnek |
| :--- | :--- | :--- |
| **Component Layout** (Yerleşim) | **TailwindCSS** | `flex`, `grid`, `gap-4`, `p-6`, `w-full`, `justify-center` |
| **Spacing & Sizing** (Boyutlandırma) | **TailwindCSS** | `min-h-screen`, `max-w-md`, `my-4` |
| **Typography Basics** (Hizalama) | **TailwindCSS** | `text-center`, `font-bold`, `uppercase` |
| **Theme Colors** (Renkler) | **Angular Material** | `color="primary"`, `mat-app-background` |
| **Bileşen Özelleştirme** | **SCSS** | `.mat-mdc-card { border-radius: 16px; }` |
| **Complex Animations** | **SCSS** | `@keyframes slideIn { ... }` |

### ❌ Yasaklar (Anti-Patterns)
*   **Yasak:** Layout için SCSS yazmak. (Örn: `.wrapper { display: flex; }` -> **YAPMA!** Yerine `class="flex"` kullan).
*   **Yasak:** Tailwind ile renk kodlarını hardcode etmek. (Örn: `bg-[#3f51b5]` -> **YAPMA!** Material Theme kullan).

---

## ⚡ 4. State Management (Durum Yönetimi)

NgRx gibi ağır kütüphaneler yerine, Angular 19'un yerel gücü olan **Signals** kullanılacak.

1.  **Lokal State:** Component içinde `signal()` ve `computed()`.
2.  **Global State:** Service'ler içinde `signal()` (Signal Store Pattern).

**Örnek Service:**
```typescript
@Injectable({ providedIn: 'root' })
export class SessionService {
  // Read-only signal dışarıya
  readonly user = this._user.asReadonly();
  
  // Private writable signal
  private _user = signal<User | null>(null);

  updateUser(user: User) {
    this._user.set(user);
  }
}
```

---

## 🔐 5. Authentication & Security

*   **Pattern:** Backend-for-Frontend (BFF) mantığına yakın, ancak frontend tarafında OIDC (OpenID Connect) akışı.
*   **Kütüphane:** `angular-auth-oidc-client` (Keycloak yönetimi için en stabili).
*   **Storage:** Token'lar `sessionStorage` veya `localStorage` (Beni Hatırla seçeneğine göre) tutulacak.
*   **Guard:** `canActivate` fonksiyonel guardlar ile rota koruması.
*   **Interceptor:** `AuthInterceptor`, giden her isteğin header'ına `Authorization: Bearer ...` ekleyecek.

---

## 🚀 6. Implementation Plan (Uygulama Haritası)

### Phase 1: Foundation (Temel)
- [ ] Angular projesinin oluşturulması (SSR, SCSS).
- [ ] TailwindCSS kurulumu ve konfigürasyonu.
- [ ] Angular Material kurulumu ve Custom Theme (Material 3) ayarı.
- [ ] Klasör yapısının oluşturulması (`core`, `shared`, `features`).

### Phase 2: Core Infrastructure (Altyapı)
- [ ] `AuthService` ve HttpClient kurulumu.
- [ ] Keycloak entegrasyonu (environment ayarları).
- [ ] Base Layout (Sidenav, Toolbar) tasarımı.
- [ ] Dark/Light mode switch implementasyonu.

### Phase 3: Auth Module (Kimlik Doğrulama)
- [ ] Login Sayfası (Glassmorphism tasarım).
- [ ] Register Sayfası.
- [ ] Forgot Password & Email Verification akışları.
- [ ] Form Validasyonları.

### Phase 4: Dashboard & Integration
- [ ] Dashboard Sayfası (Statik widgetlar).
- [ ] Identity API entegrasyonu (Kayıt olma, giriş yapma).
- [ ] SSR Hydration testleri.

### Phase 5: Dinamik yönetim (tamamlandı)

- [x] Server-issued permission claim'lerine bağlı functional route guard ve menü.
- [x] Identity kurum yaşam döngüsü yönetimi.
- [x] Notification destek inbox ve e-posta şablonu yönetimi.
- [x] SystemAdmin için salt-okunur Coaching operasyon özeti.
- [x] Yönetim kapsamı ve güvenlik sınırları: `docs/ADMIN_PANEL_MANAGEMENT_MATRIX.md`.

---

## 📱 7. Mobile Responsiveness (Responsive Stratejisi)

Proje **Mobile-First** yaklaşımıyla tasarlanacaktır. TailwindCSS breakpoint'leri standarttır:

*   **sm (640px):** Büyük telefonlar.
*   **md (768px):** Tabletler (Sidebar'ın `over` modundan `side` moduna geçtiği kırılma noktası).
*   **lg (1024px):** Küçük laptoplar.
*   **xl (1280px):** Masaüstü.

### Kurallar:
1.  **Sidebar:** Mobil cihazlarda varsayılan olarak **kapalı** ve `over` modunda (içeriğin üstüne binen) olmalıdır. Tablet ve üzerinde **açık** ve `side` modunda (içeriği iten) olmalıdır.
2.  **Tablolar:** Mobilde yatay scroll (`overflow-x-auto`) veya kart görünümü (Card View) kullanılmalıdır.
3.  **Grid Sistem:** Dashboard widget'ları mobilde 1 sütun, tablette 2 sütun, masaüstünde 3/4 sütun olmalıdır (`grid-cols-1 md:grid-cols-2 lg:grid-cols-4`).
4.  **Touch Targets:** Mobilde butonlar ve tıklanabilir alanlar en az 44px yükseklikte olmalıdır.

