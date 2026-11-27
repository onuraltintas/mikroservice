import { CommonModule } from '@angular/common';
import { RevealDirective } from '../../../shared/public-site/reveal.directive';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SiteSeoService } from '../../../core/services/site-seo.service';
import { switchMap } from 'rxjs';
import { CoachingCmsEntry, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { parseCoachingCmsContent } from '../coaching-cms-content';

@Component({
  selector: 'app-coaching-public-page',
  standalone: true,
  imports: [CommonModule, RouterLink, RevealDirective],
  template: `
    @if (loading()) { <p role="status" class="status">Sayfa yükleniyor…</p> }
    @if (error()) {
      <section role="alert" class="sub sub--narrow"><div class="article"><h1>İçerik bulunamadı</h1><p>{{ error() }}</p><a routerLink="/coaching" class="ds-link">Koçluk ana sayfasına dön</a></div></section>
    }
    @if (page(); as content) {
      <div class="sub sub--narrow">
        <header class="band-hero">
          <div class="band-hero__glow" aria-hidden="true"><span class="band-hero__blob band-hero__blob--ink"></span><span class="band-hero__blob band-hero__blob--sun"></span></div>
          <nav aria-label="Sayfa yolu" class="crumb"><a routerLink="/coaching">maxkoç</a><span aria-hidden="true">/</span><span>{{ content.title }}</span></nav>
          <h1>{{ content.title }}</h1>
          @if (content.summary) { <p class="band-hero__lead">{{ content.summary }}</p> }
        </header>
        <article class="article" publicReveal>
          @for (segment of contentSegments(content.content); track $index) {
            @if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" /> }
            @else if (segment.type === 'heading') { <h2>{{ segment.value }}</h2> }
            @else if (segment.type === 'list') { <ul>@for (item of segment.items; track $index) { <li>{{ item }}</li> }</ul> }
            @else { <p>{{ segment.value }}</p> }
          }
        </article>
        <aside class="call" publicReveal>
          <div><p class="call__title">Koçluk alanına geçmeye hazır mısınız?</p><p>Hedef ve çalışma adımlarınızı hesabınızda takip edin.</p></div>
          <a routerLink="/auth/login" [queryParams]="{ returnUrl: '/coaching-portal' }" class="ds-btn ds-btn--large call__cta">Giriş yap veya kayıt ol</a>
        </aside>
      </div>
    }
  `,
  styleUrl: './coaching-public-subpage.scss'
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
