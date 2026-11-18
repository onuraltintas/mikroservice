import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { IdentityService, PlatformLegalPageDto, RegistrationLegalAcceptance } from '../../core/services/identity.service';
import { injectAuthCopy } from './auth-voice';

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
    <section class="legal" aria-label="Yasal metinler ve kayıt onayları">
      <h2 class="legal__title">Yasal metinler</h2>
      @if (loading()) {
        <p class="legal__note" role="status">Güncel yasal metinler yükleniyor…</p>
      } @else if (error()) {
        <p class="ds-alert ds-alert--warning" role="alert">{{ error() }} Kayıt, yayımlanmış güncel metinler olmadan tamamlanamaz.</p>
      } @else {
        @for (page of pages(); track page.slug) {
          <label class="legal__item">
            <input type="checkbox" [checked]="isSelected(page.slug)" (change)="setSelected(page, $any($event.target).checked)" />
            <span><a [routerLink]="['/legal', page.slug]" target="_blank" rel="noopener noreferrer" class="ds-link">{{ page.title }}</a>
              {{ isTerms(page.slug) ? 'koşullarını kabul ediyorum.' : 'metnini okudum ve bilgilendirildim.' }}</span>
          </label>
        }
        <p class="legal__note" data-testid="legal-consent-note">{{ copy.legalNote }}</p>
      }
    </section>
  `,
  styles: [`
    .legal { display: flex; flex-direction: column; gap: 10px; padding: 16px; border: 1px solid var(--ds-line); border-radius: var(--ds-radius-control); background: var(--ds-bg); }
    .legal__title { margin: 0; color: var(--ds-text); font-size: 15px; font-weight: 600; }
    .legal__item { display: flex; align-items: flex-start; gap: 10px; min-height: 44px; color: var(--ds-text); font-size: 15px; line-height: 1.5; cursor: pointer; }
    .legal__item input { flex: none; width: 20px; height: 20px; margin: 2px 0 0; accent-color: var(--ds-primary); }
    .legal__note { margin: 0; color: var(--ds-muted); font-size: 13px; line-height: 1.5; }
    .ds-alert { margin: 0; }
  `]
})
export class RegistrationLegalConsentComponent implements OnInit {
  private readonly identity = inject(IdentityService);
  /** PR #124 review L7: also shown in the Google consent step on the management host's login page. */
  readonly copy = injectAuthCopy().copy;
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
