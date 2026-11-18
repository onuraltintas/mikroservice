import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { catchError, map, of, switchMap } from 'rxjs';
import { PlatformLegalPage, PlatformLegalPagesService } from '../../core/services/platform-legal-pages.service';

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
export class PlatformLegalPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly legalPages = inject(PlatformLegalPagesService);
  private readonly destroyRef = inject(DestroyRef);
  readonly page = signal<PlatformLegalPage | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal(false);

  ngOnInit(): void {
    this.route.paramMap.pipe(switchMap(params => {
      const slug = params.get('slug') ?? '';
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
    });
  }
}
