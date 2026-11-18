import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { IdentityService, PlatformLegalPageDto } from '../../core/services/identity.service';
import { catchError, map, of, switchMap } from 'rxjs';
import { injectSiteBrand } from '../../core/brand/site-brand';

@Component({
  selector: 'app-platform-legal-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <main class="mx-auto min-h-screen max-w-4xl px-4 py-10 sm:px-6" aria-labelledby="legal-page-title">
      <header class="mb-8 flex flex-wrap items-center justify-between gap-4 border-b border-(--ui-border) pb-5">
        <a routerLink="/coaching" class="text-lg font-bold text-(--ui-brand-text)">{{ brand.name }}</a>
        <nav class="flex gap-4 text-sm" aria-label="Yasal sayfalar"><a routerLink="/legal/privacy" class="text-(--ui-brand-text) underline-offset-4 hover:underline">Gizlilik</a><a routerLink="/legal/kvkk" class="text-(--ui-brand-text) underline-offset-4 hover:underline">KVKK</a></nav>
      </header>

      @if (loading()) {
        <p class="rounded-xl border border-(--ui-border) p-5 text-sm text-(--ui-text-muted)" role="status">Yasal belge yükleniyor…</p>
      } @else if (page()) {
        <article class="rounded-2xl border border-(--ui-border) bg-(--ui-surface) p-6 shadow-sm sm:p-9">
          <h1 id="legal-page-title" class="text-3xl font-bold tracking-tight text-(--ui-text)">{{ page()?.title }}</h1>
          <pre class="mt-8 whitespace-pre-wrap break-words font-sans text-sm leading-7 text-(--ui-text)">{{ page()?.content }}</pre>
        </article>
      } @else if (notFound()) {
        <section class="rounded-2xl border border-(--ui-warning-soft) bg-(--ui-warning-soft) p-6" role="status">
          <h1 id="legal-page-title" class="text-xl font-semibold text-(--ui-warning)">Belge henüz yayımlanmadı</h1>
          <p class="mt-2 text-sm leading-6 text-(--ui-warning)">Bu ortak yasal belge henüz yetkili yönetici tarafından yayımlanmamış. Yayımlandığında Koçluk ve Hızlı Okuma ürünlerinde aynı güncel metin gösterilecektir.</p>
        </section>
      } @else {
        <p class="rounded-xl border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-5 text-sm text-(--ui-danger)" role="alert">Yasal belge şu anda yüklenemiyor. Lütfen daha sonra tekrar deneyin.</p>
      }
    </main>
  `,
  styles: [`:host { display: block; min-height: 100%; background: var(--ui-page, var(--ui-surface)); }`]
})
export class PlatformLegalPageComponent implements OnInit {
  protected readonly brand = injectSiteBrand();
  private readonly route = inject(ActivatedRoute);
  private readonly identity = inject(IdentityService);
  private readonly destroyRef = inject(DestroyRef);
  readonly page = signal<PlatformLegalPageDto | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal(false);

  ngOnInit(): void {
    this.route.paramMap.pipe(switchMap(params => {
      const slug = params.get('slug');
      if (!slug || !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug)) {
        this.loading.set(false);
        this.notFound.set(true);
        this.page.set(null);
        return of(null);
      }

      this.loading.set(true);
      this.notFound.set(false);
      this.error.set(false);
      this.page.set(null);
      return this.identity.getPublicLegalPage(slug).pipe(
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
