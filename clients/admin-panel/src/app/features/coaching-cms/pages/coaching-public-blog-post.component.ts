import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { CoachingCmsEntry, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { parseCoachingCmsContent } from '../coaching-cms-content';

@Component({
  selector: 'app-coaching-public-blog-post',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    @if (loading()) { <p role="status" class="text-center text-sm text-slate-500">Yazı yükleniyor…</p> }
    @if (error()) { <section role="alert" class="mx-auto max-w-3xl rounded-2xl border border-slate-200 bg-white p-8 dark:border-slate-800 dark:bg-slate-900"><h1 class="text-2xl font-bold">Yazı bulunamadı</h1><p class="mt-2 text-sm text-slate-600 dark:text-slate-300">{{ error() }}</p><a routerLink="/coaching/blog" class="mt-4 inline-flex text-sm font-semibold text-indigo-600">Tüm yazılara dön</a></section> }
    @if (post(); as content) { <article class="mx-auto max-w-3xl rounded-[1.5rem] border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900 sm:p-10"><nav aria-label="Sayfa yolu" class="text-sm"><a routerLink="/coaching/blog" class="font-semibold text-indigo-700 hover:underline dark:text-indigo-300">Koçluk kaynakları</a><span class="mx-2 text-slate-400" aria-hidden="true">/</span><span class="text-slate-500">Yazı</span></nav><p class="mt-6 text-xs font-medium uppercase tracking-wide text-slate-500">{{ content.publishedAt || content.scheduledPublishAt || content.updatedAt || content.createdAt | date:'dd.MM.yyyy' }} @if (content.author) {<span> · {{ content.author }}</span>} · {{ content.viewCount }} görüntülenme</p><h1 class="mt-2 text-3xl font-bold tracking-tight sm:text-4xl">{{ content.title }}</h1>@if (content.summary) {<p class="mt-4 text-lg leading-7 text-slate-600 dark:text-slate-300">{{ content.summary }}</p>}@if (isCoachingMediaUrl(content.coverImageUrl)) {<img [src]="content.coverImageUrl" [alt]="content.title" class="mt-7 max-h-[32rem] w-full rounded-2xl object-cover" />}<div class="cms-article mt-8 space-y-5 text-base leading-8 text-slate-700 dark:text-slate-200">@for (segment of contentSegments(content.content); track $index) {@if (segment.type === 'image') {<img [src]="segment.src" [alt]="segment.alt" class="max-h-[32rem] rounded-2xl object-cover" />} @else if (segment.type === 'heading') {<h2 class="pt-3 text-2xl font-semibold tracking-tight text-slate-950 dark:text-white">{{ segment.value }}</h2>} @else if (segment.type === 'list') {<ul class="list-disc space-y-2 pl-6">@for (item of segment.items; track $index) {<li>{{ item }}</li>}</ul>} @else {<p class="whitespace-pre-wrap">{{ segment.value }}</p>}}</div><footer class="mt-10 border-t border-slate-200 pt-6 dark:border-slate-700"><a routerLink="/coaching/blog" class="text-sm font-semibold text-indigo-700 dark:text-indigo-300">← Diğer yazıları keşfet</a></footer></article> }
  `
})
export class CoachingPublicBlogPostComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  private readonly route = inject(ActivatedRoute);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
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
        this.title.setTitle(post.seoTitle || `${post.title} | Eduİvme Koçluk`);
        if (post.seoDescription) this.meta.updateTag({ name: 'description', content: post.seoDescription });
      },
      error: () => { this.post.set(null); this.error.set('Yayınlanan Koçluk yazısı bulunamadı.'); this.loading.set(false); }
    });
  }
}
