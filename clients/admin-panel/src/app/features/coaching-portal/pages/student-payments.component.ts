import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { MySubscriptionComponent } from '../../../shared/billing/my-subscription.component';
import { CoachingFinanceComponent } from './coaching-finance.component';

/**
 * Ödemeler on one page: "Aboneliğim" (the subscription, orders, payments and invoices, kept by the billing service and
 * shown by the page every product shares) and Ücretler (the fees a teacher or institution charges, only when there
 * are any).
 */
@Component({
  selector: 'app-student-payments',
  standalone: true,
  imports: [CoachingFinanceComponent, MySubscriptionComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="cs-account cs-payments" aria-labelledby="payments-title">
    <header>
      <h1 id="payments-title">Ödemeler</h1>
      <p>Aboneliğin, ödemelerin, faturaların ve ücretlerin burada.</p>
    </header>
    <app-my-subscription product="coaching" />
    @if (showFees()) {
      <section id="ucretler" class="cs-account-section cs-fees" aria-labelledby="fees-heading">
        <h2 id="fees-heading">Ücretler</h2>
        <p>Öğretmeninin ya da kurumunun koçluk ücretleri ve ödediklerin.</p>
        <app-coaching-finance mode="student" />
      </section>
    }
  </div>`
})
export class StudentPaymentsComponent {
  private readonly navigation = inject(CoachingStudentNavigationService);
  /** Review of PR 122: Ücretler waits for the navigation answer instead of flashing and disappearing. */
  readonly showFees = computed(() => this.navigation.loaded() && this.navigation.showFees());
}
