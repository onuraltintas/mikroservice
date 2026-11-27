import { CommonModule } from '@angular/common';
import { RevealDirective } from '../../../shared/public-site/reveal.directive';
import { Component, OnInit, inject, signal } from '@angular/core';
import { SiteSeoService } from '../../../core/services/site-seo.service';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { CoachingCmsEntry, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { parseCoachingCmsContent } from '../coaching-cms-content';

@Component({
  selector: 'app-coaching-public-blog-post',
  standalone: true,
  imports: [CommonModule, RouterLink, RevealDirective],
  template: `
    @if (loading()) { <p role="status" class="status">Yazı yükleniyor…</p> }
    @if (error()) {
      <section role="alert" class="sub sub--narrow"><div class="article"><h1>Yazı bulunamadı</h1><p>{{ error() }}</p><a routerLink="/coaching/blog" class="ds-link">Tüm yazılara dön</a></div></section>
    }
    @if (post(); as content) {
      <div class="sub sub--narrow">
        <header class="band-hero">
          <div class="band-hero__glow" aria-hidden="true"><span class="band-hero__blob band-hero__blob--ink"></span><span class="band-hero__blob band-hero__blob--sun"></span></div>
          <nav aria-label="Sayfa yolu" class="crumb"><a routerLink="/coaching/blog">Koçluk kaynakları</a><span aria-hidden="true">/</span><span>Yazı</span></nav>
          <h1>{{ content.title }}</h1>
          @if (content.summary) { <p class="band-hero__lead">{{ content.summary }}</p> }
          <p class="band-hero__meta">{{ content.publishedAt || content.scheduledPublishAt || content.updatedAt || content.createdAt | date:'dd.MM.yyyy' }}@if (content.author) {, {{ content.author }}}</p>
        </header>
        <article class="article" publicReveal>
          @if (isCoachingMediaUrl(content.coverImageUrl)) { <img [src]="content.coverImageUrl" [alt]="content.title" class="article__cover" /> }
          @for (segment of contentSegments(content.content); track $index) {
            @if (segment.type === 'image') { <img [src]="segment.src" [alt]="segment.alt" /> }
            @else if (segment.type === 'heading') { <h2>{{ segment.value }}</h2> }
            @else if (segment.type === 'list') { <ul>@for (item of segment.items; track $index) { <li>{{ item }}</li> }</ul> }
            @else { <p>{{ segment.value }}</p> }
          }
        </article>
        <aside class="call" publicReveal>
          <div><p class="call__title">Bu haftanın planını bugün kur.</p><p>Hesap açmak ücretsiz. Boş saatlerini gir, ilk haftan hazır olsun.</p></div>
          <a routerLink="/auth/register/student" class="ds-btn ds-btn--large call__cta">Ücretsiz hesap aç</a>
        </aside>
        <a routerLink="/coaching/blog" class="ds-link">Diğer yazıları keşfet</a>
      </div>
    }
  `,
  styleUrl: './coaching-public-subpage.scss'
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
