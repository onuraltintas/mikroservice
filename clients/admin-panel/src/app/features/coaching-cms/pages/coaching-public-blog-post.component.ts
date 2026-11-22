import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { SiteSeoService } from '../../../core/services/site-seo.service';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { CoachingCmsEntry, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { parseCoachingCmsContent } from '../coaching-cms-content';

@Component({
  selector: 'app-coaching-public-blog-post',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    @if (loading()) { <p role="status" class="text-center text-sm text-[var(--ds-muted)]">Yazı yükleniyor…</p> }
    @if (error()) { <section role="alert" class="mx-auto max-w-3xl rounded-2xl border border-[var(--ds-line)] bg-[var(--ds-surface)] p-8"><h1 class="text-2xl font-bold">Yazı bulunamadı</h1><p class="mt-2 text-sm text-[var(--ds-muted)]">{{ error() }}</p><a routerLink="/coaching/blog" class="mt-4 inline-flex text-sm font-semibold text-[var(--ds-link)]">Tüm yazılara dön</a></section> }
    @if (post(); as content) { <article class="mx-auto max-w-3xl rounded-[1.5rem] border border-[var(--ds-line)] bg-[var(--ds-surface)] p-6 shadow-sm sm:p-10"><nav aria-label="Sayfa yolu" class="text-sm"><a routerLink="/coaching/blog" class="font-semibold text-[var(--ds-link)] hover:underline">Koçluk kaynakları</a><span class="mx-2 text-[var(--ds-muted)]" aria-hidden="true">/</span><span class="text-[var(--ds-muted)]">Yazı</span></nav><p class="mt-6 text-xs font-medium text-[var(--ds-muted)]">{{ content.publishedAt || content.scheduledPublishAt || content.updatedAt || content.createdAt | date:'dd.MM.yyyy' }} @if (content.author) {<span>, {{ content.author }}</span>}, {{ content.viewCount }} görüntülenme</p><h1 class="mt-2 text-3xl font-bold tracking-tight sm:text-4xl">{{ content.title }}</h1>@if (content.summary) {<p class="mt-4 text-lg leading-7 text-[var(--ds-muted)]">{{ content.summary }}</p>}@if (isCoachingMediaUrl(content.coverImageUrl)) {<img [src]="content.coverImageUrl" [alt]="content.title" class="mt-7 max-h-[32rem] w-full rounded-2xl object-cover" />}<div class="cms-article mt-8 space-y-5 text-base leading-8 text-[var(--ds-text)]">@for (segment of contentSegments(content.content); track $index) {@if (segment.type === 'image') {<img [src]="segment.src" [alt]="segment.alt" class="max-h-[32rem] rounded-2xl object-cover" />} @else if (segment.type === 'heading') {<h2 class="pt-3 text-2xl font-semibold tracking-tight text-(--ui-text)">{{ segment.value }}</h2>} @else if (segment.type === 'list') {<ul class="list-disc space-y-2 pl-6">@for (item of segment.items; track $index) {<li>{{ item }}</li>}</ul>} @else {<p class="whitespace-pre-wrap">{{ segment.value }}</p>}}</div><footer class="mt-10 border-t border-[var(--ds-line)] pt-6"><a routerLink="/coaching/blog" class="text-sm font-semibold text-[var(--ds-link)]">← Diğer yazıları keşfet</a></footer></article> }
  `
})
export class CoachingPublicBlogPostComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SiteSeoService);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly post = signal<CoachingCmsEntry | null>(null);
  readonly contentSegments = parseCoachingCmsContent;

  isCoachingMediaUrl(value: string | null | undefined): value is string {
    return !!value && /^\/api\/coaching\/cms\/media\/[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value);
  }

  ngOnInit(): void {
    this.route.paramMap.pipe(switchMap(params => this.service.getPublicCmsBlogPost(params.get('slug') ?? ''))).subscribe({
      next: post => {
        this.post.set(post); this.error.set(null); this.loading.set(false);
        const published = post.publishedAt || post.scheduledPublishAt || post.createdAt;
        const image = this.isCoachingMediaUrl(post.coverImageUrl) ? post.coverImageUrl : null;
        this.seo.apply({ title: post.seoTitle || `${post.title} | maxkoç`, description: post.seoDescription || post.summary,
          path: `/coaching/blog/${post.slug}`, image, type: 'article', jsonLd: {
            '@context': 'https://schema.org', '@type': 'BlogPosting', headline: post.title, description: post.seoDescription || post.summary || undefined,
            datePublished: published, dateModified: post.updatedAt || published, inLanguage: 'tr-TR',
            author: post.author ? { '@type': 'Person', name: post.author } : { '@type': 'Organization', name: 'eduivme' },
            publisher: { '@type': 'Organization', name: 'eduivme' }, mainEntityOfPage: this.seo.absolute(`/coaching/blog/${post.slug}`),
            ...(image ? { image: this.seo.absolute(image) } : {}) } });
      },
      error: () => { this.post.set(null); this.error.set('Yayınlanan Koçluk yazısı bulunamadı.'); this.loading.set(false); }
    });
  }
}
