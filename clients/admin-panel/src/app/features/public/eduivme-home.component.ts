import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SiteSeoService } from '../../core/services/site-seo.service';
import { EDUIVME_SITE_URLS } from '../../../../shared/eduivme-sites';

/**
 * Redesign 18.11.2026: eduivme.com has its own page instead of forwarding to the management panel.
 * eduivme is the parent brand; the page leads to its two products and to the staff addresses.
 */
@Component({
  selector: 'app-eduivme-home',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="site" data-brand="eduivme">
      <a class="skip ds-btn ds-btn--primary" href="#main-content">İçeriğe geç</a>
      <header class="site__header">
        <div class="site__bar">
          <a routerLink="/" class="site__brand" aria-label="eduivme ana sayfası">
            <img src="brand/eduivme.svg" alt="" width="40" height="40">
            <span class="site__name">eduivme</span>
          </a>
          <nav class="site__nav" aria-label="Sayfa içi">
            <a href="#urunler">Ürünler</a>
            <a href="#personel">Öğretmen ve kurumlar</a>
          </nav>
          <a [href]="urls.admin + '/auth/login'" class="ds-btn ds-btn--quiet">Yönetici girişi</a>
        </div>
      </header>

      <main id="main-content" tabindex="-1">
        <section class="hero" aria-labelledby="eduivme-title">
          <div class="hero__inner">
            <h1 id="eduivme-title">Öğrencinin çalışmasını görünür kılan araçlar.</h1>
            <p>eduivme; sınava hazırlanan öğrenciler, onların velileri, öğretmenleri ve kurumları için iki ürün geliştirir. Her biri tek bir işi iyi yapar.</p>
            <div class="hero__actions">
              <a href="#urunler" class="ds-btn ds-btn--primary ds-btn--large">Ürünleri incele</a>
              <a href="#personel" class="ds-btn ds-btn--secondary ds-btn--large">Öğretmen ve kurum girişi</a>
            </div>
          </div>
          <svg class="hero__mark" width="360" height="480" viewBox="0 0 520 700" aria-hidden="true">
            <rect class="hero__stem" x="210" y="300" width="130" height="420" rx="65"></rect>
            <path class="hero__arc" d="M150 200c40-80 120-120 190-108" fill="none" stroke-width="22" stroke-linecap="round"></path>
            <circle class="hero__dot" cx="370" cy="110" r="78"></circle>
          </svg>
        </section>

        <section id="urunler" class="section" aria-labelledby="products-title">
          <h2 id="products-title">Ürünler</h2>
          <div class="products">
            <article class="product" data-brand="maxkoc" aria-labelledby="product-maxkoc">
              <div class="product__head">
                <img src="brand/maxkoc.svg" alt="" width="48" height="48">
                <div><h3 id="product-maxkoc">maxkoç</h3><p class="product__kind">Öğrenci koçluğu</p></div>
              </div>
              <p>Boş saatlere göre haftalık çalışma planı, deneme netleri, ödevler ve koçla düzenli görüşmeler. Veli, çocuğunun izin verdiği kadarını kendi ekranından izler.</p>
              <ul>
                <li>LGS ve YKS öğrencileri için</li>
                <li>Öğrenci, veli ve öğretmen aynı planı görür</li>
              </ul>
              <div class="product__actions">
                <a [href]="urls.student" class="ds-btn ds-btn--primary">maxkoc.com</a>
                <a [href]="urls.parent + '/auth/login'" class="ds-link">Veli girişi</a>
              </div>
            </article>

            <article class="product" data-brand="ho" aria-labelledby="product-ho">
              <div class="product__head">
                <img src="brand/master-hizli-okuma.svg" alt="" width="48" height="48">
                <div><h3 id="product-ho">Master Hızlı Okuma</h3><p class="product__kind">Okuma hızı ve anlama</p></div>
              </div>
              <p>Seviye belirleme sınavıyla başlayan, her gün kısa egzersizlerle ilerleyen bir okuma programı. Okuma hızı ve anlama birlikte ölçülür.</p>
              <ul>
                <li>İlkokuldan yetişkine her seviye için</li>
                <li>Öğretmen ve kurum öğrencilerini izleyebilir</li>
              </ul>
              <div class="product__actions">
                <a href="https://masterhizliokuma.com" class="ds-btn ds-btn--primary">masterhizliokuma.com</a>
              </div>
            </article>
          </div>
        </section>

        <section id="personel" class="section" aria-labelledby="staff-title">
          <h2 id="staff-title">Öğretmen ve kurumlar</h2>
          <p class="section__lead">Öğretmenler ve kurum yöneticileri iki ürünü de aynı personel panelinden yönetir: öğrenci listeleri, ödevler, seanslar ve raporlar.</p>
          <div class="staff">
            <a class="staff__item" [href]="urls.teacher + '/auth/login'">
              <span class="staff__title">Öğretmen girişi</span>
              <span class="staff__text">ogretmen.eduivme.com</span>
            </a>
            <a class="staff__item" [href]="urls.institution + '/auth/login'">
              <span class="staff__title">Kurum girişi</span>
              <span class="staff__text">kurum.eduivme.com</span>
            </a>
            <a class="staff__item" [href]="urls.teacher + '/auth/register/teacher'">
              <span class="staff__title">Öğretmen olarak katıl</span>
              <span class="staff__text">Ücretsiz hesap aç, öğrencilerini davet et.</span>
            </a>
          </div>
        </section>
      </main>

      <footer class="site__footer">
        <nav class="site__links" aria-label="Yasal metinler">
          <a routerLink="/legal/privacy">Gizlilik politikası</a>
          <a routerLink="/legal/kvkk">KVKK aydınlatma metni</a>
          <a [href]="urls.admin + '/auth/support'">Yardım ve destek</a>
        </nav>
        <p>© {{ year }} ONAL Yazılım ve Otomasyon. Tüm hakları saklıdır.</p>
      </footer>
    </div>
  `,
  styles: [`
    :host { display: block; }
    h1, h2, h3, p, ul { margin: 0; }
    .site { min-height: 100vh; display: flex; flex-direction: column; background: var(--ds-bg); color: var(--ds-text); }
    .skip { position: absolute; left: 12px; top: -80px; z-index: 50; }
    .skip:focus { top: 12px; }
    .site__header { border-bottom: 1px solid var(--ds-line); background: var(--ds-surface); }
    .site__bar { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 24px; max-width: 1120px; margin: 0 auto; padding: 12px 24px; }
    .site__brand { display: flex; align-items: center; gap: 10px; color: var(--ds-text); text-decoration: none; }
    .site__name { font-size: 20px; font-weight: 700; }
    .site__nav { display: flex; gap: 4px; margin-right: auto; }
    .site__nav a { display: inline-flex; align-items: center; min-height: 44px; padding: 0 12px; border-radius: var(--ds-radius-control); color: var(--ds-text); font-weight: 500; text-decoration: none; }
    .site__nav a:hover { background: var(--ds-soft); }
    main { flex: 1; outline: none; }

    .hero { position: relative; overflow: hidden; background: var(--ds-visual-bg); color: var(--ds-visual-text); }
    .hero__inner { position: relative; z-index: 1; display: flex; flex-direction: column; gap: 20px; max-width: 1120px; margin: 0 auto; padding: 88px 24px 96px; box-sizing: border-box; }
    .hero h1 { max-width: 640px; font-size: clamp(36px, 4.6vw, 54px); font-weight: 700; line-height: 1.08; letter-spacing: -0.025em; text-wrap: balance; }
    .hero p { max-width: 560px; color: var(--ds-visual-muted); font-size: 19px; line-height: 1.6; }
    .hero__actions { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 8px; }
    .hero .ds-btn--primary { background: var(--ds-accent); color: var(--ds-on-accent); }
    .hero .ds-btn--secondary { background: transparent; border-color: var(--ds-visual-muted); color: var(--ds-visual-text); }
    .hero .ds-btn--secondary:hover { background: color-mix(in srgb, var(--ds-visual-text) 10%, transparent); }
    .hero__mark { position: absolute; right: max(24px, calc(50% - 560px)); bottom: -160px; }
    .hero__stem { fill: var(--ds-visual-text); opacity: 0.07; }
    .hero__arc { stroke: var(--ds-accent); opacity: 0.45; }
    .hero__dot { fill: var(--ds-accent); }

    .section { display: flex; flex-direction: column; gap: 20px; max-width: 1120px; margin: 0 auto; padding: 72px 24px 0; box-sizing: border-box; }
    .section h2 { font-size: clamp(26px, 2.6vw, 32px); font-weight: 700; letter-spacing: -0.015em; }
    .section__lead { max-width: 640px; color: var(--ds-muted); font-size: 17px; line-height: 1.6; }

    .products { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }
    .product { display: flex; flex-direction: column; gap: 16px; padding: 28px; border: 1px solid var(--ds-line); border-radius: var(--ds-radius); background: var(--ds-surface); box-shadow: var(--ds-shadow); }
    .product__head { display: flex; align-items: center; gap: 14px; }
    .product h3 { font-size: 24px; font-weight: 700; font-family: var(--ds-font); }
    .product__kind { color: var(--ds-muted); font-size: 15px; }
    .product > p { color: var(--ds-muted); line-height: 1.6; }
    .product ul { display: flex; flex-direction: column; gap: 6px; padding-left: 20px; color: var(--ds-text); }
    .product__actions { display: flex; flex-wrap: wrap; align-items: center; gap: 16px; margin-top: auto; }

    .staff { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px; }
    .staff__item { display: flex; flex-direction: column; gap: 4px; padding: 18px; border: 1.5px solid var(--ds-line); border-radius: var(--ds-radius); background: var(--ds-surface); color: var(--ds-text); text-decoration: none; }
    .staff__item:hover { border-color: var(--ds-primary); }
    .staff__title { font-size: 17px; font-weight: 600; }
    .staff__text { color: var(--ds-muted); font-size: 14px; }

    .site__footer { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px 24px; max-width: 1120px; width: 100%; box-sizing: border-box; margin: 72px auto 0; padding: 24px; border-top: 1px solid var(--ds-line); color: var(--ds-muted); font-size: 14px; }
    .site__links { display: flex; flex-wrap: wrap; gap: 0 18px; }
    .site__links a { display: inline-flex; align-items: center; min-height: 44px; color: var(--ds-text); text-decoration: none; }
    .site__links a:hover { color: var(--ds-link); text-decoration: underline; text-underline-offset: 3px; }

    @media (max-width: 860px) {
      .products, .staff { grid-template-columns: 1fr; }
      .hero__inner { padding: 56px 20px 64px; }
      .hero__mark { display: none; }
      .section { padding: 56px 20px 0; }
    }
  `]
})
export class EduivmeHomeComponent implements OnInit {
  private readonly seo = inject(SiteSeoService);
  readonly urls = EDUIVME_SITE_URLS;
  readonly year = new Date().getFullYear();

  ngOnInit(): void {
    this.seo.apply({
      title: 'eduivme | Öğrenci koçluğu ve hızlı okuma',
      description: 'eduivme; maxkoç öğrenci koçluğu ve Master Hızlı Okuma ürünlerini geliştirir. Öğretmen ve kurumlar için personel paneli.',
      path: '/',
      image: '/brand/og-eduivme.png',
      siteName: 'eduivme',
      jsonLd: { '@context': 'https://schema.org', '@type': 'Organization', name: 'eduivme', url: 'https://eduivme.com', logo: 'https://eduivme.com/brand/eduivme-512.png' }
    });
  }
}
