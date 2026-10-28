import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SiteAnalyticsService } from '../../core/services/site-analytics.service';

/** Roadmap O11: asks before analytics cookies (GA4) are set; shown only when GA4 is configured. */
@Component({
  selector: 'app-cookie-consent', standalone: true, imports: [RouterLink],
  template: `
    @if (analytics.needsChoice()) {
      <section class="consent" role="dialog" aria-live="polite" aria-labelledby="consent-title" data-testid="cookie-consent">
        <div>
          <h2 id="consent-title">Çerez tercihiniz</h2>
          <p>Siteyi geliştirmek için ziyaretleri Google Analytics ile ölçmek istiyoruz. Bu analiz çerezleri yalnız onay verirseniz kullanılır;
            zorunlu çerezler her durumda çalışır. Ayrıntılar <a routerLink="/legal/cookies">çerez politikasında</a>.</p>
        </div>
        <div class="actions">
          <button type="button" class="secondary" data-testid="consent-deny" (click)="analytics.setConsent('denied')">Reddet</button>
          <button type="button" class="primary" data-testid="consent-accept" (click)="analytics.setConsent('granted')">Kabul et</button>
        </div>
      </section>
    }`,
  styles: [`
    .consent { position: fixed; z-index: 1000; left: 1rem; right: 1rem; bottom: 1rem; max-width: 46rem; margin: 0 auto; display: flex; gap: 1rem;
      align-items: flex-end; justify-content: space-between; flex-wrap: wrap; padding: 1rem 1.1rem; border: 1px solid #c7d2fe; border-radius: 1rem;
      background: #fff; color: #1e293b; box-shadow: 0 18px 40px rgba(15, 23, 42, .18); font-size: .9rem; line-height: 1.5; }
    :host-context(.dark) .consent { background: #0f172a; color: #e2e8f0; border-color: #312e81; }
    h2 { margin: 0 0 .3rem; font-size: 1rem; font-weight: 700; }
    p { margin: 0; max-width: 34rem; } a { color: #4338ca; text-decoration: underline; }
    :host-context(.dark) a { color: #a5b4fc; }
    .actions { display: flex; gap: .5rem; }
    button { min-height: 2.5rem; border-radius: .6rem; padding: .4rem 1rem; font: inherit; font-weight: 600; cursor: pointer; }
    .primary { border: 1px solid #4338ca; background: #4338ca; color: #fff; }
    .secondary { border: 1px solid #94a3b8; background: transparent; color: inherit; }
  `]
})
export class CookieConsentComponent {
  readonly analytics = inject(SiteAnalyticsService);
}
