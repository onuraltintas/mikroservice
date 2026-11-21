import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MySubscriptionComponent } from '../../../shared/billing/my-subscription.component';

/** "Aboneliğim": the student's subscription, orders and payments for Hızlı Okuma, kept by the billing service. */
@Component({
  selector: 'app-student-subscription',
  standalone: true,
  imports: [MySubscriptionComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="subscription-page" aria-labelledby="subscription-title">
      <header class="subscription-header">
        <h1 id="subscription-title">Aboneliğim</h1>
        <p>Hızlı Okuma aboneliğin, siparişlerin ve ödemelerin burada.</p>
      </header>
      <app-my-subscription class="subscription-panel" product="speed-reading" />
    </main>
  `,
  styles: [`
    .subscription-page { max-width: 62rem; margin: 0 auto; padding: 1.5rem 1rem 3rem; }
    .subscription-header { margin-bottom: 1.25rem; }
    h1 { margin: 0; font-size: 1.75rem; font-weight: 700; color: var(--sp-text-1); }
    .subscription-header p { margin: 0.375rem 0 0; color: var(--sp-text-2); }
    /* Lively design (21.11.2026): the shared billing panel takes the soft mint tone for its highlighted areas. */
    .subscription-panel { display: block; --bill-soft: var(--sp-tone-mint-soft); }
  `]
})
export class StudentSubscriptionComponent {}
