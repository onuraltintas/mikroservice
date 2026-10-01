import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { CoachingCmsEntry, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { parseCoachingCmsContent } from '../coaching-cms-content';

@Component({
  selector: 'app-coaching-public-faq',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="mx-auto max-w-4xl space-y-7" aria-labelledby="coaching-faq-title">
      <header class="faq-heading rounded-[1.75rem] px-6 py-8 sm:px-9 sm:py-10"><p class="text-xs font-bold uppercase tracking-[.14em] text-indigo-700 dark:text-indigo-300">{{ heading()?.eyebrow || 'Koçluk desteği' }}</p><h1 id="coaching-faq-title" class="mt-3 text-3xl font-semibold tracking-tight sm:text-4xl">{{ heading()?.title || 'Sıkça sorulan sorular' }}</h1>@if (heading()?.summary) {<p class="mt-3 max-w-2xl text-sm leading-6 text-slate-600 dark:text-slate-300">{{ heading()?.summary }}</p>}</header>
      @if (loading()) { <p role="status" class="text-center text-sm text-slate-500">Sorular yükleniyor…</p> }
      @if (error()) { <p role="alert" class="rounded-xl bg-red-50 p-4 text-sm text-red-700">{{ error() }}</p> }
      <div class="divide-y divide-slate-200 rounded-2xl border border-slate-200 bg-white px-5 dark:divide-slate-700 dark:border-slate-800 dark:bg-slate-900">
        @for (item of entries(); track item.id) {<details class="py-5"><summary class="cursor-pointer font-semibold">{{ item.title }}</summary><div class="cms-article mt-3 space-y-3 text-sm leading-7 text-slate-600 dark:text-slate-300">@for (segment of contentSegments(item.content); track $index) { @if (segment.type === 'heading') { <h2 class="font-semibold text-slate-900 dark:text-white">{{ segment.value }}</h2> } @else if (segment.type === 'list') { <ul class="list-disc space-y-1 pl-5">@for (part of segment.items; track $index) { <li>{{ part }}</li> }</ul> } @else if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" class="max-h-72 rounded-xl object-cover" /> } @else { <p class="whitespace-pre-wrap">{{ segment.value }}</p> } }</div></details>} @empty { @if (!loading() && !error()) { <p class="py-8 text-sm text-slate-500">Henüz yayınlanmış bir soru yok.</p> } }
      </div>
    </section>
  `,
  styles: [`
    .faq-heading { border: 1px solid var(--ui-border); background: linear-gradient(120deg, rgba(238,242,255,.92), var(--ui-surface) 72%); }
  `]
})
export class CoachingPublicFaqComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  readonly heading = signal<CoachingCmsEntry | null>(null);
  readonly entries = signal<CoachingCmsEntry[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly contentSegments = parseCoachingCmsContent;

  ngOnInit(): void {
    this.service.getPublicCmsBlocks('HomeFaqHeading').subscribe({
      next: blocks => {
        const heading = blocks[0] ?? null;
        this.heading.set(heading);
        this.title.setTitle(heading?.seoTitle || `${heading?.title || 'Sıkça sorulan sorular'} | Eduİvme Koçluk`);
        if (heading?.seoDescription) this.meta.updateTag({ name: 'description', content: heading.seoDescription });
      },
      error: () => this.heading.set(null)
    });
    this.service.getPublicCmsBlocks('HomeFaq').subscribe({
      next: entries => { this.entries.set(entries); this.loading.set(false); },
      error: () => { this.error.set('Sıkça sorulan sorular yüklenemedi.'); this.loading.set(false); }
    });
  }
}
