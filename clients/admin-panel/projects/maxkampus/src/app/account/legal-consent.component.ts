import { Component, OnInit, inject, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LegalAcceptance, LegalPage, MaxkampusApi } from '../core/api';

/** The maxkampüs registration needs these published texts; each is accepted at its current version. */
export const MAXKAMPUS_LEGAL_SLUGS = ['kvkk', 'privacy', 'course-management-terms'] as const;

@Component({
  selector: 'mk-legal-consent',
  standalone: true,
  imports: [RouterLink],
  template: `
    <fieldset class="ds-fieldset legal">
      <legend>Onaylar</legend>
      @if (loading()) {
        <p class="ds-field__hint" role="status">Yasal metinler yükleniyor</p>
      } @else if (error()) {
        <p class="ds-alert ds-alert--warning" role="alert">{{ error() }}</p>
      } @else {
        @for (page of pages(); track page.slug) {
          <label class="ds-check item"><input type="checkbox" [checked]="selected.has(page.slug)" (change)="toggle(page, $any($event.target).checked)">
            <span><a [routerLink]="['/yasal', page.slug]" target="_blank" rel="noopener">{{ page.title }}</a>
              {{ page.slug === 'course-management-terms' ? 'koşullarını kabul ediyorum.' : 'metnini okudum.' }}</span></label>
        }
      }
    </fieldset>
  `,
  styles: `
    .legal { padding-bottom: 12px; display: flex; flex-direction: column; gap: 4px; }
    .item { align-items: flex-start; font-size: 14px; line-height: 1.5; } .item input { margin-top: 2px; }
  `
})
export class LegalConsentComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  readonly accepted = output<LegalAcceptance[] | null>();
  readonly pages = signal<LegalPage[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly selected = new Set<string>();

  async ngOnInit(): Promise<void> {
    try {
      this.pages.set(await Promise.all(MAXKAMPUS_LEGAL_SLUGS.map(slug => this.api.legal(slug))));
    } catch {
      this.error.set('Yasal metinler yüklenemedi; kayıt şu an tamamlanamıyor. Sayfayı yenileyin ya da biraz sonra yeniden deneyin.');
    } finally {
      this.loading.set(false);
      this.emit();
    }
  }

  toggle(page: LegalPage, on: boolean): void {
    if (on) this.selected.add(page.slug); else this.selected.delete(page.slug);
    this.emit();
  }

  private emit(): void {
    const pages = this.pages();
    this.accepted.emit(pages.length === MAXKAMPUS_LEGAL_SLUGS.length && pages.every(page => this.selected.has(page.slug))
      ? pages.map(page => ({ slug: page.slug, version: page.version })) : null);
  }
}
