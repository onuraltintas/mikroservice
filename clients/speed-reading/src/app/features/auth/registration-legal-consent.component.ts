import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { PlatformLegalPage, PlatformLegalPagesService } from '../../core/services/platform-legal-pages.service';
import { RegistrationLegalAcceptance } from '../../core/models/user.model';

@Component({
  selector: 'app-registration-legal-consent',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <section class="consents-wrapper" aria-label="Yasal metinler ve kayıt onayları">
      <strong>Yasal metinler</strong>
      @if (loading()) {
        <p role="status">Güncel yasal metinler yükleniyor…</p>
      } @else if (error()) {
        <p class="consent-hint" role="alert">{{ error() }} Kayıt, yayımlanmış güncel metinler olmadan tamamlanamaz.</p>
      } @else {
        @for (page of pages(); track page.slug) {
          <label class="legal-consent-row">
            <input type="checkbox" [checked]="isSelected(page.slug)" (change)="setSelected(page, $any($event.target).checked)" />
            <span><a [routerLink]="['/legal', page.slug]" target="_blank" rel="noopener noreferrer">{{ page.title }} · sürüm {{ page.version }}</a>
              {{ isTerms(page.slug) ? 'koşullarını kabul ediyorum.' : 'metnini okudum ve bilgilendirildim.' }}</span>
          </label>
        }
        <p class="consent-hint">Onaylanan metin sürümleri güvenli şekilde kayıt altına alınır.</p>
      }
    </section>
  `,
  styles: [`.legal-consent-row { display: flex; align-items: flex-start; gap: .6rem; line-height: 1.5; } .legal-consent-row input { margin-top: .25rem; } .legal-consent-row a { font-weight: 600; text-decoration: underline; }`]
})
export class RegistrationLegalConsentComponent implements OnInit {
  private readonly legalPages = inject(PlatformLegalPagesService);
  @Input() product: 'coaching' | 'speed-reading' = 'speed-reading';
  @Output() acceptancesChange = new EventEmitter<RegistrationLegalAcceptance[]>();
  @Output() readinessChange = new EventEmitter<boolean>();

  readonly pages = signal<PlatformLegalPage[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  private readonly selected = new Set<string>();

  ngOnInit(): void {
    const termsSlug = this.product === 'coaching' ? 'coaching-terms' : 'speed-reading-terms';
    const requiredSlugs = ['privacy', 'kvkk', termsSlug];
    forkJoin(requiredSlugs.map(slug => this.legalPages.getPage(slug).pipe(
      map(page => ({ page, unavailable: false })),
      catchError(() => of({ page: null, unavailable: true }))
    ))).subscribe(results => {
      const missing = results.some(result => result.unavailable || !result.page?.isPublished || !result.page.content?.trim());
      this.pages.set(results.flatMap(result => result.page?.isPublished && result.page.content?.trim() ? [result.page] : []));
      this.loading.set(false);
      if (missing) {
        this.error.set('Gerekli yasal metinlerden biri yayımlanmamış veya şu anda yüklenemiyor.');
        this.readinessChange.emit(false);
        return;
      }
      this.readinessChange.emit(true);
    });
  }

  isSelected(slug: string): boolean { return this.selected.has(slug); }
  isTerms(slug: string): boolean { return slug.endsWith('-terms'); }

  setSelected(page: PlatformLegalPage, checked: boolean): void {
    if (checked) this.selected.add(page.slug);
    else this.selected.delete(page.slug);
    this.acceptancesChange.emit(this.pages()
      .filter(item => this.selected.has(item.slug))
      .map(item => ({ slug: item.slug, version: item.version })));
  }
}
