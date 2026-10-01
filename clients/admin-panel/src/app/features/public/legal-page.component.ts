import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { IdentityService, PlatformLegalPageDto } from '../../core/services/identity.service';
import { catchError, map, of, switchMap } from 'rxjs';

@Component({
  selector: 'app-platform-legal-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <main class="mx-auto min-h-screen max-w-4xl px-4 py-10 sm:px-6" aria-labelledby="legal-page-title">
      <header class="mb-8 flex flex-wrap items-center justify-between gap-4 border-b border-slate-200 pb-5 dark:border-slate-800">
        <a routerLink="/coaching" class="text-lg font-bold text-indigo-800 dark:text-indigo-200">Eduİvme</a>
        <nav class="flex gap-4 text-sm" aria-label="Yasal sayfalar"><a routerLink="/legal/privacy" class="text-indigo-700 underline-offset-4 hover:underline dark:text-indigo-300">Gizlilik</a><a routerLink="/legal/kvkk" class="text-indigo-700 underline-offset-4 hover:underline dark:text-indigo-300">KVKK</a></nav>
      </header>

      @if (loading()) {
        <p class="rounded-xl border border-slate-200 p-5 text-sm text-slate-600 dark:border-slate-800 dark:text-slate-300" role="status">Yasal belge yükleniyor…</p>
      } @else if (page()) {
        <article class="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900 sm:p-9">
          <h1 id="legal-page-title" class="text-3xl font-bold tracking-tight text-slate-900 dark:text-white">{{ page()?.title }}</h1>
          <p class="mt-3 text-xs text-slate-500">Son güncelleme: {{ page()?.updatedAt || page()?.createdAt | date:'dd.MM.yyyy' }} · Sürüm {{ page()?.version }}</p>
          <pre class="mt-8 whitespace-pre-wrap break-words font-sans text-sm leading-7 text-slate-700 dark:text-slate-200">{{ page()?.content }}</pre>
        </article>
      } @else if (notFound()) {
        <section class="rounded-2xl border border-amber-200 bg-amber-50 p-6 dark:border-amber-900 dark:bg-amber-950" role="status">
          <h1 id="legal-page-title" class="text-xl font-semibold text-amber-950 dark:text-amber-100">Belge henüz yayımlanmadı</h1>
          <p class="mt-2 text-sm leading-6 text-amber-900 dark:text-amber-200">Bu ortak yasal belge henüz yetkili yönetici tarafından yayımlanmamış. Yayımlandığında Koçluk ve Hızlı Okuma ürünlerinde aynı güncel metin gösterilecektir.</p>
        </section>
      } @else {
        <p class="rounded-xl border border-red-200 bg-red-50 p-5 text-sm text-red-800 dark:border-red-900 dark:bg-red-950 dark:text-red-200" role="alert">Yasal belge şu anda yüklenemiyor. Lütfen daha sonra tekrar deneyin.</p>
      }
    </main>
  `,
  styles: [`:host { display: block; min-height: 100%; background: var(--ui-page, #f8fafc); }`]
})
export class PlatformLegalPageComponent implements OnInit {
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
