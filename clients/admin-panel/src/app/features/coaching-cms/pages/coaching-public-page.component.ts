import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { switchMap } from 'rxjs';
import { CoachingCmsEntry, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { parseCoachingCmsContent } from '../coaching-cms-content';

@Component({
  selector: 'app-coaching-public-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    @if (loading()) { <p role="status" class="text-center text-sm text-slate-500">Sayfa yükleniyor…</p> }
    @if (error()) { <section role="alert" class="rounded-2xl border border-slate-200 bg-white p-8 dark:border-slate-800 dark:bg-slate-900"><h1 class="text-2xl font-bold">İçerik bulunamadı</h1><p class="mt-2 text-sm text-slate-600 dark:text-slate-300">{{ error() }}</p><a routerLink="/coaching" class="mt-4 inline-flex text-sm font-semibold text-indigo-600">Koçluk ana sayfasına dön</a></section> }
    @if (page(); as content) { <article class="mx-auto max-w-3xl rounded-[1.5rem] border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900 sm:p-10"><nav aria-label="Sayfa yolu" class="text-sm"><a routerLink="/coaching" class="font-medium text-indigo-700 hover:underline dark:text-indigo-300">Koçluk</a><span class="mx-2 text-slate-400" aria-hidden="true">/</span><span class="text-slate-500">{{ content.title }}</span></nav><h1 class="mt-5 text-3xl font-bold tracking-tight sm:text-4xl">{{ content.title }}</h1>@if (content.summary) {<p class="mt-4 text-lg leading-7 text-slate-600 dark:text-slate-300">{{ content.summary }}</p>}<div class="cms-article mt-8 space-y-5 text-base leading-8 text-slate-700 dark:text-slate-200">@for (segment of contentSegments(content.content); track $index) {@if (segment.type === 'image') {<img [src]="segment.src" [alt]="segment.alt" class="max-h-[32rem] rounded-2xl object-cover" />} @else if (segment.type === 'heading') {<h2 class="pt-3 text-2xl font-semibold tracking-tight text-slate-950 dark:text-white">{{ segment.value }}</h2>} @else if (segment.type === 'list') {<ul class="list-disc space-y-2 pl-6">@for (item of segment.items; track $index) {<li>{{ item }}</li>}</ul>} @else {<p class="whitespace-pre-wrap">{{ segment.value }}</p>}}</div><div class="mt-10 rounded-2xl bg-indigo-50 p-5 dark:bg-indigo-950/40"><p class="font-semibold text-slate-900 dark:text-white">Koçluk alanına geçmeye hazır mısınız?</p><p class="mt-1 text-sm text-slate-600 dark:text-slate-300">Hedef ve çalışma adımlarınızı hesabınızda takip edin.</p><a routerLink="/auth/login" [queryParams]="{ returnUrl: '/coaching-portal' }" class="mt-4 inline-flex rounded-lg bg-indigo-700 px-4 py-2.5 text-sm font-semibold text-white">Giriş yap veya kayıt ol</a></div></article> }
  `
})
export class CoachingPublicPageComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  private readonly route = inject(ActivatedRoute);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly page = signal<CoachingCmsEntry | null>(null);
  readonly contentSegments = parseCoachingCmsContent;

  ngOnInit(): void {
    this.route.paramMap.pipe(switchMap(params => this.service.getPublicCmsPage(params.get('slug') ?? ''))).subscribe({
      next: page => {
        this.page.set(page); this.error.set(null); this.loading.set(false);
        this.title.setTitle(page.seoTitle || `${page.title} | Eduİvme Koçluk`);
        if (page.seoDescription) this.meta.updateTag({ name: 'description', content: page.seoDescription });
      },
      error: () => { this.page.set(null); this.error.set('Yayınlanan Koçluk sayfası bulunamadı.'); this.loading.set(false); }
    });
  }
}
