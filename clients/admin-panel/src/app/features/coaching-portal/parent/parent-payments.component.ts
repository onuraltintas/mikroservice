import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, input, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, catchError, firstValueFrom, forkJoin, of } from 'rxjs';
import {
  CoachingBankTransferRequest, CoachingManagementService, CoachingSubscriptionAccess, CoachingSubscriptionPlan, CoachingSubscriptionSettings
} from '../../../core/services/coaching-management.service';
import { environment } from '../../../../environments/environment';
import { CoachingCheckoutComponent } from '../../../../../shared/coaching-checkout.component';
import { CoachingMyPaymentsComponent } from '../../../../../shared/coaching-my-payments.component';
import { CoachingCheckoutChoice, CoachingSalesApi } from '../../../../../shared/coaching-sales.api';
import { CoachingDatePipe, CoachingLabelPipe } from '../coaching-labels';
import { CsStatusChipComponent } from '../ui';
import { CoachingParentService, ParentFeeAccount } from './coaching-parent.service';
import { apiMessage } from './parent-home.component';
import { firstName, possessive } from './parent-model';

const money = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * V4 (01.11.2026): the child's Koçluk subscription and fees. The bank-transfer form appears only when the child needs a
 * subscription and no notice is already under review.
 */
@Component({
  selector: 'app-parent-payments',
  standalone: true,
  imports: [FormsModule, CoachingCheckoutComponent, CoachingMyPaymentsComponent, CoachingDatePipe, CoachingLabelPipe, CsStatusChipComponent],
  template: `
    <section class="cp-section" aria-labelledby="cp-payments-title" data-testid="parent-subscription">
      <h2 id="cp-payments-title">Ödemeler</h2>
      @if (error()) { <p class="cp-alert" role="alert">{{ error() }}</p> }
      @if (success()) { <p class="cp-success" role="status">{{ success() }}</p> }
      <div class="cp-card">
        <h3>Koçluk aboneliği</h3>
        @if (access(); as current) {
          @for (subscription of current.subscriptions; track subscription.id) {
            <p class="cp-line"><strong>{{ subscription.plan.name }}</strong> <cs-status-chip label="Etkin" tone="success" /> <span class="cp-muted">{{ subscription.endDate | coachingDate }} tarihine kadar</span></p>
          } @empty {
            @if (pendingTransfer()) {
              <p class="cp-line">Ödeme bildirimin inceleniyor. Onaylanınca {{ possessiveName() }} Koçluk erişimi açılır.</p>
            } @else if (needsPayment()) {
              <p class="cp-line">{{ possessiveName() }} etkin bir Koçluk aboneliği yok. Aşağıdan onun adına havale/EFT ödemesi bildirebilirsin.</p>
            } @else {
              <p class="cp-line">{{ name() }} Koçluk'u şu an kullanabiliyor; senin bir ödeme yapman gerekmiyor.</p>
            }
          }
        } @else if (sectionError()['access']) { <p class="cp-muted">{{ sectionError()['access'] }}</p> }
        @else { <p class="cp-muted" role="status">Abonelik bilgisi yükleniyor…</p> }
        @for (transfer of transfers(); track transfer.id) {
          <p class="cp-muted">{{ transfer.plan.name }} · {{ transfer.paymentReference }} · {{ transfer.status | coachingLabel }}</p>
        }

        @if (showForm()) {
          <form class="cp-form" (ngSubmit)="sendTransfer()">
            <p class="cp-span">Havale/EFT: <strong>{{ settings()?.accountHolder }}</strong> · {{ settings()?.bankName }} · <span class="cp-mono">{{ settings()?.iban }}</span></p>
            <label>Plan<select [(ngModel)]="planId" name="planId" required>@for (plan of plans(); track plan.id) { <option [value]="plan.id">{{ plan.name }} · {{ amount(plan.price) }} {{ settings()?.currency || 'TRY' }}</option> }</select></label>
            <label>Banka işlem referansı<input [(ngModel)]="paymentReference" name="paymentReference" maxlength="120" required autocomplete="off" /></label>
            <label>Ödemeyi yapan<input [(ngModel)]="payerName" name="payerName" maxlength="200" autocomplete="name" /></label>
            <label>Not<input [(ngModel)]="note" name="note" maxlength="1000" /></label>
            <div class="cp-span"><edu-coaching-checkout [api]="sales" [planId]="planId" [price]="selectedPlanPrice()" [currency]="settings()?.currency || 'TRY'" (choiceChange)="checkout = $event" /></div>
            <label class="cp-check cp-span"><input type="checkbox" [(ngModel)]="adultPayerDeclaration" name="adultPayerDeclaration" />Ödemeyi yapan kişi olarak 18 yaş ve üzeri olduğumu beyan ediyorum.</label>
            <button type="submit" class="cp-primary cp-span" [disabled]="sending() || !checkout.acceptSalesContract">{{ sending() ? 'Gönderiliyor…' : 'Ödeme yaptım, bildir' }}</button>
          </form>
        }
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

      <div class="cp-card" aria-label="Veli olarak yaptığın ödemeler">
        <edu-coaching-my-payments [api]="sales" heading="Veli olarak yaptığın ödemeler" />
      </div>
    </section>`
})
export class ParentPaymentsComponent implements OnInit {
  private readonly service = inject(CoachingParentService);
  private readonly management = inject(CoachingManagementService);
  readonly studentId = input.required<string>();
  readonly childName = input('');
  readonly name = computed(() => firstName(this.childName()));
  readonly possessiveName = computed(() => possessive(this.name()));
  readonly fees = signal<ParentFeeAccount | null>(null);
  readonly access = signal<CoachingSubscriptionAccess | null>(null);
  readonly transfers = signal<CoachingBankTransferRequest[]>([]);
  readonly plans = signal<CoachingSubscriptionPlan[]>([]);
  readonly settings = signal<CoachingSubscriptionSettings | null>(null);
  readonly sectionError = signal<Record<string, string>>({});
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly sending = signal(false);
  readonly pendingTransfer = computed(() => this.transfers().some(t => t.status === 'Pending'));
  /** A subscription is needed only when access requires one and none is active or under review. */
  readonly needsPayment = computed(() => { const access = this.access(); return !!access && access.enforcementEnabled && !access.subscriptions.length && !this.pendingTransfer(); });
  readonly showForm = computed(() => this.needsPayment() && this.plans().length > 0 && !!this.settings());
  readonly hasFees = computed(() => { const fees = this.fees(); return !!fees && (fees.agreements.length > 0 || fees.balance > 0); });
  planId = '';
  paymentReference = '';
  payerName = '';
  note = '';
  adultPayerDeclaration = false;
  /** Roadmap O10: discount code and contract acceptance from the checkout. */
  checkout: CoachingCheckoutChoice = { couponCode: null, acceptSalesContract: false };
  readonly sales = new CoachingSalesApi(inject(HttpClient), environment.apiUrl);
  private readonly checkoutForm = viewChild(CoachingCheckoutComponent);

