import { Component, ElementRef, inject, viewChild } from '@angular/core';
import { DarkModeService, ThemeMode } from '../../../../../src/app/core/services/dark-mode.service';

/** Görünüm: light, dark or the device setting. A small menu in the site and panel headers; the choice is kept in this browser. */
@Component({
  selector: 'mk-theme-menu',
  standalone: true,
  host: { '(document:keydown.escape)': 'close()', '(document:click)': 'outside($event)' },
  template: `
    <details class="theme" #menu>
      <summary [attr.aria-label]="'Görünüm: ' + current()" [attr.title]="'Görünüm: ' + current()">
        @if (theme.isDarkMode()) {
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5z"/></svg>
        } @else {
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></svg>
        }
      </summary>
      <div class="theme__menu" role="group" aria-label="Görünüm">
        @for (option of options; track option.mode) {
          <button type="button" [attr.aria-pressed]="theme.mode() === option.mode" (click)="choose(option.mode)">{{ option.label }}</button>
        }
      </div>
    </details>`,
  styles: `
    :host { position: relative; display: inline-flex; }
    .theme summary { display: grid; width: 44px; height: 44px; place-items: center; border: 1px solid var(--ds-line); border-radius: 10px;
      background: var(--ds-surface); color: var(--ds-text); cursor: pointer; list-style: none; }
    .theme summary::-webkit-details-marker { display: none; }
    .theme summary:focus-visible { outline: 3px solid var(--ds-focus); outline-offset: 2px; }
    .theme__menu { position: absolute; right: 0; top: calc(100% + 6px); z-index: 30; display: grid; min-width: 210px; padding: 6px;
      border: 1px solid var(--ds-line); border-radius: 12px; background: var(--ds-surface); box-shadow: var(--ds-shadow-raised, var(--ds-shadow)); }
    .theme__menu button { min-height: 44px; padding: 0 12px; border: 0; border-radius: 8px; background: transparent; color: var(--ds-text);
      font: inherit; text-align: left; white-space: nowrap; cursor: pointer; }
    .theme__menu button:hover { background: var(--ds-soft); }
    .theme__menu button[aria-pressed='true'] { font-weight: 600; box-shadow: inset 3px 0 var(--ds-accent); }
    .theme__menu button:focus-visible { outline: 3px solid var(--ds-focus); outline-offset: -3px; }
  `
})
export class ThemeMenuComponent {
  readonly theme = inject(DarkModeService);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly menu = viewChild<ElementRef<HTMLDetailsElement>>('menu');
  readonly options: { mode: ThemeMode; label: string }[] = [
    { mode: 'light', label: 'Açık' }, { mode: 'dark', label: 'Koyu' }, { mode: 'system', label: 'Cihaz ayarına göre' }];

  current(): string { return this.options.find(option => option.mode === this.theme.mode())?.label ?? 'Cihaz ayarına göre'; }
  choose(mode: ThemeMode): void { this.theme.setMode(mode); this.close(); }
  close(): void { const menu = this.menu()?.nativeElement; if (menu) menu.open = false; }
  outside(event: Event): void { if (!this.host.nativeElement.contains(event.target as Node)) this.close(); }
}
