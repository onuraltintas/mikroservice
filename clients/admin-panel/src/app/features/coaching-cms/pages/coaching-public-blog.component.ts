import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { CoachingCmsEntry, CoachingManagementPage, CoachingManagementService } from '../../../core/services/coaching-management.service';

@Component({
  selector: 'app-coaching-public-blog',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <section class="mx-auto max-w-6xl space-y-7" aria-labelledby="coaching-blog-title">
      <header class="blog-heading rounded-[1.75rem] px-6 py-8 sm:px-9 sm:py-10"><p class="text-xs font-bold uppercase tracking-[.14em] text-indigo-700 dark:text-indigo-300">{{ heading()?.eyebrow || 'Koçluk kaynakları' }}</p><h1 id="coaching-blog-title" class="mt-3 text-3xl font-semibold tracking-tight sm:text-4xl">{{ heading()?.title || 'Öğrenme yolculuğuna eşlik eden yazılar' }}</h1><p class="mt-3 max-w-2xl text-sm leading-6 text-slate-600 dark:text-slate-300">{{ heading()?.summary || 'Hedef belirleme, çalışma planı ve koçluk görüşmeleri için uygulamaya dönük kısa rehberler.' }}</p></header>
      @if (loading()) { <p role="status" class="text-center text-sm text-slate-500">Yazılar yükleniyor…</p> }
      @if (error()) { <div role="alert" class="rounded-xl bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div> }
      <div class="grid gap-4 md:grid-cols-2">@for (post of page().items; track post.id) {<article class="blog-list-card rounded-2xl border border-slate-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">@if (isCoachingMediaUrl(post.coverImageUrl)) {<img [src]="post.coverImageUrl" [alt]="post.title" class="mb-4 h-48 w-full rounded-xl object-cover" />}<p class="text-xs font-medium text-slate-500">{{ post.publishedAt || post.scheduledPublishAt || post.updatedAt || post.createdAt | date:'dd MMMM yyyy' }} @if (post.author) {<span> · {{ post.author }}</span>}</p><h2 class="mt-3 text-xl font-semibold leading-7 tracking-tight">{{ post.title }}</h2><p class="mt-3 line-clamp-4 whitespace-pre-wrap text-sm leading-6 text-slate-600 dark:text-slate-300">{{ post.summary || post.content.slice(0, 400) }}</p><a [routerLink]="['/coaching/blog', post.slug]" class="mt-5 inline-flex items-center gap-1 text-sm font-semibold text-indigo-700 dark:text-indigo-300">Yazıyı oku <span aria-hidden="true">→</span></a></article>} @empty { @if (!loading()) { <p class="col-span-full rounded-2xl border border-dashed border-slate-300 p-10 text-center text-sm text-slate-500 dark:border-slate-700">Henüz yayınlanmış yazı yok. Yeni yazılar eklendiğinde burada görebilirsiniz.</p> } }</div>
      <nav class="flex items-center justify-between border-t border-slate-200 pt-5 dark:border-slate-800" aria-label="Yazı sayfaları"><button type="button" class="rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:hover:bg-slate-800" (click)="changePage(-1)" [disabled]="page().pageNumber <= 1 || loading()">← Önceki</button><span class="text-sm text-slate-500" aria-live="polite">Sayfa {{ page().pageNumber }} / {{ totalPages() }}</span><button type="button" class="rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:hover:bg-slate-800" (click)="changePage(1)" [disabled]="page().pageNumber >= totalPages() || loading()">Sonraki →</button></nav>
    </section>
  `,
  styles: [`
    .blog-heading { border: 1px solid var(--ui-border); background: linear-gradient(120deg, rgba(238,242,255,.92), var(--ui-surface) 72%); }
    .blog-list-card { display: flex; flex-direction: column; min-height: 15rem; box-shadow: 0 8px 24px rgba(21,34,67,.04); transition: transform .18s ease, border-color .18s ease, box-shadow .18s ease; }
    .blog-list-card:hover { transform: translateY(-2px); border-color: #a5b4fc; box-shadow: 0 14px 28px rgba(21,34,67,.08); }
    .blog-list-card a { margin-top: auto; padding-top: 1rem; }
    @media (prefers-reduced-motion: reduce) { *, *::before, *::after { transition-duration: .01ms !important; } }
  `]
})
export class CoachingPublicBlogComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  readonly heading = signal<CoachingCmsEntry | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly page = signal<CoachingManagementPage<CoachingCmsEntry>>({ items: [], pageNumber: 1, pageSize: 10, totalCount: 0 });

  ngOnInit(): void {
    this.service.getPublicCmsBlocks('BlogLanding').subscribe({
      next: blocks => {
        const heading = blocks[0] ?? null;
        this.heading.set(heading);
        this.title.setTitle(heading?.seoTitle || `${heading?.title || 'Koçluk yazıları'} | Eduİvme Koçluk`);
        if (heading?.seoDescription) this.meta.updateTag({ name: 'description', content: heading.seoDescription });
      },
      error: () => this.heading.set(null)
    });
    this.load(1);
  }
  totalPages(): number { return Math.max(1, Math.ceil(this.page().totalCount / this.page().pageSize)); }
  changePage(delta: number): void { this.load(Math.max(1, Math.min(this.totalPages(), this.page().pageNumber + delta))); }
  isCoachingMediaUrl(value: string | null | undefined): value is string {
    return !!value && /^\/api\/coaching\/cms\/media\/[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value);
  }

  private load(pageNumber: number): void {
    this.loading.set(true); this.error.set(null);
    this.service.getPublicCmsBlog(pageNumber, 10).subscribe({
      next: page => { this.page.set(page); this.loading.set(false); },
      error: () => { this.error.set('Koçluk yazıları yüklenemedi.'); this.loading.set(false); }
    });
  }
}
