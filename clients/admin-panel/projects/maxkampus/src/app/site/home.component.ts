import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MaxkampusApi, SiteEntry } from '../core/api';
import { Seo } from '../core/seo';
import { SiteStore } from '../core/site-store';
import { trDate } from '../core/text';
import { PlanCardsComponent } from './plan-cards.component';
import { SiteIconComponent } from './site-icon.component';
import { SiteLinkComponent } from './site-link.component';

const WEEK: { day: string; items: { text: string; amber?: boolean }[] }[] = [
  { day: 'Pzt', items: [{ text: '09.00 8-A Mat' }, { text: '14.00 LGS Fen' }] },
  { day: 'Sal', items: [{ text: '10.00 7-B Türkçe', amber: true }] },
  { day: 'Çar', items: [{ text: '09.00 8-A Mat' }, { text: '16.00 Birebir' }] },
  { day: 'Per', items: [{ text: '14.00 LGS Fen' }] },
  { day: 'Cum', items: [{ text: '09.00 8-A Mat' }] }
];

/** maxkampus.com: every word comes from the admin panel (maxkampüs › Site içeriği); prices from billing. */
@Component({
  selector: 'mk-home',
  standalone: true,
  imports: [RouterLink, SiteLinkComponent, SiteIconComponent, PlanCardsComponent],
  template: `
    @if (store.home(); as home) {
      <section class="hero" aria-labelledby="hero-title">
        <div class="hero__glow" aria-hidden="true"><span class="hero__blob hero__blob--amber"></span><span class="hero__blob hero__blob--blue"></span></div>
        <div class="mk-wrap hero__grid">
          <div class="hero__text mk-enter">
            @if (home.hero?.subtitle) { <span class="hero__tag">{{ home.hero!.subtitle }}</span> }
            <h1 id="hero-title">{{ home.hero?.title || 'Kursunuzun bütün haftası tek ekranda' }}</h1>
            @if (home.hero?.body) { <p class="hero__lead">{{ home.hero!.body }}</p> }
            <div class="hero__actions">
              @if (home.hero?.linkUrl) { <mk-site-link [href]="home.hero!.linkUrl!" cls="ds-btn ds-btn--large mk-btn-amber">{{ home.hero!.linkLabel }}</mk-site-link> }
              @if (home.hero?.secondaryLinkUrl) { <mk-site-link [href]="home.hero!.secondaryLinkUrl!" cls="ds-btn ds-btn--large ds-btn--secondary mk-btn-ghost">{{ home.hero!.secondaryLinkLabel }}</mk-site-link> }
            </div>
          </div>
          @if (home.hero?.imageUrl) {
            <img class="hero__image" [src]="home.hero!.imageUrl" alt="" width="560" height="380">
          } @else {
            <div class="week mk-enter" style="--i: 2" role="img" aria-label="Haftalık takvim örneği: beş günde yedi ders, yoklaması alınmayan ders yok.">
              <div class="week__head"><strong>Bu hafta</strong><span>18 ders, 3 öğretmen</span></div>
              <div class="week__days">
                @for (column of week; track column.day) {
                  <div class="week__day"><span>{{ column.day }}</span>
                    @for (item of column.items; track item.text) { <span class="week__lesson" [class.week__lesson--amber]="item.amber">{{ item.text }}</span> }
                  </div>
                }
              </div>
              <div class="week__foot"><span>Yoklaması alınmayan ders</span><strong class="num">0</strong></div>
            </div>
          }
        </div>
      </section>

      @for (section of home.sections; track section.id) {
        @switch (section.slug) {
          @case ('features') {
            @if (home.features.length) {
              <section class="mk-section" id="ozellikler" [attr.aria-labelledby]="'s-' + section.slug">
                <div class="mk-wrap">
                  <h2 [id]="'s-' + section.slug">{{ section.title }}</h2>
                  @if (section.body) { <p class="mk-lead">{{ section.body }}</p> }
                  <ul class="mk-grid plain mk-tones">
                    @for (item of home.features; track item.id) {
                      <li class="mk-card feature mk-lift mk-reveal"><mk-site-icon [name]="item.icon" /><h3>{{ item.title }}</h3><p>{{ item.body }}</p></li>
                    }
                  </ul>
                </div>
              </section>
            }
          }
          @case ('steps') {
            @if (home.steps.length) {
              <section class="mk-section band" [attr.aria-labelledby]="'s-' + section.slug">
                <div class="mk-wrap">
                  <h2 [id]="'s-' + section.slug">{{ section.title }}</h2>
                  @if (section.body) { <p class="mk-lead">{{ section.body }}</p> }
                  <ol class="steps">
                    @for (item of home.steps; track item.id; let i = $index) {
                      <li class="mk-reveal"><span class="steps__num num" aria-hidden="true">{{ i + 1 }}</span><div><h3>{{ item.title }}</h3><p>{{ item.body }}</p></div></li>
                    }
                  </ol>
                </div>
              </section>
            }
          }
          @case ('reviews') {
            @if (home.reviews.length) {
              <section class="mk-section" [attr.aria-labelledby]="'s-' + section.slug">
                <div class="mk-wrap">
                  <h2 [id]="'s-' + section.slug">{{ section.title }}</h2>
                  @if (section.body) { <p class="mk-lead">{{ section.body }}</p> }
                  <ul class="mk-grid plain">
                    @for (item of home.reviews; track item.id) {
                      <li class="mk-reveal"><figure class="mk-card review">
                        <blockquote>{{ item.body }}</blockquote>
                        <figcaption>
                          @if (item.imageUrl) { <img [src]="item.imageUrl" alt="" width="44" height="44"> }
                          <span><strong>{{ item.title }}</strong>@if (item.subtitle) { <span class="mk-muted">{{ item.subtitle }}</span> }</span>
                        </figcaption>
                      </figure></li>
                    }
                  </ul>
                </div>
              </section>
            }
          }
          @case ('pricing') {
            <section class="mk-section band" [attr.aria-labelledby]="'s-' + section.slug">
              <div class="mk-wrap">
                <h2 [id]="'s-' + section.slug">{{ section.title }}</h2>
                @if (section.body) { <p class="mk-lead">{{ section.body }}</p> }
                <mk-plan-cards [notes]="home.plans" />
                <p class="more"><a routerLink="/fiyatlar">Plan ayrıntıları ve sık sorulanlar</a></p>
              </div>
            </section>
          }
          @case ('faq') {
            @if (home.faqs.length) {
              <section class="mk-section" [attr.aria-labelledby]="'s-' + section.slug">
                <div class="mk-narrow">
                  <h2 [id]="'s-' + section.slug">{{ section.title }}</h2>
                  @for (item of home.faqs; track item.id) {
                    <details class="faq"><summary>{{ item.title }}</summary><p>{{ item.body }}</p></details>
                  }
                </div>
              </section>
            }
          }
          @case ('blog') {
            @if (posts().length) {
              <section class="mk-section band" [attr.aria-labelledby]="'s-' + section.slug">
                <div class="mk-wrap">
                  <h2 [id]="'s-' + section.slug">{{ section.title }}</h2>
                  @if (section.body) { <p class="mk-lead">{{ section.body }}</p> }
                  <ul class="mk-grid plain">
                    @for (post of posts(); track post.id) {
                      <li class="mk-card post mk-lift mk-reveal">
                        @if (post.category) { <span class="mk-pill">{{ post.category }}</span> }
                        <h3><a [routerLink]="['/blog', post.slug]">{{ post.title }}</a></h3>
                        @if (post.subtitle) { <p>{{ post.subtitle }}</p> }
                        <span class="mk-muted">{{ date(post.publishedAt) }}</span>
                      </li>
                    }
                  </ul>
                  <p class="more"><a routerLink="/blog">Bütün yazılar</a></p>
                </div>
              </section>
            }
          }
          @case ('cta') {
            <section class="cta" [attr.aria-labelledby]="'s-' + section.slug">
              <div class="mk-wrap cta__inner">
                <div><h2 [id]="'s-' + section.slug">{{ section.title }}</h2>@if (section.body) { <p>{{ section.body }}</p> }</div>
                @if (section.linkUrl) { <mk-site-link [href]="section.linkUrl" cls="ds-btn ds-btn--large mk-btn-amber">{{ section.linkLabel || 'Kurumunuzu açın' }}</mk-site-link> }
              </div>
            </section>
          }
        }
      }
    } @else if (store.failed()) {
      <div class="mk-wrap mk-status" role="alert"><p>Sayfa yüklenemedi. İnternet bağlantınızı kontrol edip sayfayı yenileyin.</p>
        <button type="button" class="ds-btn ds-btn--secondary" (click)="retry()">Yeniden dene</button></div>
    } @else {
      <p class="mk-status" role="status"><span class="ds-spinner" aria-hidden="true"></span> Yükleniyor</p>
    }
  `,
  styles: `
    .plain { list-style: none; margin: 0; padding: 0; }
    /* 27.11.2026: the opening block follows the theme: warm and light by default, graphite in the dark theme.
       Amber and blue light drift behind it; the sample week floats gently (off under reduced motion). */
    .hero { --h-bg: var(--ds-page-tint, var(--ds-bg)); --h-text: var(--ds-text); --h-muted: var(--ds-muted); --h-card: var(--ds-surface);
      --h-line: var(--ds-line); --h-chip: var(--ds-soft); --h-tag-bg: var(--mk-soft); --h-tag-fg: var(--mk-on-soft);
      position: relative; overflow: hidden; isolation: isolate; background: var(--h-bg); color: var(--h-text); }
    :host-context(html.dark) .hero { --h-bg: var(--ds-visual-bg); --h-text: var(--ds-visual-text); --h-muted: var(--mk-hero-muted); --h-card: var(--mk-hero-card);
      --h-line: var(--mk-hero-line); --h-chip: var(--mk-hero-line); --h-tag-bg: var(--mk-hero-line); --h-tag-fg: var(--mk-on-soft); }
    .hero__glow { position: absolute; inset: 0; z-index: -1; pointer-events: none; }
    .hero__blob { position: absolute; border-radius: 50%; filter: blur(70px); opacity: 0.45; animation: public-drift 16s ease-in-out infinite; }
    .hero__blob--amber { width: 420px; height: 420px; right: 6%; top: -140px; background: var(--ds-accent); }
    .hero__blob--blue { width: 320px; height: 320px; left: -100px; bottom: -160px; background: var(--mk-chip-blue); animation-delay: -7s; }
    :host-context(html.dark) .hero__blob { opacity: 0.22; }
    .hero__grid { display: flex; flex-wrap: wrap; align-items: center; gap: 48px; padding-top: 80px; padding-bottom: 88px; }
    .hero__text { flex: 1 1 460px; min-width: 0; display: flex; flex-direction: column; gap: 22px; }
    .hero__tag { align-self: flex-start; padding: 6px 14px; border-radius: 999px; background: var(--h-tag-bg); color: var(--h-tag-fg); font-size: 14px; font-weight: 600; }
    .hero h1 { margin: 0; font-size: clamp(38px, 5.2vw, 58px); line-height: 1.05; letter-spacing: -0.03em; text-wrap: balance; }
    .hero__lead { margin: 0; max-width: 34em; color: var(--h-muted); font-size: 19px; }
    .hero__actions { display: flex; flex-wrap: wrap; gap: 12px; }
    :host-context(html:not(.dark)) .hero ::ng-deep .mk-btn-ghost { border-color: var(--ds-line); background: var(--ds-surface); color: var(--ds-text); }
    :host-context(html:not(.dark)) .hero ::ng-deep .mk-btn-ghost:hover { background: var(--ds-soft); }
    .hero__image { flex: 1 1 440px; min-width: 0; border-radius: 16px; }
    .week { flex: 1 1 440px; min-width: 0; padding: 22px; border: 1px solid var(--h-line); border-radius: 20px; background: var(--h-card);
      box-shadow: 0 30px 60px -30px color-mix(in srgb, var(--ds-accent) 55%, transparent); animation: public-float 7s ease-in-out infinite 1s; }
    .week__head, .week__foot { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 10px; }
    .week__head { margin-bottom: 14px; } .week__head span { color: var(--h-muted); font-size: 14px; }
    .week__days { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 10px; }
    .week__day { display: flex; flex-direction: column; gap: 6px; } .week__day > span:first-child { color: var(--h-muted); font-size: 13px; }
    .week__lesson { padding: 8px 10px; border-radius: 8px; background: var(--h-chip); color: var(--h-text); font-size: 13px; font-weight: 600; }
    .week__lesson--amber { background: var(--ds-accent); color: var(--ds-on-accent); }
    .week__foot { margin-top: 16px; padding: 12px 14px; border-radius: 10px; background: var(--h-chip); }
    .week__foot strong { color: var(--ds-success); }
    :host-context(html.dark) .week__foot strong { color: var(--ds-accent); }
    @media (prefers-reduced-motion: reduce) { .hero__blob, .week { animation: none; } }
    @media (max-width: 560px) { .week__days { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
    .band { background: var(--ds-surface); border-top: 1px solid var(--ds-line); border-bottom: 1px solid var(--ds-line); }
    .feature { display: flex; flex-direction: column; gap: 10px; } .feature h3 { margin: 0; font-size: 19px; } .feature p { margin: 0; color: var(--ds-muted); }
    .steps { display: flex; flex-wrap: wrap; gap: 28px; margin: 0; padding: 0; list-style: none; }
    .steps li { flex: 1 1 240px; display: flex; gap: 14px; align-items: flex-start; }
    .steps__num { flex: none; display: inline-flex; align-items: center; justify-content: center; width: 48px; height: 48px; border-radius: 14px; background: var(--ds-accent); color: var(--ds-on-accent); font-size: 20px; font-weight: 700; box-shadow: 0 10px 20px -10px color-mix(in srgb, var(--ds-accent) 80%, transparent); }
    .steps h3 { margin: 0 0 4px; font-size: 18px; } .steps p { margin: 0; color: var(--ds-muted); }
    .review { margin: 0; display: flex; flex-direction: column; gap: 14px; height: 100%; }
    .review blockquote { margin: 0; font-size: 17px; line-height: 1.6; }
    .review figcaption { display: flex; align-items: center; gap: 12px; } .review figcaption > span { display: flex; flex-direction: column; }
    .review img { border-radius: 999px; object-fit: cover; }
    .faq { border-top: 1px solid var(--ds-line); padding: 4px 0; }
    .faq summary { min-height: 52px; display: flex; align-items: center; font-weight: 600; font-size: 17px; cursor: pointer; }
    .faq p { margin: 0 0 16px; color: var(--ds-muted); }
    .post { display: flex; flex-direction: column; gap: 10px; align-items: flex-start; } .post h3 { margin: 0; font-size: 19px; } .post p { margin: 0; color: var(--ds-muted); }
    .post h3 a { color: var(--ds-text); text-decoration: none; } .post h3 a:hover { text-decoration: underline; }
    .more { margin: 24px 0 0; font-weight: 600; }
    .cta { position: relative; overflow: hidden; isolation: isolate; background: var(--mk-soft); }
    .cta::after { content: ''; position: absolute; z-index: -1; right: -80px; top: -120px; width: 300px; height: 300px; border-radius: 50%; border: 36px solid color-mix(in srgb, var(--ds-accent) 45%, transparent); }
    .cta__inner { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 24px; padding-top: 48px; padding-bottom: 48px; }
    .cta h2 { margin: 0 0 6px; font-size: 30px; color: var(--mk-mark); } .cta p { margin: 0; color: var(--mk-on-soft); }
  `
})
export class HomeComponent implements OnInit {
  readonly store = inject(SiteStore);
  private readonly api = inject(MaxkampusApi);
  private readonly seo = inject(Seo);
  readonly week = WEEK;
  readonly posts = signal<SiteEntry[]>([]);
  readonly date = trDate;
  readonly hasBlog = computed(() => !!this.store.section('blog'));

  async ngOnInit(): Promise<void> {
    this.seo.set({ title: 'maxkampüs: kurs ve özel ders yönetimi', path: '/' });
    const home = await this.store.load();
    if (home?.site) this.seo.set({ title: home.site.seoTitle || 'maxkampüs: kurs ve özel ders yönetimi', description: home.site.seoDescription, path: '/' });
    if (home?.sections.some(item => item.slug === 'blog')) {
      this.api.posts(null, 0, 3).then(page => this.posts.set(page.items)).catch(() => this.posts.set([]));
    }
  }

  retry(): void { void this.store.load(); }
}
