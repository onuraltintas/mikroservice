import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { EduivmeSiteService, SiteBrand, SitePageView, SitePublicEntry } from '../../../core/services/eduivme-site.service';
import { SiteSeoService } from '../../../core/services/site-seo.service';
import { EduivmeHrefDirective } from './eduivme-link.directive';
import { BRAND_MARKS, DEFAULT_PAGES, SITE_ICON_PATHS, brandForLink, sectionOf } from './eduivme-site-content';
import { EduivmeSiteLayoutComponent } from './eduivme-site-layout.component';

/**
 * eduivme.com/urunler/{slug}: the page of a product that has no site of its own (first: Kurs Yönetimi), or a guide page
 * such as "Hangi ürün kime uygun" whose cards lead to the products' own sites.
 */
@Component({
  selector: 'app-eduivme-product-page',
  standalone: true,
  imports: [EduivmeSiteLayoutComponent, EduivmeHrefDirective, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-eduivme-site-layout [site]="view()?.site ?? null">
      @if (view(); as data) {
        <section class="hero" data-brand="eduivme" aria-labelledby="page-title">
          <div class="hero__inner">
            <div class="hero__text">
              <nav class="crumb" aria-label="Konum"><a routerLink="/" fragment="urunler">Ürünler</a><span aria-hidden="true">/</span><span aria-current="page">{{ data.page.title }}</span></nav>
              <div class="page-brand"><img [src]="mark(data.page)" alt="" width="56" height="56"><span>{{ data.page.title }}</span></div>
              <h1 id="page-title">{{ data.page.subtitle || data.page.title }}</h1>
              @if (data.page.body) { <p>{{ data.page.body }}</p> }
              <div class="hero__actions">
                @if (data.page.linkLabel) { <a class="ds-btn ds-btn--large hero__primary" [eduivmeHref]="data.page.linkUrl">{{ data.page.linkLabel }}</a> }
                @if (data.page.secondaryLinkLabel) { <a class="ds-btn ds-btn--large hero__secondary" [eduivmeHref]="data.page.secondaryLinkUrl">{{ data.page.secondaryLinkLabel }}</a> }
              </div>
            </div>
            @if (data.page.imageUrl) {
              <img class="hero__image" [src]="data.page.imageUrl" alt="" width="460" height="400">
            } @else {
              <div class="hero__art" aria-hidden="true">
                <svg viewBox="0 0 360 420" width="360" height="420">
                  <rect class="hero__stem" x="128" y="182" width="104" height="226" rx="52"></rect>
                  <path class="hero__arc" d="M84 128c28-56 90-86 146-75" fill="none" stroke-width="14" stroke-linecap="round"></path>
                  <circle class="hero__dot" cx="262" cy="70" r="52"></circle>
                </svg>
                <img class="hero__chip hero__chip--1" [src]="mark(data.page)" alt="" width="64" height="64">
              </div>
            }
          </div>
        </section>

        @if (data.features.length) {
          <section class="band band--soft" aria-labelledby="features-title">
            <div class="wrap">
              <header class="head"><h2 id="features-title">{{ section(data, 'features')?.title || 'Neler yapabilirsiniz' }}</h2>@if (section(data, 'features')?.body) {<p>{{ section(data, 'features')?.body }}</p>}</header>
              <ul class="features">
                @for (feature of data.features; track feature.id) {
                  <li class="feature">
                    <span class="feature__icon" aria-hidden="true"><svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path [attr.d]="icon(feature)"/></svg></span>
                    <h3>{{ feature.title }}</h3>
                    @if (feature.body) { <p>{{ feature.body }}</p> }
                  </li>
                }
              </ul>
            </div>
          </section>
        }

        @if (data.audiences.length) {
          <section class="band" aria-labelledby="audiences-title">
            <div class="wrap">
              <header class="head"><h2 id="audiences-title">{{ section(data, 'audiences')?.title || 'Kimler için' }}</h2>@if (section(data, 'audiences')?.body) {<p>{{ section(data, 'audiences')?.body }}</p>}</header>
              <ul class="pair">
                @for (audience of data.audiences; track audience.id; let i = $index) {
                  @let product = productOf(audience);
                  <li class="audience" [attr.data-brand]="product ? scopeOf(product) : (i % 2 === 0 ? 'eduivme' : 'maxkoc')"
                    [class.audience--accent]="!product && i % 2 === 0" [class.audience--product]="!!product">
                    @if (product) {
                      <img class="audience__mark" [src]="markOf(product)" alt="" width="56" height="56">
                    } @else {
                      <span class="audience__icon" aria-hidden="true"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path [attr.d]="icon(audience)"/></svg></span>
                    }
                    <h3>{{ audience.title }}</h3>
                    @if (audience.body) { <p>{{ audience.body }}</p> }
                    @if (audience.linkLabel && audience.linkUrl) {
                      <a class="audience__link" [eduivmeHref]="audience.linkUrl">{{ audience.linkLabel }}@if (product) {<span class="ds-visually-hidden"> (ürünün kendi sitesi)</span>}</a>
                    }
                  </li>
                }
              </ul>
            </div>
          </section>
        }

        @if (data.steps.length) {
          <section class="band" aria-labelledby="steps-title">
            <div class="wrap">
              <header class="head"><h2 id="steps-title">{{ section(data, 'steps')?.title || 'Nasıl başlanır' }}</h2>@if (section(data, 'steps')?.body) {<p>{{ section(data, 'steps')?.body }}</p>}</header>
              <ol class="steps">
                @for (step of data.steps; track step.id; let i = $index) {
                  <li class="step"><span class="step__number" aria-hidden="true">{{ i + 1 }}</span><h3><span class="ds-visually-hidden">{{ i + 1 }}. adım: </span>{{ step.title }}</h3>@if (step.body) {<p>{{ step.body }}</p>}</li>
                }
              </ol>
            </div>
          </section>
        }

        @if (data.faqs.length) {
          <section class="band" aria-labelledby="faq-title">
            <div class="wrap wrap--narrow">
              <header class="head"><h2 id="faq-title">{{ section(data, 'faq')?.title || 'Sık sorulanlar' }}</h2>@if (section(data, 'faq')?.body) {<p>{{ section(data, 'faq')?.body }}</p>}</header>
              <div class="faq">
                @for (faq of data.faqs; track faq.id; let first = $first) {
                  <details [open]="first"><summary>{{ faq.title }}</summary><p>{{ faq.body }}</p></details>
                }
              </div>
            </div>
          </section>
        }

        @if (data.page.ctaTitle) {
          <section class="band" aria-labelledby="cta-title">
            <div class="wrap">
              <div class="cta">
                <div><h2 id="cta-title">{{ data.page.ctaTitle }}</h2>@if (data.page.ctaBody) {<p>{{ data.page.ctaBody }}</p>}</div>
                @if (data.page.linkLabel) { <a class="ds-btn ds-btn--large cta__button" [eduivmeHref]="data.page.linkUrl">{{ data.page.linkLabel }}</a> }
              </div>
            </div>
          </section>
        }
      } @else if (missing()) {
        <div class="wrap missing">
          <h1>Bu ürün sayfası bulunamadı.</h1>
          <p>Sayfa kaldırılmış ya da adresi değişmiş olabilir. Bütün ürünleri ana sayfada görebilirsiniz.</p>
          <a class="ds-btn ds-btn--primary" routerLink="/" fragment="urunler">Ürünlere dön</a>
        </div>
      } @else {
        <div class="loading" role="status"><span class="ds-spinner" aria-hidden="true"></span><span class="ds-visually-hidden">Sayfa yükleniyor</span></div>
      }
    </app-eduivme-site-layout>
  `,
  styleUrls: ['./eduivme-site.scss', './eduivme-product-page.scss']
})
export class EduivmeProductPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly content = inject(EduivmeSiteService);
  private readonly seo = inject(SiteSeoService);
  readonly view = signal<SitePageView | null>(null);
  readonly missing = signal(false);

  async ngOnInit(): Promise<void> {
    const slug = this.route.snapshot.paramMap.get('slug') ?? '';
    let page: SitePageView | null;
    try {
      page = await firstValueFrom(this.content.getPage(slug));
    } catch (error) {
      // Not found is an answer; anything else (service down) falls back to the built-in copy of the page.
      page = error instanceof HttpErrorResponse && error.status === 404 ? null : DEFAULT_PAGES[slug] ?? null;
    }
    if (!page) {
      this.missing.set(true);
      return;
    }
    this.view.set(page);
    this.seo.apply({
      title: page.page.seoTitle || `${page.page.title} | eduivme`,
      description: page.page.seoDescription || page.page.body,
      path: `/urunler/${slug}`,
      image: '/brand/og-eduivme.png',
      siteName: 'eduivme'
    });
  }

  section(data: SitePageView, slug: string): SitePublicEntry | undefined { return sectionOf(data.sections, slug); }
  mark(entry: SitePublicEntry): string { return BRAND_MARKS[entry.brand ?? 'eduivme'].mark; }
  icon(entry: SitePublicEntry): string { return SITE_ICON_PATHS[entry.icon ?? 'star'] ?? SITE_ICON_PATHS.star; }
  /** A "Kimler için" card that leads to a product's own site is that product's card (its mark and colours). */
  productOf(entry: SitePublicEntry): SiteBrand | null { return entry.brand ?? brandForLink(entry.linkUrl); }
  scopeOf(brand: SiteBrand): string { return BRAND_MARKS[brand].scope; }
  markOf(brand: SiteBrand): string { return BRAND_MARKS[brand].mark; }
}