  amount(value: number | null | undefined) { return money.format(value ?? 0); }
  selectedPlanPrice(): number { return this.plans().find(plan => plan.id === this.planId)?.price ?? 0; }

  ngOnInit(): void {
    const id = this.studentId();
    const safe = <T>(name: string, message: string, source: Observable<T>, fallback: T) => source.pipe(catchError(() => {
      this.sectionError.update(errors => ({ ...errors, [name]: message })); return of(fallback);
    }));
    forkJoin({
      fees: safe('fees', 'Ücret bilgisi yüklenemedi.', this.service.getFees(id), null),
      access: safe('access', 'Abonelik bilgisi yüklenemedi.', this.service.getAccess(id), null),
      transfers: safe('transfers', '', this.service.getTransfers(id), [] as CoachingBankTransferRequest[]),
      plans: safe('plans', '', this.management.getPublicSubscriptionPlans(), [] as CoachingSubscriptionPlan[]),
      settings: safe('settings', '', this.management.getPublicBankTransferSettings(), null)
    }).subscribe(result => {
      this.fees.set(result.fees); this.access.set(result.access); this.transfers.set(result.transfers);
      const plans = result.plans.filter(plan => plan.audience === 'Individual' && plan.isActive && plan.isPublic && !plan.isContactOnly);
      this.plans.set(plans); this.settings.set(result.settings as CoachingSubscriptionSettings | null);
      if (!this.planId && plans.length) this.planId = plans[0].id;
    });
  }

  async sendTransfer(): Promise<void> {
    const reference = this.paymentReference.trim();
    if (!this.planId || !reference) { this.error.set('Plan ve banka işlem referansı gerekli.'); return; }
    if (!this.adultPayerDeclaration) { this.error.set('Ödeme için 18 yaş ve üzeri olduğunu beyan etmen gerekir.'); return; }
    if (!this.checkout.acceptSalesContract) { this.error.set('Ödeme bildiriminden önce ön bilgilendirme formunu ve mesafeli satış sözleşmesini açıp onaylaman gerekir.'); return; }
    this.sending.set(true); this.error.set(null); this.success.set(null);
    try {
      const sent = await firstValueFrom(this.service.sendTransfer(this.studentId(), {
        planId: this.planId, paymentReference: reference, payerName: this.payerName.trim() || null, note: this.note.trim() || null,
        adultPayerDeclaration: true, couponCode: this.checkout.couponCode, acceptSalesContract: true
      }));
      this.transfers.update(items => [sent, ...items.filter(item => item.id !== sent.id)]);
      this.paymentReference = ''; this.note = ''; this.adultPayerDeclaration = false; this.checkoutForm()?.reset();
      this.success.set(`Ödeme bildirimin incelemeye alındı. Onaylanınca ${this.possessiveName()} erişimi açılır.`);
    } catch (response) {
      this.error.set(apiMessage(response, 'Ödeme bildirimi gönderilemedi.'));
    } finally { this.sending.set(false); }
  }
}
