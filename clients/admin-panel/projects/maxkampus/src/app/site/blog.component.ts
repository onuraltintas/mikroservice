import { Component, OnInit, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MaxkampusApi, SiteEntry } from '../core/api';
import { Seo } from '../core/seo';
import { trDate } from '../core/text';
import { ProseComponent } from './content-pages.component';
import { NewsletterFormComponent } from './newsletter-form.component';

const PAGE = 12;

/** /blog: posts newest first, filtered by topic (?konu=). */
@Component({
  selector: 'mk-blog',
  standalone: true,
  imports: [RouterLink, NewsletterFormComponent],
  template: `
    <section class="mk-section"><div class="mk-wrap">
      <h1>Blog</h1>
      <p class="mk-lead">Kurs yönetimi, veli iletişimi ve ders planlama üzerine yazılar.</p>
      @if (categories().length) {
        <nav class="topics" aria-label="Konular">
          <a routerLink="/blog" [class.on]="!konu()" [attr.aria-current]="!konu() ? 'page' : null">Hepsi</a>
          @for (topic of categories(); track topic) {
            <a routerLink="/blog" [queryParams]="{ konu: topic }" [class.on]="konu() === topic" [attr.aria-current]="konu() === topic ? 'page' : null">{{ topic }}</a>
          }
        </nav>
      }
      @if (loading()) { <p class="mk-status" role="status">Yazılar yükleniyor</p> }
      @else if (error()) { <p class="ds-alert" role="alert">{{ error() }}</p> }
      @else if (!posts().length) { <p class="mk-notice">Henüz yazı yok. Yeni yazılardan haberdar olmak için bültene abone olun.</p> }
      @else {
        <ul class="mk-grid plain">
          @for (post of posts(); track post.id) {
            <li class="mk-card post">
              @if (post.imageUrl) { <img [src]="post.imageUrl" alt="" loading="lazy" width="400" height="220"> }
              @if (post.category) { <span class="mk-pill">{{ post.category }}</span> }
              <h2><a [routerLink]="['/blog', post.slug]">{{ post.title }}</a></h2>
              @if (post.subtitle) { <p>{{ post.subtitle }}</p> }
              <span class="mk-muted">{{ date(post.publishedAt) }}{{ post.author ? ', ' + post.author : '' }}</span>
            </li>
          }
        </ul>
        @if (total() > posts().length) {
          <p class="more"><button type="button" class="ds-btn ds-btn--secondary" (click)="more()" [disabled]="busy()">Daha fazla yazı</button></p>
        }
      }
      <section class="mk-card subscribe" aria-labelledby="blog-newsletter">
        <h2 id="blog-newsletter">Bülten</h2>
        <p class="mk-muted">Yeni yazılar ve özellikler ayda bir e-postayla. Onay e-postasındaki bağlantıya tıklayınca abonelik başlar.</p>
        <mk-newsletter-form source="blog" headingId="blog-newsletter" inputId="blog-newsletter-email" />
      </section>
    </div></section>
  `,
  styles: `
    h1 { margin: 0 0 12px; font-size: clamp(30px, 4vw, 40px); }
    .plain { list-style: none; margin: 0; padding: 0; }
    .topics { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 28px; }
    .topics a { display: inline-flex; align-items: center; min-height: 40px; padding: 0 14px; border: 1px solid var(--ds-line); border-radius: 999px; background: var(--ds-surface); color: var(--ds-text); text-decoration: none; }
    .topics a.on { background: var(--ds-primary); border-color: var(--ds-primary); color: var(--ds-on-primary); }
    .post { display: flex; flex-direction: column; gap: 10px; align-items: flex-start; }
    .post img { width: 100%; border-radius: 10px; object-fit: cover; aspect-ratio: 16 / 9; }
    .post h2 { margin: 0; font-size: 20px; } .post h2 a { color: var(--ds-text); text-decoration: none; } .post h2 a:hover { text-decoration: underline; }
    .post p { margin: 0; color: var(--ds-muted); }
    .more { margin-top: 24px; text-align: center; }
    .subscribe { margin-top: 48px; max-width: 640px; } .subscribe h2 { margin: 0 0 6px; font-size: 22px; }
  `
})
export class BlogComponent {
  private readonly api = inject(MaxkampusApi);
  private readonly seo = inject(Seo);
  readonly konu = input<string | undefined>();
  readonly posts = signal<SiteEntry[]>([]);
  readonly categories = signal<string[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly date = trDate;

  constructor() {
    // A topic link keeps this page and changes only ?konu=.
    effect(() => {
      const topic = this.konu();
      untracked(() => {
        this.seo.set({ title: topic ? `${topic} yazıları` : 'Blog', path: topic ? `/blog?konu=${encodeURIComponent(topic)}` : '/blog',
          description: 'Kurs yönetimi, veli iletişimi ve ders planlama üzerine yazılar.' });
        this.loading.set(true);
        void this.load(false);
      });
    });
  }

  async load(append: boolean): Promise<void> {
    this.busy.set(true);
    try {
      const page = await this.api.posts(this.konu() ?? null, append ? this.posts().length : 0, PAGE);
      this.posts.set(append ? [...this.posts(), ...page.items] : page.items);
      this.total.set(page.total);
      this.categories.set(page.categories);
    } catch {
      this.error.set('Yazılar yüklenemedi. Sayfayı yenileyin.');
    } finally {
      this.loading.set(false);
      this.busy.set(false);
    }
  }

  more(): void { void this.load(true); }
}

/** /blog/:slug: one post. */
@Component({
  selector: 'mk-post',
  standalone: true,
  imports: [RouterLink, ProseComponent, NewsletterFormComponent],
  template: `
    <article class="mk-section"><div class="mk-narrow">
      @if (loading()) { <p class="mk-status" role="status">Yükleniyor</p> }
      @else if (post(); as item) {
        <p class="back"><a routerLink="/blog">Blog</a>@if (item.category) { <span aria-hidden="true"> / </span><a routerLink="/blog" [queryParams]="{ konu: item.category }">{{ item.category }}</a> }</p>
        <h1>{{ item.title }}</h1>
        @if (item.subtitle) { <p class="mk-lead">{{ item.subtitle }}</p> }
        <p class="mk-muted">{{ date(item.publishedAt) }}{{ item.author ? ', ' + item.author : '' }}</p>
        @if (item.imageUrl) { <img class="cover" [src]="item.imageUrl" alt=""> }
        <mk-prose [text]="item.body" />
        <aside class="mk-card end" aria-labelledby="post-newsletter">
          <h2 id="post-newsletter">Yeni yazıları kaçırmayın</h2>
          <mk-newsletter-form source="post" headingId="post-newsletter" inputId="post-newsletter-email" />
        </aside>
      } @else {
        <h1>Yazı bulunamadı</h1><p>Bu yazı kaldırılmış ya da adresi değişmiş olabilir. <a routerLink="/blog">Bütün yazılar</a></p>
      }
    </div></article>
  `,
  styles: `
    h1 { margin: 0 0 12px; font-size: clamp(30px, 4.4vw, 42px); }
    .back { margin: 0 0 12px; } .cover { margin: 8px 0 28px; border-radius: 14px; }
    .end { margin-top: 40px; } .end h2 { margin: 0 0 12px; font-size: 20px; }
  `
})
export class PostComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  private readonly seo = inject(Seo);
  private readonly router = inject(Router);
  readonly slug = input.required<string>();
  readonly post = signal<SiteEntry | null>(null);
  readonly loading = signal(true);
  readonly date = trDate;

  async ngOnInit(): Promise<void> {
    const post = await this.api.post(this.slug()).catch(() => null);
    this.post.set(post);
    this.loading.set(false);
    this.seo.set(post
      ? { title: post.seoTitle || post.title, description: post.seoDescription || post.subtitle, path: `/blog/${this.slug()}`, image: post.imageUrl }
      : { title: 'Yazı bulunamadı', path: `/blog/${this.slug()}`, noindex: true });
  }
}
