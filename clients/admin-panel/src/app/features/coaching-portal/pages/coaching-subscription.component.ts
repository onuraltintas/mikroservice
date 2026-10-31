import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingLabelPipe } from '../coaching-labels';
import { CsStatusChipComponent } from '../ui';
import { environment } from '../../../../environments/environment';
import { CoachingCheckoutComponent } from '../../../../../shared/coaching-checkout.component';
import { CoachingMyPaymentsComponent } from '../../../../../shared/coaching-my-payments.component';
import { CoachingCheckoutChoice, CoachingSalesApi, coachingErrorMessage, coachingMoney } from '../../../../../shared/coaching-sales.api';
import {
  CoachingBankTransferRequest,
  CoachingManagementService,
  CoachingSubscriptionAccess,
  CoachingSubscriptionPlan,
  CoachingSubscriptionSettings
} from '../../../core/services/coaching-management.service';

@Component({
  selector: 'app-coaching-subscription',
  standalone: true,
  imports: [CoachingCheckoutComponent, CoachingLabelPipe, CoachingMyPaymentsComponent, CommonModule, CsStatusChipComponent, FormsModule],
  template: `
    <div class="cs-account cs-payments" aria-labelledby="coaching-subscription-title">
      <header>
        <h1 id="coaching-subscription-title">Ödemeler</h1>
        <p>Aboneliğin, havale bildirimlerin, ücretlerin ve faturaların burada.</p>
      </header>

      @if (subscriptionRequired) {
        <p data-testid="subscription-required" role="status" class="cs-work-note">Bu sayfa için etkin bir Koçluk aboneliği gerekiyor. Aşağıdan bir plan seç ve havale bildirimini gönder. Öğretmeninin ya da kurumunun aboneliğindeysen, sana yer açtıklarında erişimin kendiliğinden açılır.</p>
      }
      @if (error()) { <p role="alert" class="cs-work-alert is-danger">{{ error() }}</p> }
      @if (success()) { <p role="status" class="cs-work-alert is-success">{{ success() }}</p> }

      <section aria-labelledby="access-heading">
        <h2 id="access-heading">Abonelik durumu</h2>
        @if (access()?.subscriptions?.length) {
          <p>Erişimin açık.</p>
          <ul class="cs-payments-list">@for (subscription of access()?.subscriptions; track subscription.id) {
            <li><span><strong>{{ subscription.plan.name }}</strong><small>{{ subscription.startDate | date:'d.MM.yyyy' }} – {{ subscription.endDate | date:'d.MM.yyyy' }}</small></span>
              <cs-status-chip [label]="subscription.status === 'Active' ? 'Etkin' : (subscription.status | coachingLabel)" [tone]="subscription.status === 'Active' ? 'success' : 'neutral'" /></li>
          }</ul>
        } @else if (access()?.enforcementEnabled) {
          <p>Etkin bir aboneliğin yok. Aşağıdan bir plan seçebilirsin.</p>
        } @else if (access()) {
          <p>Koçluk erişimin açık; şu an abonelik gerekmiyor.</p>
        }

        <h3>Planlar</h3>
        @if (loading()) { <p role="status" class="muted">Planlar yükleniyor…</p> }
        @if (!loading() && plans().length === 0) { <p class="muted">Şu an satın alınabilecek bir plan yok. Kurum aboneliği için kurum yöneticine yaz.</p> }
        <div class="cs-plans">
          @for (plan of plans(); track plan.id) {
            <article class="cs-plan" [class.is-selected-plan]="selectedPlanId() === plan.id">
              <h4>{{ plan.name }}</h4>
              @if (plan.description) { <p>{{ plan.description }}</p> }
              <p class="price">{{ plan.isContactOnly ? 'Fiyat için yaz' : (plan.price | number:'1.2-2') + ' ' + (settings()?.currency || 'TRY') }}</p>
              <p class="muted">{{ plan.durationDays }} gün, {{ plan.billingPeriod | coachingLabel:'billingPeriod' }}</p>
              @if (plan.features.length) { <ul>@for (feature of plan.features; track feature) { <li>{{ feature }}</li> }</ul> }
              @if ((plan.trialDays ?? 0) > 0 && !plan.isContactOnly && !hasSubscription()) {
                <button type="button" class="cs-button-secondary" [attr.data-testid]="'trial-' + plan.id" [disabled]="trialPlanId() !== null" (click)="startTrial(plan)">{{ trialPlanId() === plan.id ? 'Başlatılıyor…' : plan.trialDays + ' gün ücretsiz dene' }}</button>
              }
              <button type="button" class="cs-button-primary" [disabled]="plan.isContactOnly" (click)="selectPlan(plan)">{{ plan.isContactOnly ? 'Çevrimiçi satın alınamaz' : (selectedPlanId() === plan.id ? 'Seçildi' : 'Bu planı seç') }}</button>
            </article>
          }
        </div>
      </section>

      <section aria-labelledby="requests-heading">
        <h2 id="requests-heading">Havale bildirimleri</h2>
        <p>Havaleyi yaptıktan sonra buradan bildir. Ödemeni doğrulayınca erişimin açılır.</p>
        @if (selectedPlan() && !selectedPlan()?.isContactOnly) {
          <div class="cs-transfer" aria-labelledby="payment-heading">
            <div class="bank">
              <h3 id="payment-heading">Havale bilgileri</h3>
              @if (settings()) {
                <dl><div><dt>Hesap sahibi</dt><dd>{{ settings()?.accountHolder }}</dd></div><div><dt>Banka</dt><dd>{{ settings()?.bankName }}</dd></div><div><dt>IBAN</dt><dd class="iban">{{ settings()?.iban }}</dd></div><div><dt>Açıklama</dt><dd>{{ settings()?.paymentInstructions || 'Açıklamaya işlem referansını yaz.' }}</dd></div></dl>
              } @else { <p class="cs-work-note">Havale şu an kullanılamıyor. Daha sonra yeniden dene.</p> }
            </div>
            <form (ngSubmit)="submitRequest()">
              <h3>Ödemeni bildir</h3>
              <p class="muted">{{ selectedPlan()?.name }}, {{ selectedPlan()?.price | number:'1.2-2' }} {{ settings()?.currency || 'TRY' }}</p>
              <label>Banka işlem referansı<input [(ngModel)]="paymentReference" name="paymentReference" maxlength="120" required autocomplete="off" /></label>
              <label>Ödemeyi yapan kişi (isteğe bağlı)<input [(ngModel)]="payerName" name="payerName" maxlength="200" autocomplete="name" /></label>
              <label>Not (isteğe bağlı)<textarea [(ngModel)]="note" name="paymentNote" maxlength="1000" rows="3"></textarea></label>
              <edu-coaching-checkout [api]="sales" [planId]="selectedPlanId()" [price]="selectedPlan()?.price ?? 0" [currency]="settings()?.currency || 'TRY'" (choiceChange)="checkout = $event" />
              <label class="check"><input type="checkbox" [(ngModel)]="adultPayerDeclaration" name="adultPayerDeclaration" required />Ödemeyi yapan kişi olarak 18 yaşından büyük olduğumu beyan ediyorum. Bu bir yaş doğrulaması değildir.</label>
              <button class="cs-button-primary" type="submit" [disabled]="submitting() || !settings() || !adultPayerDeclaration || !checkout.acceptSalesContract">{{ submitting() ? 'Gönderiliyor…' : 'Ödeme yaptım, bildir' }}</button>
              <p class="muted">Bu sayfa kart bilgisi istemez.</p>
            </form>
          </div>
        }
        <ul class="cs-payments-list">
          @for (request of requests(); track request.id) {
            <li class="request">
              <span><strong>{{ request.plan.name }}</strong><small>{{ request.paymentReference }}, {{ request.createdAt | date:'d.MM.yyyy HH:mm' }}@if (request.couponCode) {, {{ request.couponCode }} ile {{ money(request.discountAmount) }} {{ request.currency }} indirim}</small>
                @if (request.reviewNote) {<small>İnceleme notu: {{ request.reviewNote }}</small>}</span>
              <span class="request-actions"><cs-status-chip [label]="statusLabel(request.status)" [tone]="request.status === 'Approved' ? 'success' : request.status === 'Pending' ? 'warning' : 'neutral'" />
                @if (request.status === 'Pending' && editingRequestId() !== request.id) {
                  <button type="button" class="cs-button-ghost" [attr.data-testid]="'edit-request-' + request.id" [disabled]="busyRequestId() === request.id" (click)="startEdit(request)">Düzelt</button>
                  <button type="button" class="cs-button-ghost" [attr.data-testid]="'withdraw-request-' + request.id" [disabled]="busyRequestId() === request.id" (click)="withdraw(request)">Geri çek</button>
                }
              </span>
              @if (editingRequestId() === request.id) {
                <form class="edit" (ngSubmit)="saveEdit()">
                  <label>Banka işlem referansı<input [(ngModel)]="editReference" name="editReference" maxlength="120" required autocomplete="off" /></label>
                  <label>Ödemeyi yapan kişi<input [(ngModel)]="editPayerName" name="editPayerName" maxlength="200" autocomplete="name" /></label>
                  <label>Not<input [(ngModel)]="editNote" name="editNote" maxlength="1000" /></label>
                  <div class="row"><button type="submit" class="cs-button-primary" [disabled]="busyRequestId() === request.id">Kaydet</button><button type="button" class="cs-button-ghost" (click)="editingRequestId.set(null)">Vazgeç</button></div>
                </form>
              }
            </li>
          } @empty { <li class="empty muted">Henüz havale bildirimin yok.</li> }
        </ul>
      </section>

      <ng-content select="[fees]" />

      <section aria-label="Faturalar">
        <edu-coaching-my-payments [api]="sales" heading="Faturalar" />
      </section>
    </div>
  `
})
export class CoachingSubscriptionComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  readonly subscriptionRequired = inject(ActivatedRoute, { optional: true })?.snapshot.queryParamMap.get('required') === '1';
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly plans = signal<CoachingSubscriptionPlan[]>([]);
  readonly settings = signal<CoachingSubscriptionSettings | null>(null);
  readonly access = signal<CoachingSubscriptionAccess | null>(null);
  readonly requests = signal<CoachingBankTransferRequest[]>([]);
  readonly selectedPlanId = signal<string | null>(null);
  paymentReference = '';
  payerName = '';
  note = '';
  adultPayerDeclaration = false;
  /** Roadmap O10: discount code and contract acceptance from the checkout. */
  checkout: CoachingCheckoutChoice = { couponCode: null, acceptSalesContract: false };
  readonly sales = new CoachingSalesApi(inject(HttpClient), environment.apiUrl);
  readonly money = coachingMoney;
  readonly trialPlanId = signal<string | null>(null);
  private readonly checkoutForm = viewChild(CoachingCheckoutComponent);
  private readonly toaster = inject(ToasterService);
  readonly editingRequestId = signal<string | null>(null);
  readonly busyRequestId = signal<string | null>(null);
  editReference = '';
  editPayerName = '';
  editNote = '';

  ngOnInit(): void {
    this.service.getPublicSubscriptionPlans().subscribe({
      next: plans => { this.plans.set(plans.filter(plan => plan.audience === 'Individual' && plan.isActive && plan.isPublic)); this.loading.set(false); },
      error: () => { this.error.set('Koçluk planları yüklenemedi. Lütfen yeniden dene.'); this.loading.set(false); }
    });
    this.service.getPublicBankTransferSettings().subscribe({ next: settings => this.settings.set(settings), error: () => this.settings.set(null) });
    this.service.getMyCoachingSubscriptionAccess().subscribe({ next: access => this.access.set(access), error: () => this.error.set('Koçluk erişim durumun yüklenemedi.') });
    this.loadRequests();
  }

  selectedPlan(): CoachingSubscriptionPlan | null { return this.plans().find(plan => plan.id === this.selectedPlanId()) ?? null; }

  selectPlan(plan: CoachingSubscriptionPlan): void {
    if (plan.isContactOnly) return;
    this.selectedPlanId.set(plan.id);
    this.error.set(null);
    this.success.set(null);
  }

  submitRequest(): void {
    const plan = this.selectedPlan();
    const reference = this.paymentReference.trim();
    if (!plan || plan.isContactOnly || !this.settings()) { this.error.set('Ödeme bildirimi için satışa açık plan ve yayınlanmış banka bilgisi gereklidir.'); return; }
    if (!reference || reference.length > 120) { this.error.set('Banka işlem referansını 1–120 karakter arasında gir.'); return; }
    if (!this.adultPayerDeclaration) { this.error.set('Ödeme için 18 yaş ve üzeri olduğunu beyan etmeniz gerekir.'); return; }
    if (!this.checkout.acceptSalesContract) { this.error.set('Ödeme bildiriminden önce ön bilgilendirme formunu ve mesafeli satış sözleşmesini açıp onaylaman gerekir.'); return; }

    this.submitting.set(true);
    this.error.set(null);
    this.success.set(null);
    this.service.createMyCoachingBankTransferRequest({
      planId: plan.id,
      paymentReference: reference,
      payerName: this.payerName.trim() || null,
      note: this.note.trim() || null,
      adultPayerDeclaration: this.adultPayerDeclaration,
      couponCode: this.checkout.couponCode,
      acceptSalesContract: this.checkout.acceptSalesContract
    }).subscribe({
      next: request => {
        this.requests.update(items => [request, ...items.filter(item => item.id !== request.id)]);
        this.success.set(request.status === 'Pending'
          ? 'Ödeme bildirimin incelemeye alındı. Banka hareketi doğrulandıktan sonra erişimin açılacak.'
          : 'Ödeme bildirimin zaten kayıtlı.');
        this.paymentReference = '';
        this.payerName = '';
        this.note = '';
        this.adultPayerDeclaration = false;
        this.checkoutForm()?.reset();
        this.submitting.set(false);
      },
      error: response => {
        this.error.set(response?.error?.message ?? 'Ödeme bildirimi gönderilemedi. Bilgileri kontrol edip yeniden dene.');
        this.submitting.set(false);
      }
    });
  }

  hasSubscription(): boolean { return (this.access()?.subscriptions?.length ?? 0) > 0; }

  /** Roadmap O10: a plan with trial days opens access right away, once per account. */
  async startTrial(plan: CoachingSubscriptionPlan): Promise<void> {
    if (this.trialPlanId() || !(plan.trialDays ?? 0)) return;
    this.trialPlanId.set(plan.id); this.error.set(null); this.success.set(null);
    try {
      const trial = await firstValueFrom(this.sales.startTrial(plan.id));
      this.success.set(`${plan.trialDays} günlük ücretsiz denemen başladı. Erişimin ${new Date(trial.endDate).toLocaleDateString('tr-TR')} tarihine kadar açık.`);
      this.service.getMyCoachingSubscriptionAccess().subscribe({ next: access => this.access.set(access) });
    } catch (response) {
      this.error.set(coachingErrorMessage(response, 'Ücretsiz deneme başlatılamadı.'));
    } finally { this.trialPlanId.set(null); }
  }

  statusLabel(status: string): string {
    return ({ Pending: 'İnceleniyor', Approved: 'Onaylandı', Rejected: 'Reddedildi', Cancelled: 'İptal edildi', Withdrawn: 'Geri çekildi', Expired: 'Süresi doldu' } as Record<string, string>)[status] ?? status;
  }

  startEdit(request: CoachingBankTransferRequest): void {
    if (request.status !== 'Pending') return;
    this.editingRequestId.set(request.id);
    this.editReference = request.paymentReference; this.editPayerName = request.payerName ?? ''; this.editNote = request.note ?? '';
    this.error.set(null); this.success.set(null);
  }

  async saveEdit(): Promise<void> {
    const id = this.editingRequestId();
    const reference = this.editReference.trim();
    if (!id || this.busyRequestId()) return;
    if (!reference || reference.length > 120) { this.error.set('Banka işlem referansını 1–120 karakter arasında gir.'); return; }
    this.busyRequestId.set(id); this.error.set(null);
    try {
      const updated = await firstValueFrom(this.service.updateMyCoachingBankTransferRequest(id, {
        paymentReference: reference, payerName: this.editPayerName.trim() || null, note: this.editNote.trim() || null
      }));
      this.replaceRequest(updated); this.editingRequestId.set(null);
      this.success.set('Ödeme bildirimin güncellendi.');
    } catch (response) {
      this.error.set((response as { error?: { message?: string } })?.error?.message ?? 'Ödeme bildirimi güncellenemedi.');
    } finally { this.busyRequestId.set(null); }
  }

  async withdraw(request: CoachingBankTransferRequest): Promise<void> {
    if (request.status !== 'Pending' || this.busyRequestId()) return;
    if (!await this.toaster.confirm(`${request.paymentReference} referanslı ödeme bildirimi geri çekilsin mi? Ödemeyi yaptıysan daha sonra yeni bir bildirim gönderebilirsin.`)) return;
    this.busyRequestId.set(request.id); this.error.set(null);
    try {
      this.replaceRequest(await firstValueFrom(this.service.withdrawMyCoachingBankTransferRequest(request.id)));
      if (this.editingRequestId() === request.id) this.editingRequestId.set(null);
      this.success.set('Ödeme bildirimin geri çekildi.');
    } catch (response) {
      this.error.set((response as { error?: { message?: string } })?.error?.message ?? 'Ödeme bildirimi geri çekilemedi.');
    } finally { this.busyRequestId.set(null); }
  }

  private replaceRequest(updated: CoachingBankTransferRequest): void {
    this.requests.update(items => items.map(item => item.id === updated.id ? { ...item, ...updated } : item));
  }

  private loadRequests(): void {
    this.service.getMyCoachingBankTransferRequests().subscribe({
      next: requests => this.requests.set(requests),
      error: () => this.error.set('Ödeme bildirim geçmişin yüklenemedi.')
    });
  }
  private readonly feedback = coachingFeedback(this.success, this.error);
}
