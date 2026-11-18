import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SiteSeoService } from '../../../core/services/site-seo.service';
import { switchMap } from 'rxjs';
import { CoachingCmsEntry, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { parseCoachingCmsContent } from '../coaching-cms-content';

@Component({
  selector: 'app-coaching-public-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    @if (loading()) { <p role="status" class="text-center text-sm text-[var(--ds-muted)]">Sayfa yükleniyor…</p> }
    @if (error()) { <section role="alert" class="rounded-2xl border border-[var(--ds-line)] bg-[var(--ds-surface)] p-8"><h1 class="text-2xl font-bold">İçerik bulunamadı</h1><p class="mt-2 text-sm text-[var(--ds-muted)]">{{ error() }}</p><a routerLink="/coaching" class="mt-4 inline-flex text-sm font-semibold text-[var(--ds-link)]">Koçluk ana sayfasına dön</a></section> }
    @if (page(); as content) { <article class="mx-auto max-w-3xl rounded-[1.5rem] border border-[var(--ds-line)] bg-[var(--ds-surface)] p-6 shadow-sm sm:p-10"><nav aria-label="Sayfa yolu" class="text-sm"><a routerLink="/coaching" class="font-medium text-[var(--ds-link)] hover:underline">Koçluk</a><span class="mx-2 text-[var(--ds-muted)]" aria-hidden="true">/</span><span class="text-[var(--ds-muted)]">{{ content.title }}</span></nav><h1 class="mt-5 text-3xl font-bold tracking-tight sm:text-4xl">{{ content.title }}</h1>@if (content.summary) {<p class="mt-4 text-lg leading-7 text-[var(--ds-muted)]">{{ content.summary }}</p>}<div class="cms-article mt-8 space-y-5 text-base leading-8 text-[var(--ds-text)]">@for (segment of contentSegments(content.content); track $index) {@if (segment.type === 'image') {<img [src]="segment.src" [alt]="segment.alt" class="max-h-[32rem] rounded-2xl object-cover" />} @else if (segment.type === 'heading') {<h2 class="pt-3 text-2xl font-semibold tracking-tight text-slate-950">{{ segment.value }}</h2>} @else if (segment.type === 'list') {<ul class="list-disc space-y-2 pl-6">@for (item of segment.items; track $index) {<li>{{ item }}</li>}</ul>} @else {<p class="whitespace-pre-wrap">{{ segment.value }}</p>}}</div><div class="mt-10 rounded-2xl bg-indigo-50 p-5"><p class="font-semibold text-[var(--ds-text)]">Koçluk alanına geçmeye hazır mısınız?</p><p class="mt-1 text-sm text-[var(--ds-muted)]">Hedef ve çalışma adımlarınızı hesabınızda takip edin.</p><a routerLink="/auth/login" [queryParams]="{ returnUrl: '/coaching-portal' }" class="mt-4 inline-flex rounded-lg bg-[var(--ds-primary)] px-4 py-2.5 text-sm font-semibold text-[var(--ds-on-primary)]">Giriş yap veya kayıt ol</a></div></article> }
  `
})
export class CoachingPublicPageComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SiteSeoService);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly page = signal<CoachingCmsEntry | null>(null);
  readonly contentSegments = parseCoachingCmsContent;

  ngOnInit(): void {
    this.route.paramMap.pipe(switchMap(params => this.service.getPublicCmsPage(params.get('slug') ?? ''))).subscribe({
      next: page => {
        this.page.set(page); this.error.set(null); this.loading.set(false);
        this.seo.apply({ title: page.seoTitle || `${page.title} | maxkoç`, description: page.seoDescription || page.summary,
          path: `/coaching/pages/${page.slug}`, jsonLd: { '@context': 'https://schema.org', '@type': 'WebPage', name: page.title,
            description: page.seoDescription || page.summary || undefined, inLanguage: 'tr-TR' } });
      },
      error: () => { this.page.set(null); this.error.set('Yayınlanan Koçluk sayfası bulunamadı.'); this.loading.set(false); }
    });
  }
}
