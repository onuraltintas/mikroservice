import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { PASSWORD_POLICY_SUMMARY, passwordRequirements } from '../../validators/password.validator';

/**
 * Shows the platform password policy before anything is typed and ticks each rule live.
 * Used by every speed-reading form that sets a password.
 */
@Component({
  selector: 'app-password-requirements',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="password-requirements" [attr.id]="describedById()">
      <p class="password-requirements__summary">{{ summary }}.</p>
      <ul class="password-requirements__list" aria-live="polite">
        @for (requirement of requirements(); track requirement.key) {
          <li [class.met]="requirement.met" [attr.data-met]="requirement.met">
            @if (requirement.met) {
              <svg width="14" height="14" viewBox="0 0 16 16" aria-hidden="true"><path d="M3 8.5l3 3 7-7" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"></path></svg>
            } @else {
              <svg width="14" height="14" viewBox="0 0 16 16" aria-hidden="true"><circle cx="8" cy="8" r="2.5" fill="currentColor"></circle></svg>
            }
            {{ requirement.label }}
            <span class="password-requirements__sr">{{ requirement.met ? '(tamam)' : '(eksik)' }}</span>
          </li>
        }
      </ul>
    </div>
  `,
  styles: [`
    .password-requirements { margin: 4px 0 8px; font-size: 13px; line-height: 1.5; color: var(--ds-muted, var(--ui-text-muted, var(--ui-text))); }
    .password-requirements__summary { margin: 0 0 4px; font-weight: 600; }
    .password-requirements__list { display: flex; flex-wrap: wrap; gap: 4px 12px; margin: 0; padding: 0; list-style: none; }
    .password-requirements__list li { display: inline-flex; gap: 4px; align-items: center; }
    .password-requirements__list li.met { color: var(--ds-success, var(--ui-success, var(--ui-text))); }
    .password-requirements__sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
  `],
})
export class PasswordRequirementsComponent {
  readonly password = input<string | null | undefined>('');
  readonly describedById = input<string | null>(null);
  readonly summary = PASSWORD_POLICY_SUMMARY;
  readonly requirements = computed(() => passwordRequirements(this.password()));
}
