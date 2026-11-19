import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { CoachingCmsEntry, CoachingCmsNavigationItem, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { SiteAnalyticsService } from '../../../core/services/site-analytics.service';

@Component({
  selector: 'app-coaching-public-cms-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterOutlet],
  template: `
    <div class="site" data-brand="maxkoc">
      <a class="skip ds-btn ds-btn--primary" href="#main-content">İçeriğe geç</a>
      <header class="site__header">
        <div class="site__bar">
          <a routerLink="/coaching" class="site__brand" aria-label="maxkoç ana sayfası">
            <img src="brand/maxkoc.svg" alt="" width="40" height="40">
            <span><span class="site__name">{{ branding()?.title || 'maxkoç' }}</span><span class="site__note">{{ branding()?.eyebrow || 'Öğrenci koçluğu' }}</span></span>
          </a>
          <nav class="site__nav" aria-label="Koçluk içerik menüsü">
            @for (item of navigation(); track item.id) {
              <a [href]="item.url" [target]="item.openInNewTab ? '_blank' : null" [rel]="item.openInNewTab ? 'noopener noreferrer' : null">{{ item.label }}</a>
            }
          </nav>
          <div class="site__actions">
            <a href="/auth/login?returnUrl=%2Fcoaching-portal" class="ds-btn ds-btn--quiet">Giriş yap</a>
            <a [href]="branding()?.linkUrl || '/auth/register/student'" class="ds-btn ds-btn--primary">{{ branding()?.linkLabel || 'Ücretsiz başla' }}</a>
          </div>
        </div>
      </header>
      <main id="main-content" class="site__main" tabindex="-1"><router-outlet></router-outlet></main>
      <footer class="site__footer">
        <div class="site__footer-inner">
          <div class="site__about">
            <p class="site__name">{{ branding()?.title || 'maxkoç' }}</p>
            <p>{{ branding()?.summary || 'Hedef belirleme, çalışma planı ve ilerleme değerlendirmesi için öğrenci, öğretmen ve kurumlara özel koçluk alanı.' }}</p>
          </div>
          <nav class="site__links" aria-label="Alt menü">
            @for (item of footerNavigation(); track item.id) {
              <a [href]="item.url" [target]="item.openInNewTab ? '_blank' : null" [rel]="item.openInNewTab ? 'noopener noreferrer' : null">{{ item.label }}</a>
            }
            <a routerLink="/coaching/newsletter">Bültene abone ol</a>
            <a routerLink="/legal/privacy">Gizlilik politikası</a>
            <a routerLink="/legal/kvkk">KVKK aydınlatma metni</a>
            @if (analytics.measurementId()) { <button type="button" data-testid="cookie-preferences" (click)="analytics.reopen()">Çerez tercihleri</button> }
          </nav>
          <p class="site__copyright">maxkoç bir eduivme markasıdır. © {{ currentYear }} ONAL Yazılım ve Otomasyon. Tüm hakları saklıdır.</p>
        </div>
      </footer>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .site { min-height: 100vh; display: flex; flex-direction: column; background: var(--ds-bg); }
    .skip { position: absolute; left: 12px; top: -80px; z-index: 50; }
    .skip:focus { top: 12px; }
    .site__header { position: sticky; top: 0; z-index: 30; border-bottom: 1px solid var(--ds-line); background: color-mix(in srgb, var(--ds-surface) 94%, transparent); backdrop-filter: blur(8px); }
    .site__bar { display: flex; flex-wrap: wrap; align-items: center; gap: 12px 24px; max-width: 1200px; margin: 0 auto; padding: 12px 24px; }
    .site__brand { display: flex; align-items: center; gap: 10px; color: var(--ds-text); text-decoration: none; }
    .site__name { display: block; font-size: 18px; font-weight: 700; line-height: 1.1; }
    .site__note { display: block; color: var(--ds-muted); font-size: 12px; }
    .site__nav { display: flex; flex-wrap: wrap; gap: 4px; margin-right: auto; }
    .site__nav a { display: inline-flex; align-items: center; min-height: 44px; padding: 0 12px; border-radius: var(--ds-radius-control); color: var(--ds-text); font-size: 15px; font-weight: 500; text-decoration: none; }
    .site__nav a:hover { background: var(--ds-soft); }
    .site__actions { display: flex; align-items: center; gap: 8px; }
    .site__actions .ds-btn { min-height: 44px; }
    .site__main { flex: 1; width: 100%; max-width: 1200px; box-sizing: border-box; margin: 0 auto; padding: 32px 24px 72px; outline: none; }
    .site__footer { border-top: 1px solid var(--ds-line); background: var(--ds-surface); }
    .site__footer-inner { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 24px; max-width: 1200px; margin: 0 auto; padding: 32px 24px; }
    .site__about { display: flex; flex-direction: column; gap: 8px; max-width: 520px; }
    .site__about p:not(.site__name) { margin: 0; color: var(--ds-muted); font-size: 14px; line-height: 1.6; }
    .site__about .site__name { margin: 0; }
    .site__links { display: flex; flex-wrap: wrap; align-content: flex-start; gap: 0 18px; }
    .site__links a, .site__links button { display: inline-flex; align-items: center; min-height: 44px; padding: 0; border: 0; background: none; color: var(--ds-text); font: inherit; font-size: 14px; text-decoration: none; cursor: pointer; }
    .site__links a:hover, .site__links button:hover { color: var(--ds-link); text-decoration: underline; text-underline-offset: 3px; }
    .site__copyright { grid-column: 1 / -1; margin: 0; padding-top: 16px; border-top: 1px solid var(--ds-line); color: var(--ds-muted); font-size: 13px; }
    @media (max-width: 760px) {
      .site__bar { padding: 10px 16px; }
      .site__nav { order: 3; width: 100%; overflow-x: auto; flex-wrap: nowrap; }
      .site__actions { margin-left: auto; }
      .site__main { padding: 24px 16px 56px; }
      .site__footer-inner { grid-template-columns: 1fr; padding: 24px 16px; }
    }
  `]
})
export class CoachingPublicCmsLayoutComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  readonly analytics = inject(SiteAnalyticsService);
  readonly navigation = signal<CoachingCmsNavigationItem[]>([]);
  readonly footerNavigation = signal<CoachingCmsNavigationItem[]>([]);
  readonly branding = signal<CoachingCmsEntry | null>(null);
  readonly currentYear = new Date().getFullYear();

  ngOnInit(): void {
    this.service.getPublicCmsNavigation('Main').subscribe({ next: items => this.navigation.set(items), error: () => this.navigation.set([]) });
    this.service.getPublicCmsNavigation('Footer').subscribe({ next: items => this.footerNavigation.set(items), error: () => this.footerNavigation.set([]) });
    this.service.getPublicCmsBlocks('HomeBranding').subscribe({ next: blocks => this.branding.set(blocks[0] ?? null), error: () => this.branding.set(null) });
  }
}
