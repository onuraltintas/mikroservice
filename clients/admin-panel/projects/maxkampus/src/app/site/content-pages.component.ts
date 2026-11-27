import { Component, OnInit, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MaxkampusApi, SiteEntry } from '../core/api';
import { Seo } from '../core/seo';
import { SiteStore } from '../core/site-store';
import { TextBlock, textBlocks, trDate } from '../core/text';
import { PlanCardsComponent } from './plan-cards.component';

/** Text blocks as headings, paragraphs and lists (never HTML). */
@Component({
  selector: 'mk-prose',
  standalone: true,
  template: `
    <div class="mk-prose">
      @for (block of blocks(); track $index) {
        @switch (block.kind) {
          @case ('heading') { <h2>{{ $any(block).text }}</h2> }
          @case ('list') { <ul>@for (item of $any(block).items; track $index) { <li>{{ item }}</li> }</ul> }
          @default { <p>{{ $any(block).text }}</p> }
        }
      }
    </div>
  `
})
export class ProseComponent {
  readonly text = input<string | null | undefined>('');
  blocks(): TextBlock[] { return textBlocks(this.text()); }
}

/** /fiyatlar: the plans from billing and the editors' page text. */
@Component({
  selector: 'mk-pricing',
  standalone: true,
  imports: [PlanCardsComponent, ProseComponent, RouterLink],
  template: `
    <section class="mk-section"><div class="mk-wrap">
      <h1 class="page-title">{{ page()?.subtitle || 'Öğrenci sayınıza göre fiyat' }}</h1>
      @if (page()?.body) { <div class="mk-lead"><mk-prose [text]="page()!.body" /></div> }
      <mk-plan-cards [notes]="store.home()?.plans ?? []" />
      <p class="mk-muted after">Daha fazla öğrenciniz mi var? <a routerLink="/iletisim">Bize yazın</a>, kurumunuza uygun planı birlikte belirleyelim.</p>
    </div></section>
  `,
  styles: `.page-title { margin: 0 0 12px; font-size: clamp(30px, 4vw, 40px); } .after { margin-top: 24px; }`
})
export class PricingComponent implements OnInit {
  readonly store = inject(SiteStore);
  private readonly api = inject(MaxkampusApi);
  private readonly seo = inject(Seo);
  readonly page = signal<SiteEntry | null>(null);

  async ngOnInit(): Promise<void> {
    this.seo.set({ title: 'Fiyatlar', path: '/fiyatlar' });
    void this.store.load();
    const page = await this.api.page('fiyatlar').catch(() => null);
    this.page.set(page);
    if (page) this.seo.set({ title: page.seoTitle || 'Fiyatlar', description: page.seoDescription, path: '/fiyatlar' });
  }
}

/** /sayfa/:slug: a content page written in the admin panel. */
@Component({
  selector: 'mk-content-page',
  standalone: true,
  imports: [ProseComponent, RouterLink],
  template: `
    <article class="mk-section"><div class="mk-narrow">
      @if (loading()) { <p class="mk-status" role="status">Yükleniyor</p> }
      @else if (page(); as entry) {
        <h1>{{ entry.title }}</h1>
        @if (entry.subtitle) { <p class="mk-lead">{{ entry.subtitle }}</p> }
        @if (entry.imageUrl) { <img class="cover" [src]="entry.imageUrl" alt=""> }
        <mk-prose [text]="entry.body" />
      } @else {
        <h1>Sayfa bulunamadı</h1><p>Aradığınız sayfa kaldırılmış ya da adresi değişmiş olabilir. <a routerLink="/">Ana sayfaya dönün</a>.</p>
      }
    </div></article>
  `,
  styles: `h1 { margin: 0 0 12px; font-size: clamp(30px, 4vw, 40px); } .cover { margin: 0 0 24px; border-radius: 14px; }`
})
export class ContentPageComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  private readonly seo = inject(Seo);
  readonly slug = input.required<string>();
  readonly page = signal<SiteEntry | null>(null);
  readonly loading = signal(true);

  async ngOnInit(): Promise<void> {
    const page = await this.api.page(this.slug()).catch(() => null);
    this.page.set(page);
    this.loading.set(false);
    this.seo.set(page
      ? { title: page.seoTitle || page.title, description: page.seoDescription || page.subtitle, path: `/sayfa/${this.slug()}`, image: page.imageUrl }
      : { title: 'Sayfa bulunamadı', path: `/sayfa/${this.slug()}`, noindex: true });
  }
}

/** /yasal/:slug: KVKK, privacy, terms and cookies, as published in Identity (Ortak yasal sayfalar). */
@Component({
  selector: 'mk-legal-page',
  standalone: true,
  imports: [ProseComponent, RouterLink],
  template: `
    <article class="mk-section"><div class="mk-narrow">
      @if (loading()) { <p class="mk-status" role="status">Yükleniyor</p> }
      @else if (title()) {
        <h1>{{ title() }}</h1>
        <p class="mk-muted">Sürüm {{ version() }}{{ updated() ? ', ' + updated() : '' }}</p>
        <mk-prose [text]="content()" />
      } @else {
        <h1>Metin bulunamadı</h1><p>Bu yasal metin henüz yayımlanmadı. Sorularınız için <a routerLink="/iletisim">bize yazın</a>.</p>
      }
    </div></article>
  `,
  styles: `h1 { margin: 0 0 8px; font-size: clamp(28px, 4vw, 36px); }`
})
export class LegalPageComponent {
  private readonly api = inject(MaxkampusApi);
  private readonly seo = inject(Seo);
  readonly slug = input.required<string>();
  readonly title = signal('');
  readonly content = signal('');
  readonly version = signal(0);
  readonly updated = signal('');
  readonly loading = signal(true);

  // The router reuses this component when a footer link moves from one legal document to another, so the document,
  // its title and its canonical address follow the slug, not only the first one opened (27.11.2026).
  constructor() {
    effect(() => {
      const slug = this.slug();
      untracked(() => void this.load(slug));
    });
  }

  private async load(slug: string): Promise<void> {
    this.loading.set(true);
    try {
      const page = await this.api.legal(slug);
      if (slug !== this.slug()) return;
      this.title.set(page.title);
      this.content.set(page.content);
      this.version.set(page.version);
      this.updated.set(trDate(page.updatedAt ?? page.createdAt));
      this.seo.set({ title: page.title, path: `/yasal/${slug}` });
    } catch {
      if (slug !== this.slug()) return;
      this.title.set('');
      this.content.set('');
      this.seo.set({ title: 'Yasal metin', path: `/yasal/${slug}`, noindex: true });
    } finally {
      if (slug === this.slug()) this.loading.set(false);
    }
  }
}

/** Anything else: a page that does not exist. */
@Component({
  selector: 'mk-not-found',
  standalone: true,
  imports: [RouterLink],
  template: `<section class="mk-section"><div class="mk-narrow"><h1>Sayfa bulunamadı</h1>
    <p>Aradığınız sayfa kaldırılmış ya da adresi değişmiş olabilir.</p><a routerLink="/" class="ds-btn ds-btn--primary">Ana sayfaya dönün</a></div></section>`
})
export class NotFoundComponent implements OnInit {
  private readonly seo = inject(Seo);
  ngOnInit(): void { this.seo.set({ title: 'Sayfa bulunamadı', path: '/404', noindex: true }); }
}
