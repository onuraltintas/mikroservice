import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { PASSWORD_POLICY_SUMMARY, passwordRequirements } from '../../../core/validators/password.validator';

/**
 * Shows the platform password policy before anything is typed and ticks each rule live.
 * Used by every admin-panel and staff-portal form that sets a password.
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
            <span aria-hidden="true">{{ requirement.met ? '✓' : '•' }}</span>
            {{ requirement.label }}
            <span class="password-requirements__sr">{{ requirement.met ? '(tamam)' : '(eksik)' }}</span>
          </li>
        }
      </ul>
    </div>
  `,
  styles: [`
    .password-requirements { margin: 4px 0 8px; font-size: 12px; line-height: 1.4; color: #4b5563; }
    .password-requirements__summary { margin: 0 0 4px; font-weight: 600; }
    .password-requirements__list { display: flex; flex-wrap: wrap; gap: 4px 12px; margin: 0; padding: 0; list-style: none; }
    .password-requirements__list li { display: inline-flex; gap: 4px; align-items: center; }
    .password-requirements__list li.met { color: #047857; }
    .password-requirements__sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
    :host-context(.dark) .password-requirements { color: #d1d5db; }
    :host-context(.dark) .password-requirements__list li.met { color: #34d399; }
  `],
})
export class PasswordRequirementsComponent {
  readonly password = input<string | null | undefined>('');
  readonly describedById = input<string | null>(null);
  readonly summary = PASSWORD_POLICY_SUMMARY;
  readonly requirements = computed(() => passwordRequirements(this.password()));
}
