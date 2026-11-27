import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { catchError, map, of, switchMap } from 'rxjs';
import { PlatformLegalPage, PlatformLegalPagesService } from '../../core/services/platform-legal-pages.service';
import { SeoService } from '../../core/services/seo.service';
import { LegalPageSeo } from './legal-page-seo';

@Component({
  selector: 'app-platform-legal-page',
  standalone: true,
  imports: [CommonModule],
  template: `
    <main class="mx-auto min-h-screen max-w-4xl px-4 py-10 sm:px-6" aria-labelledby="legal-page-title">
      <a href="/" class="text-sm font-semibold text-(--ui-brand-text) underline">Hızlı Okuma ana sayfası</a>
      @if (loading()) {
        <p class="mt-8 rounded-xl border border-(--ui-border) p-5 text-sm text-(--ui-text-muted)" role="status">Yasal belge yükleniyor…</p>
      } @else if (page()) {
        <article class="mt-8 rounded-2xl border border-(--ui-border) bg-(--ui-surface) p-6 shadow-sm sm:p-9">
          <h1 id="legal-page-title" class="text-3xl font-bold tracking-tight text-(--ui-text)">{{ page()?.title }}</h1>
          <pre class="mt-8 whitespace-pre-wrap break-words font-sans text-sm leading-7 text-(--ui-text)">{{ page()?.content }}</pre>
        </article>
      } @else if (notFound()) {
        <section class="mt-8 rounded-2xl border border-(--ui-warning-soft) bg-(--ui-warning-soft) p-6" role="status">
          <h1 id="legal-page-title" class="text-xl font-semibold text-(--ui-warning)">Belge henüz yayımlanmadı</h1>
          <p class="mt-2 text-sm leading-6 text-(--ui-warning)">Bu ortak yasal belge henüz yetkili yönetici tarafından yayımlanmamış.</p>
        </section>
      } @else {
        <p class="mt-8 rounded-xl border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-5 text-sm text-(--ui-danger)" role="alert">Yasal belge şu anda yüklenemiyor. Lütfen daha sonra tekrar deneyin.</p>
      }
    </main>
  `,
  styles: [`:host { display: block; min-height: 100%; background: var(--ui-page, var(--ui-surface)); }`]
})
export class PlatformLegalPageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly legalPages = inject(PlatformLegalPagesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly legalSeo = inject(LegalPageSeo);
  private readonly seo = inject(SeoService);
  private restoreSeo: (() => void) | null = null;
  readonly page = signal<PlatformLegalPage | null>(null);
  private slug = '';
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal(false);

  ngOnInit(): void {
    this.route.paramMap.pipe(switchMap(params => {
      const slug = params.get('slug') ?? '';
      this.slug = slug ?? '';
      if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug)) {
        this.page.set(null);
        this.loading.set(false);
        this.notFound.set(true);
        return of(null);
      }

      this.page.set(null);
      this.loading.set(true);
      this.notFound.set(false);
      this.error.set(false);
      return this.legalPages.getPage(slug).pipe(
        map(page => ({ page, notFound: false, error: false })),
        catchError((response: HttpErrorResponse) => of({ page: null, notFound: response.status === 404, error: response.status !== 404 }))
      );
    }), takeUntilDestroyed(this.destroyRef)).subscribe(result => {
      if (!result) return;
      if (result.page?.isPublished && result.page.content.trim()) this.page.set(result.page);
      else this.notFound.set(result.notFound || !result.page);
      this.error.set(result.error);
      this.loading.set(false);
      this.applySeo(this.slug);
    });
  }

  ngOnDestroy(): void {
    this.restoreSeo?.();
    this.seo.setNoIndex(false);
  }

  // Each shared legal document gets its own title, description and canonical address (27.11.2026);
  // an unpublished one stays out of search results.
  private applySeo(slug: string): void {
    this.restoreSeo?.();
    this.restoreSeo = null;
    const page = this.page();
    if (!page) {
      this.seo.setNoIndex(true);
      return;
    }
    this.seo.setNoIndex(false);
    this.restoreSeo = this.legalSeo.apply({ path: `/legal/${slug}`, heading: page.title.trim(), description: summary(page.content) });
  }
}

/** First sentences of a document, for its description: plain text, at most 155 characters. */
function summary(content: string): string {
  const text = content.replace(/[#*_>`]/g, ' ').replace(/\s+/g, ' ').trim();
  if (text.length <= 155) return text;
  const cut = text.slice(0, 155);
  const space = cut.lastIndexOf(' ');
  return `${cut.slice(0, space > 100 ? space : 155)}…`;
}
