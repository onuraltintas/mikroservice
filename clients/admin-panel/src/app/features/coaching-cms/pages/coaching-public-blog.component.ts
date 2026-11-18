import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { SiteSeoService } from '../../../core/services/site-seo.service';
import { RouterLink } from '@angular/router';
import { CoachingCmsEntry, CoachingManagementPage, CoachingManagementService } from '../../../core/services/coaching-management.service';

@Component({
  selector: 'app-coaching-public-blog',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <section class="mx-auto max-w-6xl space-y-7" aria-labelledby="coaching-blog-title">
      <header class="blog-heading rounded-[1.75rem] px-6 py-8 sm:px-9 sm:py-10"><p class="text-sm font-semibold text-[var(--ds-link)]">{{ heading()?.eyebrow || 'Koçluk kaynakları' }}</p><h1 id="coaching-blog-title" class="mt-3 text-3xl font-semibold tracking-tight sm:text-4xl">{{ heading()?.title || 'Öğrenme yolculuğuna eşlik eden yazılar' }}</h1><p class="mt-3 max-w-2xl text-sm leading-6 text-[var(--ds-muted)]">{{ heading()?.summary || 'Hedef belirleme, çalışma planı ve koçluk görüşmeleri için uygulamaya dönük kısa rehberler.' }}</p></header>
      @if (loading()) { <p role="status" class="text-center text-sm text-[var(--ds-muted)]">Yazılar yükleniyor…</p> }
      @if (error()) { <div role="alert" class="rounded-xl bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div> }
      <div class="grid gap-4 md:grid-cols-2">@for (post of page().items; track post.id) {<article class="blog-list-card rounded-2xl border border-[var(--ds-line)] bg-[var(--ds-surface)] p-6">@if (isCoachingMediaUrl(post.coverImageUrl)) {<img [src]="post.coverImageUrl" [alt]="post.title" class="mb-4 h-48 w-full rounded-xl object-cover" />}<p class="text-xs font-medium text-[var(--ds-muted)]">{{ post.publishedAt || post.scheduledPublishAt || post.updatedAt || post.createdAt | date:'dd MMMM yyyy' }} @if (post.author) {<span> · {{ post.author }}</span>}</p><h2 class="mt-3 text-xl font-semibold leading-7 tracking-tight">{{ post.title }}</h2><p class="mt-3 line-clamp-4 whitespace-pre-wrap text-sm leading-6 text-[var(--ds-muted)]">{{ post.summary || post.content.slice(0, 400) }}</p><a [routerLink]="['/coaching/blog', post.slug]" class="mt-5 inline-flex items-center gap-1 text-sm font-semibold text-[var(--ds-link)]">Yazıyı oku <span aria-hidden="true">→</span></a></article>} @empty { @if (!loading()) { <p class="col-span-full rounded-2xl border border-dashed border-[var(--ds-field)] p-10 text-center text-sm text-[var(--ds-muted)]">Henüz yayınlanmış yazı yok. Yeni yazılar eklendiğinde burada görebilirsiniz.</p> } }</div>
      <nav class="flex items-center justify-between border-t border-[var(--ds-line)] pt-5" aria-label="Yazı sayfaları"><button type="button" class="rounded-lg border border-[var(--ds-field)] px-4 py-2 text-sm font-medium transition hover:bg-[var(--ds-bg)] disabled:cursor-not-allowed disabled:opacity-50" (click)="changePage(-1)" [disabled]="page().pageNumber <= 1 || loading()">← Önceki</button><span class="text-sm text-[var(--ds-muted)]" aria-live="polite">Sayfa {{ page().pageNumber }} / {{ totalPages() }}</span><button type="button" class="rounded-lg border border-[var(--ds-field)] px-4 py-2 text-sm font-medium transition hover:bg-[var(--ds-bg)] disabled:cursor-not-allowed disabled:opacity-50" (click)="changePage(1)" [disabled]="page().pageNumber >= totalPages() || loading()">Sonraki →</button></nav>
    </section>
  `,
  styles: [`
    .blog-heading { border: 1px solid var(--ds-line); background: var(--ds-surface); }
    .blog-list-card { display: flex; flex-direction: column; min-height: 15rem; transition: border-color .15s ease; }
    .blog-list-card:hover { border-color: var(--ds-primary); }
    .blog-list-card a { margin-top: auto; padding-top: 1rem; }
    @media (prefers-reduced-motion: reduce) { *, *::before, *::after { transition-duration: .01ms !important; } }
  `]
})
export class CoachingPublicBlogComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  private readonly seo = inject(SiteSeoService);
  readonly heading = signal<CoachingCmsEntry | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly page = signal<CoachingManagementPage<CoachingCmsEntry>>({ items: [], pageNumber: 1, pageSize: 10, totalCount: 0 });

  ngOnInit(): void {
    this.service.getPublicCmsBlocks('BlogLanding').subscribe({
      next: blocks => {
        const heading = blocks[0] ?? null;
        this.heading.set(heading);
        this.seo.apply({ title: heading?.seoTitle || `${heading?.title || 'Koçluk yazıları'} | maxkoç`,
          description: heading?.seoDescription || heading?.summary || 'LGS, YKS ve planlı çalışma üzerine koçluk yazıları.', path: '/coaching/blog' });
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
