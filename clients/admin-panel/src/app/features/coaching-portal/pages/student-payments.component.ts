import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { CoachingFinanceComponent } from './coaching-finance.component';
import { CoachingSubscriptionComponent } from './coaching-subscription.component';

/**
 * T5 (31.10.2026): Ödemeler on one page — Abonelik durumu, Havale bildirimleri, Ücretler (the fees a teacher or
 * institution charges, only when there are any) and Faturalar.
 */
@Component({
  selector: 'app-student-payments',
  standalone: true,
  imports: [CoachingFinanceComponent, CoachingSubscriptionComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-coaching-subscription>
    @if (showFees()) {
      <section fees id="ucretler" class="cs-account-section cs-fees" aria-labelledby="fees-heading">
        <h2 id="fees-heading">Ücretler</h2>
        <p>Öğretmeninin ya da kurumunun koçluk ücretleri ve ödediklerin.</p>
        <app-coaching-finance mode="student" />
      </section>
    }
  </app-coaching-subscription>`
})
export class StudentPaymentsComponent {
  readonly showFees = inject(CoachingStudentNavigationService).showFees;
}
