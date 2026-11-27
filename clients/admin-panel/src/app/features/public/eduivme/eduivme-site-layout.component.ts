import { ChangeDetectionStrategy, Component, ElementRef, HostListener, Input, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SitePublicEntry } from '../../../core/services/eduivme-site.service';
import { EDUIVME_SITE_URLS } from '../../../../../shared/eduivme-sites';
import { EduivmeHrefDirective } from './eduivme-link.directive';
import { ThemeToggleComponent } from '../../../shared/public-site/theme-toggle.component';

/** The frame of eduivme.com: header with the sign-in menu, the page, and the footer. */
@Component({
  selector: 'app-eduivme-site-layout',
  standalone: true,
  imports: [RouterLink, EduivmeHrefDirective, ThemeToggleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="site" data-brand="eduivme">
      <a class="skip ds-btn ds-btn--primary" href="#main-content">İçeriğe geç</a>
      <header class="bar">
        <div class="bar__inner">
          <a routerLink="/" class="brand" aria-label="eduivme ana sayfası">
            <img src="/brand/eduivme.svg" alt="" width="36" height="36"><span>eduivme</span>
          </a>
          <nav class="nav" aria-label="Ana menü">
            <a [eduivmeHref]="home ? '#urunler' : '/#urunler'">Ürünler</a>
            <a [eduivmeHref]="home ? '#kimler' : '/#kimler'">Kimler için</a>
            <a [eduivmeHref]="home ? '#sss' : '/#sss'">Sık sorulanlar</a>
          </nav>
          <app-theme-toggle class="theme" />
          <div class="signin">
            <button type="button" class="ds-btn ds-btn--secondary signin__button" [attr.aria-expanded]="menuOpen()" aria-controls="signin-menu" (click)="toggleMenu()">
              Giriş yap
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M6 9l6 6 6-6"/></svg>
            </button>
            @if (menuOpen()) {
              <ul id="signin-menu" class="signin__menu">
                @for (item of signIns; track item.href) {
                  <li><a [href]="item.href"><img [src]="item.mark" alt="" width="24" height="24"><span><strong>{{ item.title }}</strong><small>{{ item.text }}</small></span></a></li>
                }
              </ul>
            }
          </div>
        </div>
      </header>

      <main id="main-content" tabindex="-1"><ng-content /></main>

      <footer class="foot">
        <div class="foot__inner">
          <div class="foot__brand">
            <a routerLink="/" class="brand brand--light" aria-label="eduivme ana sayfası"><img src="/brand/eduivme.svg" alt="" width="32" height="32"><span>eduivme</span></a>
            <p>{{ site?.body || 'Öğrenciler, veliler, öğretmenler ve kurumlar için eğitim ürünleri.' }}</p>
          </div>
          @if (products.length) {
            <nav aria-labelledby="foot-products"><h2 id="foot-products">Ürünler</h2>
              <ul>@for (product of products; track product.id) { <li><a [eduivmeHref]="product.linkUrl">{{ product.title }}</a></li> }</ul>
            </nav>
          }
          <nav aria-labelledby="foot-signin"><h2 id="foot-signin">Giriş</h2>
            <ul>@for (item of signIns; track item.href) { <li><a [href]="item.href">{{ item.short }}</a></li> }</ul>
          </nav>
          <nav aria-labelledby="foot-company"><h2 id="foot-company">Kurumsal</h2>
            <ul>
              <li><a routerLink="/legal/privacy">Gizlilik politikası</a></li>
              <li><a routerLink="/legal/kvkk">KVKK aydınlatma metni</a></li>
              <li><a [href]="urls.admin + '/auth/support'">Yardım ve destek</a></li>
            </ul>
          </nav>
        </div>
        <p class="foot__copy">© {{ year }} ONAL Yazılım ve Otomasyon. Tüm hakları saklıdır.</p>
      </footer>
    </div>
  `,
  styles: [`
    :host { display: block; }
    h2, p, ul { margin: 0; }
    ul { list-style: none; padding: 0; }
    .site { min-height: 100vh; display: flex; flex-direction: column; background: var(--ds-bg); color: var(--ds-text); font-family: var(--ds-font); }
    .skip { position: absolute; left: 12px; top: -80px; z-index: 60; }
    .skip:focus { top: 12px; }
    main { flex: 1; outline: none; }

    .bar { position: sticky; top: 0; z-index: 40; background: color-mix(in srgb, var(--ds-surface) 92%, transparent); backdrop-filter: blur(10px); border-bottom: 1px solid var(--ds-line); }
    .bar__inner { display: flex; align-items: center; gap: 12px 28px; max-width: 1200px; margin: 0 auto; padding: 12px 24px; }
    .brand { display: inline-flex; align-items: center; gap: 10px; min-height: 44px; color: var(--ds-text); font-size: 21px; font-weight: 700; letter-spacing: -0.01em; text-decoration: none; }
    .brand--light { color: var(--ds-visual-text); }
    .nav { display: flex; gap: 4px; margin-left: auto; }
    .nav a { display: inline-flex; align-items: center; min-height: 44px; padding: 0 12px; border-radius: var(--ds-radius-control); color: var(--ds-muted); font-weight: 500; text-decoration: none; }
    .nav a { transition: background-color 200ms ease, color 200ms ease; }
    .nav a:hover { background: var(--ds-soft); color: var(--ds-text); }
    .signin { position: relative; }
    .signin__button { gap: 6px; }
    .signin__menu { position: absolute; right: 0; top: calc(100% + 8px); z-index: 50; width: min(340px, calc(100vw - 32px)); padding: 8px; border: 1px solid var(--ds-line); border-radius: var(--ds-radius); background: var(--ds-surface); box-shadow: 0 18px 40px -18px rgb(0 0 0 / 0.35); }
    .signin__menu a { display: flex; align-items: center; gap: 12px; min-height: 52px; padding: 8px 10px; border-radius: var(--ds-radius-control); color: var(--ds-text); text-decoration: none; }
    .signin__menu a:hover { background: var(--ds-soft); }
    .signin__menu span { display: flex; flex-direction: column; }
    .signin__menu strong { font-size: 15px; font-weight: 600; }
    .signin__menu small { color: var(--ds-muted); font-size: 13px; }

    .foot { margin-top: 96px; background: var(--ds-visual-bg); color: var(--ds-visual-text); }
    .foot__inner { display: grid; grid-template-columns: minmax(0, 1.5fr) repeat(3, minmax(0, 1fr)); gap: 32px; max-width: 1200px; margin: 0 auto; padding: 56px 24px 40px; }
    .foot__brand p { margin-top: 14px; max-width: 26em; color: var(--ds-visual-muted); line-height: 1.6; }
    .foot h2 { margin-bottom: 12px; font-size: 15px; font-weight: 600; }
    .foot ul { display: flex; flex-direction: column; gap: 2px; }
    .foot nav a { display: inline-flex; align-items: center; min-height: 36px; color: var(--ds-visual-muted); text-decoration: none; }
    .foot nav a:hover { color: var(--ds-visual-text); text-decoration: underline; text-underline-offset: 3px; }
    .foot__copy { max-width: 1200px; margin: 0 auto; padding: 20px 24px 32px; border-top: 1px solid color-mix(in srgb, var(--ds-visual-text) 14%, transparent); color: var(--ds-visual-muted); font-size: 14px; }
    .foot a:focus-visible { outline: 2px solid var(--ds-accent); outline-offset: 2px; }

    @media (max-width: 860px) {
      .nav { display: none; }
      .theme { margin-left: auto; }
      .signin { margin-left: auto; }
      .foot__inner { grid-template-columns: 1fr 1fr; }
      .foot__brand { grid-column: 1 / -1; }
    }
  `]
})
export class EduivmeSiteLayoutComponent {
  private readonly host = inject(ElementRef<HTMLElement>);
  @Input() site: SitePublicEntry | null = null;
  @Input() products: SitePublicEntry[] = [];
  /** On the home page the menu scrolls; elsewhere it goes back to the home page's sections. */
  @Input() home = false;

  readonly urls = EDUIVME_SITE_URLS;
  readonly year = new Date().getFullYear();
  readonly menuOpen = signal(false);
  readonly signIns = [
    { title: 'Öğrenci', short: 'Öğrenci girişi', text: 'maxkoç ile çalışma planın', href: `${EDUIVME_SITE_URLS.student}/auth/login`, mark: '/brand/maxkoc.svg' },
    { title: 'Veli', short: 'Veli girişi', text: 'Çocuğunun çalışmasını izle', href: `${EDUIVME_SITE_URLS.parent}/auth/login`, mark: '/brand/maxkoc.svg' },
    { title: 'Master Hızlı Okuma', short: 'Hızlı Okuma girişi', text: 'Okuma egzersizlerin', href: 'https://masterhizliokuma.com/auth/login', mark: '/brand/master-hizli-okuma.svg' },
    { title: 'Öğretmen', short: 'Öğretmen girişi', text: 'Öğrencileriniz ve dersleriniz', href: `${EDUIVME_SITE_URLS.teacher}/auth/login`, mark: '/brand/eduivme.svg' },
    { title: 'Kurum', short: 'Kurum girişi', text: 'Kurumunuzun paneli', href: `${EDUIVME_SITE_URLS.institution}/auth/login`, mark: '/brand/eduivme.svg' },
    { title: 'Yönetici', short: 'Yönetici girişi', text: 'eduivme yönetim paneli', href: `${EDUIVME_SITE_URLS.admin}/auth/login`, mark: '/brand/eduivme.svg' }
  ];

  toggleMenu(): void { this.menuOpen.update(open => !open); }

  @HostListener('document:keydown.escape')
  closeOnEscape(): void {
    if (!this.menuOpen()) return;
    this.menuOpen.set(false);
    (this.host.nativeElement.querySelector('.signin__button') as HTMLElement | null)?.focus();
  }

  @HostListener('document:click', ['$event'])
  closeOutside(event: MouseEvent): void {
    if (this.menuOpen() && !(event.target instanceof Node && this.host.nativeElement.querySelector('.signin')?.contains(event.target))) this.menuOpen.set(false);
  }
}
