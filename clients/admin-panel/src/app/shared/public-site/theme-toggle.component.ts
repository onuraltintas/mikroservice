import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DarkModeService } from '../../core/services/dark-mode.service';

/**
 * Light/dark switch for the public sites (eduivme.com, maxkoc.com; 27.11.2026). Sites open light; one press switches
 * and the choice is kept in this browser (DarkModeService, `theme` key). The label says what the press does.
 */
@Component({
  selector: 'app-theme-toggle',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button type="button" class="toggle" (click)="theme.toggle()"
      [attr.aria-label]="theme.isDarkMode() ? 'Açık temaya geç' : 'Koyu temaya geç'"
      [attr.title]="theme.isDarkMode() ? 'Açık temaya geç' : 'Koyu temaya geç'">
      <span class="toggle__icon" [class.toggle__icon--dark]="theme.isDarkMode()" aria-hidden="true">
        <svg class="sun" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></svg>
        <svg class="moon" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5z"/></svg>
      </span>
    </button>
  `,
  styles: `
    :host { display: inline-flex; }
    .toggle { position: relative; display: grid; width: 44px; height: 44px; place-items: center; padding: 0; border: 1px solid var(--ds-line);
      border-radius: 12px; background: var(--ds-surface); color: var(--ds-text); cursor: pointer;
      transition: background-color 200ms ease, border-color 200ms ease, transform 200ms ease; }
    .toggle:hover { border-color: var(--ds-primary); background: var(--ds-soft); }
    .toggle:active { transform: scale(0.94); }
    .toggle:focus-visible { outline: 3px solid var(--ds-focus); outline-offset: 2px; }
    .toggle__icon { position: relative; display: block; width: 20px; height: 20px; }
    .toggle__icon svg { position: absolute; inset: 0; transition: opacity 250ms ease, transform 350ms cubic-bezier(0.2, 0.8, 0.2, 1); }
    .moon { opacity: 0; transform: rotate(-60deg) scale(0.6); }
    .toggle__icon--dark .sun { opacity: 0; transform: rotate(60deg) scale(0.6); }
    .toggle__icon--dark .moon { opacity: 1; transform: none; }
    @media (prefers-reduced-motion: reduce) { .toggle, .toggle__icon svg { transition: none; } }
  `
})
export class ThemeToggleComponent {
  readonly theme = inject(DarkModeService);
}
