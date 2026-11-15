import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Observable, catchError, forkJoin, of } from 'rxjs';
import { CoachingSubscriptionAccess } from '../../../core/services/coaching-management.service';
import { CoachingDatePipe } from '../coaching-labels';
import { CsStatusChipComponent } from '../ui';
import { CoachingParentService, ParentFeeAccount } from './coaching-parent.service';
import { firstName, possessive } from './parent-model';

const money = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * The child's Koçluk subscription and fees, for the parent to see. A subscription is bought in the student's own
 * account ("Aboneliğim"); whoever pays writes the payment code on the bank transfer (decision 15.11.2026: no
 * separate "parent pays for the child" order).
 */
@Component({
  selector: 'app-parent-payments',
  standalone: true,
  imports: [CoachingDatePipe, CsStatusChipComponent],
  template: `
    <section class="cp-section" aria-labelledby="cp-payments-title" data-testid="parent-subscription">
      <h2 id="cp-payments-title">Ödemeler</h2>
      <div class="cp-card">
        <h3>Koçluk aboneliği</h3>
        @if (access(); as current) {
          @if (!current.enforcementEnabled) {
            <p class="cp-line">{{ name() }} Koçluk'u şu an kullanabiliyor; bir ödeme yapılması gerekmiyor.</p>
          } @else if (current.hasAccess) {
            <p class="cp-line">@if (current.planName) { <strong>{{ current.planName }}</strong> } <cs-status-chip label="Etkin" tone="success" />
              @if (current.accessUntil) { <span class="cp-muted">{{ current.accessUntil | coachingDate }} tarihine kadar</span> }</p>
          } @else {
            <p class="cp-line">{{ possessiveName() }} etkin bir Koçluk aboneliği yok.</p>
            <p class="cp-muted">Abonelik {{ possessiveName() }} kendi hesabından, "Ödemeler" sayfasındaki Aboneliğim bölümünden alınır. Havaleyi sen de yapabilirsin: açıklamaya o sayfadaki ödeme kodunu yazman yeterli.</p>
          }
        } @else if (sectionError()['access']) { <p class="cp-muted">{{ sectionError()['access'] }}</p> }
        @else { <p class="cp-muted" role="status">Abonelik bilgisi yükleniyor…</p> }
      </div>

      @if (hasFees()) {
        <div class="cp-card" data-testid="parent-fees">
          <h3>Koçluk ücretleri</h3>
          @if (fees(); as account) {
            <p class="cp-line">Kalan: <strong>{{ amount(account.balance) }} TL</strong>@if (account.overdueAmount > 0) { · <span class="cp-danger">Gecikmiş: <strong>{{ amount(account.overdueAmount) }} TL</strong></span> }</p>
            @for (fee of account.agreements; track fee.id) { <p class="cp-muted">{{ fee.title }} · {{ amount(fee.totalAmount) }} TL</p> }
          }
        </div>
      } @else if (sectionError()['fees']) { <p class="cp-muted">{{ sectionError()['fees'] }}</p> }
    </section>`
})
export class ParentPaymentsComponent implements OnInit {
  private readonly service = inject(CoachingParentService);
  readonly studentId = input.required<string>();
  readonly childName = input('');
  readonly name = computed(() => firstName(this.childName()));
  readonly possessiveName = computed(() => possessive(this.name()));
  readonly fees = signal<ParentFeeAccount | null>(null);
  readonly access = signal<CoachingSubscriptionAccess | null>(null);
  readonly sectionError = signal<Record<string, string>>({});
  readonly hasFees = computed(() => { const fees = this.fees(); return !!fees && (fees.agreements.length > 0 || fees.balance > 0); });

  amount(value: number | null | undefined) { return money.format(value ?? 0); }

  ngOnInit(): void {
    const id = this.studentId();
    const safe = <T>(name: string, message: string, source: Observable<T>, fallback: T) => source.pipe(catchError(() => {
      this.sectionError.update(errors => ({ ...errors, [name]: message })); return of(fallback);
    }));
    forkJoin({
      fees: safe('fees', 'Ücret bilgisi yüklenemedi.', this.service.getFees(id), null),
      access: safe('access', 'Abonelik bilgisi yüklenemedi.', this.service.getAccess(id), null)
    }).subscribe(result => { this.fees.set(result.fees); this.access.set(result.access); });
  }
}
