import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { IdentityService, PlatformLegalPageDto, RegistrationLegalAcceptance } from '../../core/services/identity.service';

export type RegistrationProduct = 'coaching' | 'speed-reading';

const REQUIRED_SLUGS: Record<RegistrationProduct, string[]> = {
  coaching: ['privacy', 'kvkk', 'coaching-terms'],
  'speed-reading': ['privacy', 'kvkk', 'speed-reading-terms']
};

@Component({
  selector: 'app-registration-legal-consent',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <section class="space-y-3 rounded-xl border border-slate-200 bg-slate-50 p-4 dark:border-slate-700 dark:bg-slate-950" aria-label="Yasal metinler ve kayıt onayları">
      <h2 class="text-sm font-semibold text-slate-900 dark:text-white">Yasal metinler</h2>
      @if (loading()) {
        <p class="text-sm text-slate-600 dark:text-slate-300" role="status">Güncel yasal metinler yükleniyor…</p>
      } @else if (error()) {
        <p class="text-sm text-amber-900 dark:text-amber-200" role="alert">{{ error() }} Kayıt, yayımlanmış güncel metinler olmadan tamamlanamaz.</p>
      } @else {
        @for (page of pages(); track page.slug) {
          <label class="flex items-start gap-3 text-sm leading-6 text-slate-700 dark:text-slate-200">
            <input class="mt-1" type="checkbox" [checked]="isSelected(page.slug)" (change)="setSelected(page, $any($event.target).checked)" />
            <span><a [routerLink]="['/legal', page.slug]" target="_blank" rel="noopener noreferrer" class="font-semibold text-indigo-700 underline dark:text-indigo-300">{{ page.title }} · sürüm {{ page.version }}</a>
              {{ isTerms(page.slug) ? 'koşullarını kabul ediyorum.' : 'metnini okudum ve bilgilendirildim.' }}</span>
          </label>
        }
        <p class="text-xs leading-5 text-slate-500">Her kayıt işleminde onaylanan metin sürümü Identity’de saklanır. Metin taslaksa kayıt kapalı kalır.</p>
      }
    </section>
  `
})
export class RegistrationLegalConsentComponent implements OnInit {
  private readonly identity = inject(IdentityService);
  @Input() product: RegistrationProduct = 'coaching';
  @Output() acceptancesChange = new EventEmitter<RegistrationLegalAcceptance[]>();
  @Output() readinessChange = new EventEmitter<boolean>();

  readonly pages = signal<PlatformLegalPageDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  private readonly selected = new Set<string>();

  ngOnInit(): void {
    const requiredSlugs = REQUIRED_SLUGS[this.product];
    forkJoin(requiredSlugs.map(slug => this.identity.getPublicLegalPage(slug).pipe(
      map(page => ({ page, slug, unavailable: false })),
      catchError(() => of({ page: null, slug, unavailable: true }))
    ))).subscribe(results => {
      const missing = results.filter(result => result.unavailable || !result.page);
      this.pages.set(results.flatMap(result => result.page ? [result.page] : []));
      this.loading.set(false);
      if (missing.length) {
        this.error.set('Gerekli yasal metinlerden biri yayımlanmamış veya şu anda yüklenemiyor.');
        this.readinessChange.emit(false);
        return;
      }
      this.readinessChange.emit(true);
    });
  }

  isSelected(slug: string): boolean {
    return this.selected.has(slug);
  }

  isTerms(slug: string): boolean {
    return slug.endsWith('-terms');
  }

  setSelected(page: PlatformLegalPageDto, checked: boolean): void {
    if (checked) this.selected.add(page.slug);
    else this.selected.delete(page.slug);
    this.acceptancesChange.emit(this.pages()
      .filter(item => this.selected.has(item.slug))
      .map(item => ({ slug: item.slug, version: item.version })));
  }
}
