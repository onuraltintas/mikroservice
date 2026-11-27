import { CommonModule } from '@angular/common';
import { RevealDirective } from '../../../shared/public-site/reveal.directive';
import { Component, OnInit, inject, signal } from '@angular/core';
import { SiteSeoService } from '../../../core/services/site-seo.service';
import { CoachingCmsEntry, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { parseCoachingCmsContent } from '../coaching-cms-content';

@Component({
  selector: 'app-coaching-public-faq',
  standalone: true,
  imports: [CommonModule, RevealDirective],
  template: `
    <div class="sub sub--narrow">
      <header class="band-hero" aria-labelledby="coaching-faq-title">
        <div class="band-hero__glow" aria-hidden="true"><span class="band-hero__blob band-hero__blob--ink"></span><span class="band-hero__blob band-hero__blob--sun"></span></div>
        <p class="eyebrow">{{ heading()?.eyebrow || 'Koçluk desteği' }}</p>
        <h1 id="coaching-faq-title">{{ heading()?.title || 'Sıkça sorulan sorular' }}</h1>
        @if (heading()?.summary) { <p class="band-hero__lead">{{ heading()?.summary }}</p> }
      </header>
      @if (loading()) { <p role="status" class="status">Sorular yükleniyor…</p> }
      @if (error()) { <p role="alert" class="ds-alert ds-alert--warning">{{ error() }}</p> }
      @if (entries().length) {
        <div class="faq" publicReveal>
          @for (item of entries(); track item.id) {
            <details>
              <summary>{{ item.title }}</summary>
              <div class="faq__answer">
                @for (segment of contentSegments(item.content); track $index) {
                  @if (segment.type === 'heading') { <h2>{{ segment.value }}</h2> }
                  @else if (segment.type === 'list') { <ul>@for (part of segment.items; track $index) { <li>{{ part }}</li> }</ul> }
                  @else if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" /> }
                  @else { <p>{{ segment.value }}</p> }
                }
              </div>
            </details>
          }
        </div>
      } @else if (!loading() && !error()) { <p class="empty">Henüz yayınlanmış bir soru yok.</p> }
    </div>
  `,
  styleUrl: './coaching-public-subpage.scss'
})
export class CoachingPublicFaqComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  private readonly seo = inject(SiteSeoService);
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
        this.applySeo();
      },
      error: () => this.heading.set(null)
    });
    this.service.getPublicCmsBlocks('HomeFaq').subscribe({
      next: entries => { this.entries.set(entries); this.loading.set(false); this.applySeo(); },
      error: () => { this.error.set('Sıkça sorulan sorular yüklenemedi.'); this.loading.set(false); }
    });
  }

  /** Title, description and FAQPage structured data from the heading and the published questions. */
  private applySeo(): void {
    const heading = this.heading();
    const questions = this.entries().slice(0, 50).map(item => ({
      '@type': 'Question', name: item.title,
      acceptedAnswer: { '@type': 'Answer', text: item.content.replace(/!\[[^\]]*\]\([^)]*\)/g, '').replace(/[#*_>`]/g, '').trim().slice(0, 1000) }
    }));
    this.seo.apply({
      title: heading?.seoTitle || `${heading?.title || 'Sıkça sorulan sorular'} | maxkoç`,
      description: heading?.seoDescription || heading?.summary || 'maxkoç hakkında sıkça sorulan sorular.',
      path: '/coaching/faq',
      jsonLd: questions.length ? { '@context': 'https://schema.org', '@type': 'FAQPage', mainEntity: questions } : null
    });
  }
}
