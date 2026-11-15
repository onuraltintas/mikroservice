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
      <h1 id="subscription-title">Aboneliğim</h1>
      <app-my-subscription product="speed-reading" />
    </main>
  `,
  styles: [`
    .subscription-page { max-width: 62rem; margin: 0 auto; padding: 1.5rem 1rem 3rem; }
    h1 { margin: 0 0 1rem; font-size: 1.5rem; font-weight: 700; }
  `]
})
export class StudentSubscriptionComponent {}
