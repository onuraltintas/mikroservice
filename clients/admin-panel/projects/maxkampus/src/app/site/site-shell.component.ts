import { Component, OnInit, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { MaxkampusSession } from '../core/session.service';
import { SiteStore } from '../core/site-store';
import { BrandMarkComponent } from './brand-mark.component';
import { NewsletterFormComponent } from './newsletter-form.component';
import { SiteLinkComponent } from './site-link.component';
import { ThemeMenuComponent } from '../core/theme-menu.component';

/** maxkampus.com's public frame: header with the editors' menu, the page, footer with legal links and the newsletter. */
@Component({
  selector: 'mk-site-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, BrandMarkComponent, SiteLinkComponent, NewsletterFormComponent, ThemeMenuComponent],
  template: `
    <a class="skip" href="#icerik">İçeriğe geç</a>
    <header class="top">
      <div class="mk-wrap top__bar">
        <a routerLink="/" class="logo" aria-label="maxkampüs ana sayfa">
          <mk-brand-mark [size]="36" />
          <span class="logo__text"><strong>maxkampüs</strong></span>
        </a>
        <mk-theme-menu class="theme-slot" />
        <button type="button" class="menu-toggle" [attr.aria-expanded]="open()" aria-controls="site-menu" (click)="open.set(!open())">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
            @if (open()) { <path d="M6 6l12 12M18 6L6 18"/> } @else { <path d="M4 7h16M4 12h16M4 17h16"/> }</svg>
          <span class="sr-only">Menü</span>
        </button>
        <nav id="site-menu" class="nav" [class.nav--open]="open()" aria-label="Site menüsü">
          <ul>
            @for (item of store.headerMenu(); track item.id) {
              <li><mk-site-link [href]="item.linkUrl!" cls="nav__link">{{ item.title }}</mk-site-link></li>
            }
          </ul>
          <div class="nav__actions">
            @if (session.signedIn()) {
              <a routerLink="/panel" class="ds-btn ds-btn--primary">Panele git</a>
            } @else {
              <a routerLink="/giris" class="ds-btn ds-btn--quiet">Giriş yap</a>
              <a routerLink="/kayit" class="ds-btn ds-btn--primary">Kurumunuzu açın</a>
            }
          </div>
        </nav>
      </div>
    </header>
    <main id="icerik" tabindex="-1"><router-outlet /></main>
    <footer class="foot">
      <div class="mk-wrap foot__grid">
        <div class="foot__brand">
          <a routerLink="/" class="logo logo--light" aria-label="maxkampüs ana sayfa">
            <mk-brand-mark [size]="32" />
            <span class="logo__text"><strong>maxkampüs</strong></span>
          </a>
          <p>{{ store.home()?.site?.body || 'Kurslar, etüt merkezleri ve özel ders verenler için ders programı, yoklama, ödev ve tahsilat.' }}</p>
        </div>
        <nav aria-label="Alt bilgi">
          <h2 class="foot__title">maxkampüs</h2>
          <ul>
            @for (item of store.footerMenu(); track item.id) { <li><mk-site-link [href]="item.linkUrl!">{{ item.title }}</mk-site-link></li> }
          </ul>
        </nav>
        <nav aria-label="Yasal metinler">
          <h2 class="foot__title">Yasal</h2>
          <ul>
            <li><a routerLink="/yasal/kvkk">KVKK aydınlatma metni</a></li>
            <li><a routerLink="/yasal/privacy">Gizlilik politikası</a></li>
            <li><a routerLink="/yasal/course-management-terms">Kullanım ve abonelik koşulları</a></li>
            <li><a routerLink="/yasal/cookies">Çerez politikası</a></li>
          </ul>
        </nav>
        <section aria-labelledby="footer-newsletter">
          <h2 class="foot__title" id="footer-newsletter">Bülten</h2>
          <p>Yeni yazılar ve özellikler ayda bir e-postayla.</p>
          <mk-newsletter-form source="footer" headingId="footer-newsletter" inputId="footer-newsletter-email" />
        </section>
      </div>
      <div class="mk-wrap"><p class="foot__copy">© {{ year }} maxkampüs, bir eduivme markası.</p></div>
    </footer>
  `,
  styles: `
    :host { display: flex; flex-direction: column; min-height: 100dvh; }
    main { flex: 1; outline: none; }
    .skip { position: absolute; left: -999px; top: 8px; z-index: 50; padding: 10px 14px; background: var(--ds-surface); color: var(--ds-text); border-radius: 8px; }
    .skip:focus { left: 8px; }
    .top { position: sticky; top: 0; z-index: 20; background: var(--ds-surface); border-bottom: 1px solid var(--ds-line); }
    .top__bar { display: flex; align-items: center; gap: 20px; min-height: 72px; }
    .logo { display: inline-flex; align-items: center; gap: 10px; color: var(--ds-text); text-decoration: none; }
    .logo__text { display: flex; flex-direction: column; line-height: 1.15; }
    .logo__text strong { font-size: 19px; }
    .logo__text span { font-size: 12px; color: var(--ds-muted); }
    .logo--light, .logo--light .logo__text span { color: #FFFFFF; }
    .logo--light .logo__text span { color: var(--mk-hero-muted); }
    .nav { display: flex; align-items: center; gap: 16px; margin-left: auto; }
    .nav ul { display: flex; gap: 4px; margin: 0; padding: 0; list-style: none; }
    :host ::ng-deep .nav__link { display: inline-flex; align-items: center; min-height: 44px; padding: 0 12px; border-radius: 8px; color: var(--ds-text); text-decoration: none; font-weight: 500; }
    :host ::ng-deep .nav__link:hover { background: var(--ds-soft); }
    .nav__actions { display: flex; gap: 8px; }
    .theme-slot { order: 3; } .menu-toggle { display: none; margin-left: auto; width: 44px; height: 44px; place-items: center; border: 1px solid var(--ds-line); border-radius: 10px; background: var(--ds-surface); color: var(--ds-text); cursor: pointer; }
    @media (max-width: 900px) {
      .menu-toggle { display: grid; }
      .nav { display: none; position: absolute; top: 72px; left: 0; right: 0; flex-direction: column; align-items: stretch; padding: 12px 24px 20px; background: var(--ds-surface); border-bottom: 1px solid var(--ds-line); }
      .nav--open { display: flex; }
      .nav ul { flex-direction: column; }
      .nav__actions { flex-direction: column; }
    }
    .foot { margin-top: 48px; background: var(--ds-visual-bg); color: var(--mk-hero-muted); }
    .foot__grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 32px; padding-top: 48px; padding-bottom: 24px; }
    .foot p { margin: 12px 0; font-size: 15px; }
    .foot ul { margin: 0; padding: 0; list-style: none; }
    .foot li a, .foot ::ng-deep li a { display: inline-flex; min-height: 36px; align-items: center; color: #FFFFFF; text-decoration: none; }
    .foot li a:hover { text-decoration: underline; }
    .foot__title { margin: 0 0 8px; color: #FFFFFF; font-size: 15px; }
    .foot__copy { padding-bottom: 32px; font-size: 14px; border-top: 1px solid var(--mk-hero-line); padding-top: 16px; }
    .foot ::ng-deep .ds-check { color: var(--mk-hero-muted); }
    /* The ink "primary" button vanished on the ink footer (20.11.2026 live test); the footer uses the amber accent. */
    .foot ::ng-deep .ds-btn--primary { background: var(--ds-accent); border-color: var(--ds-accent); color: var(--ds-on-accent); }
    .foot ::ng-deep .ds-btn--primary:hover:not(:disabled) { background: color-mix(in srgb, var(--ds-accent) 85%, #FFFFFF); }
    .foot ::ng-deep .mk-success { color: var(--ds-success); }
    .foot ::ng-deep .ds-field__error { color: #FFB4AB; }
  `
})
export class SiteShellComponent implements OnInit {
  readonly store = inject(SiteStore);
  readonly session = inject(MaxkampusSession);
  private readonly router = inject(Router);
  readonly open = signal(false);
  readonly year = new Date().getFullYear();

  ngOnInit(): void {
    void this.store.load();
    this.router.events.pipe(filter(event => event instanceof NavigationEnd)).subscribe(() => this.open.set(false));
  }
}
