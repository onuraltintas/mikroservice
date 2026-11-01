import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { parentPanelLinks } from '../../core/utils/parent-panel-links';

/** V6b: a parent who lands on the speed-reading login/register page finds the way to the parent panel on the coaching site. */
@Component({
  selector: 'app-parent-panel-entry',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="parent-entry" aria-labelledby="parent-entry-title" data-testid="parent-panel-entry">
      <mat-icon class="parent-entry__icon" aria-hidden="true">family_restroom</mat-icon>
      <div class="parent-entry__body">
        <h2 id="parent-entry-title" class="parent-entry__title">Veli misin?</h2>
        <p class="parent-entry__text">Çocuğunun hızlı okuma ilerlemesini Veli panelinden izleyebilirsin.</p>
        <div class="parent-entry__links">
          <a [href]="links.login" data-testid="parent-panel-login">Veli paneline giriş</a>
          <a [href]="links.register" data-testid="parent-panel-register">Veli olarak kaydol</a>
        </div>
      </div>
    </section>
  `,
  styles: [`
    :host { display: block; margin-top: 1.25rem; }
    .parent-entry { display: flex; gap: .85rem; align-items: flex-start; padding: 1rem 1.1rem; border: 1px solid var(--ui-border);
      border-radius: .9rem; background: var(--ui-surface-muted); color: var(--ui-text); }
    .parent-entry__icon { flex-shrink: 0; width: 1.5rem; height: 1.5rem; font-size: 1.5rem; color: var(--ui-brand); }
    .parent-entry__body { min-width: 0; }
    .parent-entry__title { margin: 0; font-size: .95rem; font-weight: 750; line-height: 1.4; letter-spacing: 0; }
    .parent-entry__text { margin: .2rem 0 0; color: var(--ui-text-muted); font-size: .85rem; line-height: 1.6; }
    .parent-entry__links { display: flex; flex-wrap: wrap; gap: .25rem 1.25rem; margin-top: .35rem; }
    .parent-entry__links a { display: inline-flex; align-items: center; min-height: 2.75rem; color: var(--ui-brand); font-size: .85rem;
      font-weight: 700; text-decoration: underline; text-underline-offset: 3px; }
    .parent-entry__links a:focus-visible { outline: 3px solid var(--ui-brand); outline-offset: 3px; border-radius: .25rem; }
  `]
})
export class ParentPanelEntryComponent {
  readonly links = parentPanelLinks();
}
