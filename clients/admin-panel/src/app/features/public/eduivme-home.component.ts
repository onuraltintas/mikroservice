import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { EduivmeSiteService, SiteHomeView, SitePublicEntry } from '../../core/services/eduivme-site.service';
import { SiteSeoService } from '../../core/services/site-seo.service';
import { EduivmeHrefDirective } from './eduivme/eduivme-link.directive';
import { AUDIENCE_SCOPES, BRAND_MARKS, DEFAULT_HOME, SITE_ICON_PATHS, sectionOf, siteLink } from './eduivme/eduivme-site-content';
import { EduivmeSiteLayoutComponent } from './eduivme/eduivme-site-layout.component';

/**
 * eduivme.com: the parent brand's showcase of every product (user decisions 19.11.2026). All text comes from
 * site-service and is edited in the admin panel under "eduivme sitesi"; DEFAULT_HOME is shown only when the service
 * cannot be reached. Theme: data-brand="eduivme" (set by the layout), each product card in its own brand's colours.
 */
@Component({
  selector: 'app-eduivme-home',
  standalone: true,
  imports: [EduivmeSiteLayoutComponent, EduivmeHrefDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-eduivme-site-layout [site]="view()?.site ?? null" [products]="view()?.products ?? []" [home]="true">
      @if (view(); as home) {
        @if (home.hero; as hero) {
          <section class="hero" data-brand="eduivme" aria-labelledby="hero-title">
            <div class="hero__inner">
              <div class="hero__text">
                <h1 id="hero-title">{{ hero.title }}</h1>
                @if (hero.body) { <p>{{ hero.body }}</p> }
                <div class="hero__actions">
                  @if (hero.linkLabel) { <a class="ds-btn ds-btn--large hero__primary" [eduivmeHref]="hero.linkUrl">{{ hero.linkLabel }}</a> }
                  @if (hero.secondaryLinkLabel) { <a class="ds-btn ds-btn--large hero__secondary" [eduivmeHref]="hero.secondaryLinkUrl">{{ hero.secondaryLinkLabel }}</a> }
                </div>
                @if (home.products.length) {
                  <ul class="hero__family" aria-label="Ürünler">
                    @for (product of home.products; track product.id) {
                      <li><img [src]="mark(product)" alt="" width="28" height="28"><span>{{ product.title }}</span></li>
                    }
                  </ul>
                }
              </div>
              @if (hero.imageUrl) {
                <img class="hero__image" [src]="hero.imageUrl" alt="" width="460" height="400">
              } @else {
                <div class="hero__art" aria-hidden="true">
                  <svg viewBox="0 0 360 420" width="360" height="420">
                    <rect class="hero__stem" x="128" y="182" width="104" height="226" rx="52"></rect>
                    <path class="hero__arc" d="M84 128c28-56 90-86 146-75" fill="none" stroke-width="14" stroke-linecap="round"></path>
                    <circle class="hero__dot" cx="262" cy="70" r="52"></circle>
                  </svg>
                  @for (product of home.products.slice(0, 3); track product.id; let i = $index) {
                    <img class="hero__chip hero__chip--{{ i }}" [src]="mark(product)" alt="" width="64" height="64">
                  }
                </div>
              }
            </div>
          </section>
        }

        @if (home.products.length) {
          <section id="urunler" class="band band--soft" aria-labelledby="products-title">
            <div class="wrap">
              <header class="head">
                <h2 id="products-title">{{ section(home, 'products')?.title || 'Ürünler' }}</h2>
                @if (section(home, 'products')?.body) { <p>{{ section(home, 'products')?.body }}</p> }
              </header>
              <ul class="products">
                @for (product of home.products; track product.id) {
                  <li>
                    <a class="product" [attr.data-brand]="scope(product)" [class.product--km]="product.brand === 'km'" [eduivmeHref]="product.linkUrl">
                      <span class="product__band" aria-hidden="true"></span>
                      <span class="product__head">
                        <img [src]="product.imageUrl || mark(product)" alt="" width="56" height="56">
                        <span><span class="product__name">{{ product.title }}</span>@if (product.subtitle) {<span class="product__kind">{{ product.subtitle }}</span>}</span>
                      </span>
                      @if (product.body) { <span class="product__text">{{ product.body }}</span> }
                      @if (product.highlights.length) {
                        <span class="product__list">
                          @for (item of product.highlights; track item) {
                            <span class="product__item"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12.5l4.5 4.5L19 7.5"/></svg>{{ item }}</span>
                          }
                        </span>
                      }
                      <span class="product__foot">
                        <span>{{ product.linkLabel || 'Ürünü incele' }}</span>
                        @if (external(product.linkUrl)) {
                          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M7 17L17 7M8 7h9v9"/></svg>
                          <span class="ds-visually-hidden">(ürünün kendi sitesi)</span>
                        } @else {
                          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg>
                        }
                      </span>
                    </a>
                  </li>
                }
              </ul>
            </div>
          </section>
        }

        @if (home.audiences.length) {
          <section id="kimler" class="band" aria-labelledby="audiences-title">
            <div class="wrap">
              <header class="head">
                <h2 id="audiences-title">{{ section(home, 'audiences')?.title || 'Kimler için' }}</h2>
                @if (section(home, 'audiences')?.body) { <p>{{ section(home, 'audiences')?.body }}</p> }
              </header>
              <ul class="audiences">
                @for (audience of home.audiences; track audience.id; let i = $index) {
                  <li [attr.data-brand]="audienceScope(i)" class="audience" [class.audience--accent]="i === 2">
                    <span class="audience__icon" aria-hidden="true"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path [attr.d]="icon(audience)"/></svg></span>
                    <h3>{{ audience.title }}</h3>
                    @if (audience.body) { <p>{{ audience.body }}</p> }
                    @if (audience.linkLabel && audience.linkUrl) {
                      <a class="audience__link" [eduivmeHref]="audience.linkUrl">{{ audience.linkLabel }}<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg></a>
                    }
                  </li>
                }
              </ul>
            </div>
          </section>
        }

        @if (home.faqs.length) {
          <section id="sss" class="band" aria-labelledby="faq-title">
            <div class="wrap wrap--narrow">
              <header class="head">
                <h2 id="faq-title">{{ section(home, 'faq')?.title || 'Sık sorulanlar' }}</h2>
                @if (section(home, 'faq')?.body) { <p>{{ section(home, 'faq')?.body }}</p> }
              </header>
              <div class="faq">
                @for (faq of home.faqs; track faq.id; let first = $first) {
                  <details [open]="first"><summary>{{ faq.title }}</summary><p>{{ faq.body }}</p></details>
                }
              </div>
            </div>
          </section>
        }
      } @else {
        <div class="loading" role="status"><span class="ds-spinner" aria-hidden="true"></span><span class="ds-visually-hidden">Sayfa yükleniyor</span></div>
      }
    </app-eduivme-site-layout>
  `,
  styleUrl: './eduivme/eduivme-site.scss'
})
export class EduivmeHomeComponent implements OnInit {
  private readonly seo = inject(SiteSeoService);
  private readonly content = inject(EduivmeSiteService);
  readonly view = signal<SiteHomeView | null>(null);

  async ngOnInit(): Promise<void> {
    let home: SiteHomeView;
    try {
      home = await firstValueFrom(this.content.getHome());
      if (!home.hero && !home.products.length) home = DEFAULT_HOME;
    } catch {
      home = DEFAULT_HOME;
    }
    this.view.set(home);
    this.seo.apply({
      title: home.site?.seoTitle || 'eduivme',
      description: home.site?.seoDescription || home.hero?.body || '',
      path: '/',
      image: '/brand/og-eduivme.png',
      siteName: 'eduivme',
      jsonLd: { '@context': 'https://schema.org', '@type': 'Organization', name: 'eduivme', url: 'https://eduivme.com', logo: 'https://eduivme.com/brand/eduivme-512.png' }
    });
  }

  section(home: SiteHomeView, slug: string): SitePublicEntry | undefined { return sectionOf(home.sections, slug); }
  mark(product: SitePublicEntry): string { return BRAND_MARKS[product.brand ?? 'eduivme'].mark; }
  scope(product: SitePublicEntry): string { return BRAND_MARKS[product.brand ?? 'eduivme'].scope; }
  audienceScope(index: number): string { return AUDIENCE_SCOPES[index % AUDIENCE_SCOPES.length]; }
  icon(entry: SitePublicEntry): string { return SITE_ICON_PATHS[entry.icon ?? 'star'] ?? SITE_ICON_PATHS.star; }
  external(url: string | null): boolean { return siteLink(url)?.kind === 'external'; }
}
