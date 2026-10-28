import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

/** Roadmap O11: the campaign tags of the page a visitor landed on, sent with a registration. */
export interface RegistrationUtm { utmSource: string | null; utmMedium: string | null; utmCampaign: string | null }
export type AnalyticsConsent = 'granted' | 'denied';

const CONSENT_KEY = 'eduivme.cookie-consent.v1';
const UTM_KEY = 'eduivme.utm.v1';
const GA_ID_PATTERN = /^G-[A-Z0-9]{4,15}$/;
/** Analytics never sees signed-in areas or token links: only the public site and the sign-up and login pages. */
const TRACKED_PATH = /^\/(?:$|coaching(?:\/|$)|legal\/|auth\/(?:login|register)(?:\/|$))/;

declare global { interface Window { dataLayer?: unknown[]; gtag?: (...args: unknown[]) => void } }

/**
 * Roadmap O11: Google Analytics 4 with cookie consent, and first-party campaign (UTM) capture.
 * - GA4 loads only after the visitor accepts analytics cookies, and only when the server published a measurement id
 *   (`<meta name="eduivme-ga4-id">`, from the GA4_MEASUREMENT_ID environment variable); without an id nothing loads and no
 *   banner is shown.
 * - UTM tags of the landing URL are kept in memory and go with the registration request, so sign-ups are attributed
 *   even when analytics is refused. They are kept for the tab (sessionStorage) only after consent.
 */
@Injectable({ providedIn: 'root' })
export class SiteAnalyticsService {
  private readonly document = inject(DOCUMENT);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly router = inject(Router, { optional: true });
  private loaded = false;

  readonly measurementId = signal<string | null>(null);
  readonly consent = signal<AnalyticsConsent | null>(null);
  /** True while the visitor reopened the banner to change the choice. */
  readonly editing = signal(false);
  readonly utm = signal<RegistrationUtm | null>(null);

  constructor() {
    if (!this.browser) return;
    const id = this.document.querySelector('meta[name="eduivme-ga4-id"]')?.getAttribute('content')?.trim() ?? '';
    this.measurementId.set(GA_ID_PATTERN.test(id) ? id : null);
    this.consent.set(this.readConsent());
    this.captureUtm(this.document.location?.href ?? '');
    if (this.consent() === 'granted') this.load();
    this.router?.events?.pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(event => this.pageView(event.urlAfterRedirects));
  }

  /** Whether the consent banner should be visible. */
  needsChoice(): boolean { return !!this.measurementId() && (this.consent() === null || this.editing()); }

  setConsent(choice: AnalyticsConsent): void {
    this.consent.set(choice); this.editing.set(false);
    try { localStorage.setItem(CONSENT_KEY, JSON.stringify({ analytics: choice === 'granted', at: new Date().toISOString() })); } catch { /* storage blocked */ }
    if (choice === 'granted') {
      this.persistUtm();
      this.load();
      this.pageView(this.router?.url ?? this.document.location?.pathname ?? '/');
    } else {
      try { sessionStorage.removeItem(UTM_KEY); } catch { /* storage blocked */ }
      if (this.loaded) this.window()?.gtag?.('consent', 'update', { analytics_storage: 'denied' });
      this.clearGaCookies();
    }
  }

  reopen(): void { this.editing.set(true); }

  /** The tags to send with a registration, or null. */
  registrationUtm(): RegistrationUtm | null { return this.utm(); }

  /** Sends a GA4 event when analytics is on; ignored otherwise. */
  track(event: string, params: Record<string, string | number | boolean> = {}): void {
    if (!this.loaded || this.consent() !== 'granted') return;
    this.window()?.gtag?.('event', event, params);
  }

  /** Reads utm_source, utm_medium and utm_campaign of a URL; keeps the first ones seen in this tab. */
  captureUtm(href: string): void {
    let stored: RegistrationUtm | null = this.utm();
    if (!stored) { try { stored = JSON.parse(sessionStorage.getItem(UTM_KEY) ?? 'null'); } catch { stored = null; } }
    let url: URL;
    try { url = new URL(href, 'https://eduivme.com'); } catch { this.utm.set(stored); return; }
    const value = (name: string) => { const text = url.searchParams.get(name)?.trim().slice(0, 100); return text ? text : null; };
    const fresh: RegistrationUtm = { utmSource: value('utm_source'), utmMedium: value('utm_medium'), utmCampaign: value('utm_campaign') };
    this.utm.set(fresh.utmSource || fresh.utmMedium || fresh.utmCampaign ? fresh : stored);
    if (this.consent() === 'granted') this.persistUtm();
  }

  private pageView(url: string): void {
    const path = url.split(/[?#]/)[0] || '/';
    if (!TRACKED_PATH.test(path)) return;
    const location = this.document.location;
    this.track('page_view', { page_path: path, page_location: `${location?.origin ?? ''}${path}${this.utmQuery()}`, page_title: this.document.title });
  }

  /** GA4 reads the campaign from page_location; only utm tags are kept from the query, never tokens. */
  private utmQuery(): string {
    const utm = this.utm();
    if (!utm) return '';
    const params = new URLSearchParams();
    if (utm.utmSource) params.set('utm_source', utm.utmSource);
    if (utm.utmMedium) params.set('utm_medium', utm.utmMedium);
    if (utm.utmCampaign) params.set('utm_campaign', utm.utmCampaign);
    const text = params.toString();
    return text ? `?${text}` : '';
  }

  private load(): void {
    const id = this.measurementId();
    const win = this.window();
    if (this.loaded || !id || !win) return;
    this.loaded = true;
    win.dataLayer = win.dataLayer ?? [];
    win.gtag = function gtag() { (win.dataLayer as unknown[]).push(arguments); };
    win.gtag('consent', 'default', { analytics_storage: 'granted', ad_storage: 'denied', ad_user_data: 'denied', ad_personalization: 'denied' });
    win.gtag('js', new Date());
    win.gtag('config', id, { send_page_view: false, anonymize_ip: true, allow_google_signals: false, allow_ad_personalization_signals: false });
    const script = this.document.createElement('script');
    script.async = true;
    script.src = `https://www.googletagmanager.com/gtag/js?id=${encodeURIComponent(id)}`;
    this.document.head.appendChild(script);
  }

  private persistUtm(): void {
    const utm = this.utm();
    try { if (utm) sessionStorage.setItem(UTM_KEY, JSON.stringify(utm)); } catch { /* storage blocked */ }
  }

  private readConsent(): AnalyticsConsent | null {
    try {
      const saved = JSON.parse(localStorage.getItem(CONSENT_KEY) ?? 'null') as { analytics?: unknown } | null;
      return saved && typeof saved.analytics === 'boolean' ? (saved.analytics ? 'granted' : 'denied') : null;
    } catch { return null; }
  }

  /** Removes the _ga cookies GA set before the visitor withdrew consent. */
  private clearGaCookies(): void {
    const host = this.document.location?.hostname ?? '';
    const domains = ['', host, `.${host}`, `.${host.split('.').slice(-2).join('.')}`];
    for (const cookie of (this.document.cookie ?? '').split(';')) {
      const name = cookie.split('=')[0].trim();
      if (!name.startsWith('_ga')) continue;
      for (const domain of domains) this.document.cookie = `${name}=; Max-Age=0; path=/${domain ? `; domain=${domain}` : ''}`;
    }
  }

  private window(): Window | null { return this.document.defaultView; }
}
