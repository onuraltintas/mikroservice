import { CommonModule } from '@angular/common';
import { RevealDirective } from '../../../shared/public-site/reveal.directive';
import { Component, OnInit, inject, signal } from '@angular/core';
import { SiteSeoService } from '../../../core/services/site-seo.service';
import { RouterLink } from '@angular/router';
import { CoachingCmsEntry, CoachingManagementPage, CoachingManagementService } from '../../../core/services/coaching-management.service';

@Component({
  selector: 'app-coaching-public-blog',
  standalone: true,
  imports: [CommonModule, RouterLink, RevealDirective],
  template: `
    <div class="sub">
      <header class="band-hero" aria-labelledby="coaching-blog-title">
        <div class="band-hero__glow" aria-hidden="true"><span class="band-hero__blob band-hero__blob--ink"></span><span class="band-hero__blob band-hero__blob--sun"></span></div>
        <p class="eyebrow">{{ heading()?.eyebrow || 'Koçluk kaynakları' }}</p>
        <h1 id="coaching-blog-title">{{ heading()?.title || 'Öğrenme yolculuğuna eşlik eden yazılar' }}</h1>
        <p class="band-hero__lead">{{ heading()?.summary || 'Hedef belirleme, çalışma planı ve koçluk görüşmeleri için uygulamaya dönük kısa rehberler.' }}</p>
      </header>
      @if (loading()) { <p role="status" class="status">Yazılar yükleniyor…</p> }
      @if (error()) { <div role="alert" class="ds-alert ds-alert--warning">{{ error() }}</div> }
      <div class="cards">
        @for (post of page().items; track post.id; let index = $index) {
          <article class="card public-lift" [attr.data-tone]="tone(index)" [publicReveal]="index % 2">
            @if (isCoachingMediaUrl(post.coverImageUrl)) { <img [src]="post.coverImageUrl" [alt]="post.title" class="card__image" /> }
            <p class="card__meta">{{ post.publishedAt || post.scheduledPublishAt || post.updatedAt || post.createdAt | date:'dd MMMM yyyy' }}@if (post.author) {<span>, {{ post.author }}</span>}</p>
            <h2><a [routerLink]="['/coaching/blog', post.slug]">{{ post.title }}</a></h2>
            <p class="card__lead">{{ post.summary || post.content.slice(0, 400) }}</p>
            <span class="card__more" aria-hidden="true">Yazıyı oku</span>
          </article>
        } @empty {
          @if (!loading()) { <p class="empty">Henüz yayınlanmış yazı yok. Yeni yazılar eklendiğinde burada görebilirsiniz.</p> }
        }
      </div>
      @if (totalPages() > 1) {
        <nav class="pager" aria-label="Yazı sayfaları">
          <button type="button" class="ds-btn ds-btn--secondary" (click)="changePage(-1)" [disabled]="page().pageNumber <= 1 || loading()">Önceki sayfa</button>
          <span aria-live="polite">Sayfa {{ page().pageNumber }} / {{ totalPages() }}</span>
          <button type="button" class="ds-btn ds-btn--secondary" (click)="changePage(1)" [disabled]="page().pageNumber >= totalPages() || loading()">Sonraki sayfa</button>
        </nav>
      }
    </div>
  `,
  styleUrl: './coaching-public-subpage.scss'
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
  tone(index: number): string { return ['math', 'turkish', 'science', 'history'][index % 4]; }
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
