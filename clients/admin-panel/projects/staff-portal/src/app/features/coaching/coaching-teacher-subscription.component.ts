import { coachingFeedback } from '../../../../../../src/app/features/coaching-portal/coaching-feedback';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { CoachingTeacherStudentsService, CoachingTeacherStudent } from './coaching-teacher-students.service';
import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { environment } from '../../../environments/environment';
import { CoachingCheckoutComponent } from '../../../../../../shared/coaching-checkout.component';
import { CoachingMyPaymentsComponent } from '../../../../../../shared/coaching-my-payments.component';
import { CoachingCheckoutChoice, CoachingSalesApi, coachingErrorMessage, coachingMoney } from '../../../../../../shared/coaching-sales.api';
import {
  CoachingTeacherBankTransferRequest,
  CoachingTeacherBankTransferSettings,
  CoachingTeacherSeatSummary,
  CoachingTeacherSubscriptionPlan,
  CoachingTeacherSubscriptionService
} from './coaching-teacher-subscription.service';

@Component({
  selector: 'staff-coaching-teacher-subscription',
  standalone: true,
  imports: [CoachingCheckoutComponent, CoachingMyPaymentsComponent, CommonModule, FormsModule],
  template: `
    <section class="teacher-plan" aria-labelledby="teacher-plan-title">
      <header class="plan-heading">
        <div>
          <p class="eyebrow">KOÇLUK · BAĞIMSIZ ÖĞRETMEN</p>
          <h2 id="teacher-plan-title">Öğretmen planım</h2>
          <p>Öğretmen planı, yalnızca Koçlukta size atanmış öğrenciler için koltuk sağlar. Kuruma bağlı öğretmenler kurum lisansını kullanır.</p>
        </div>
        <button type="button" class="secondary" (click)="load()" [disabled]="loading()">Yenile</button>
      </header>

      @if (error()) { <div class="message error" role="alert">{{ error() }}</div> }
      @if (success()) { <div class="message success" role="status">{{ success() }}</div> }
      @if (loading()) { <p class="message" role="status">Öğretmen planı bilgileri yükleniyor…</p> }

      <section class="card" aria-labelledby="teacher-seats-title">
        <h3 id="teacher-seats-title">Öğrenci kontenjanım</h3>
        @if (seatSummary(); as summary) {
          <p class="capacity"><strong>{{ summary.usedStudentSeats }} / {{ summary.includedStudentSeats }}</strong> öğrenci koltuğu kullanılıyor · {{ summary.planName }} · {{ summary.accessUntil | date:'dd.MM.yyyy' }} tarihine kadar</p>
          @if (students().length > 0) {
            <ul class="student-list">
              @for (student of students(); track student.userId) {
                <li>
                  <span><strong>{{ student.fullName }}</strong><small>{{ student.gradeLevel ? student.gradeLevel + '. sınıf' : 'Koçluk öğrencisi' }}</small></span>
                  @if (isSeatActive(student.userId)) {
                    <button type="button" class="secondary" [attr.data-testid]="'teacher-seat-' + student.userId" [disabled]="isStudentBusy(student.userId)" (click)="removeStudent(student)">Kontenjanı kaldır</button>
                  } @else {
                    <button type="button" class="secondary" [attr.data-testid]="'teacher-seat-' + student.userId" [disabled]="isStudentBusy(student.userId) || isAtCapacity(summary)" (click)="assignStudent(student)">{{ isStudentBusy(student.userId) ? 'İşleniyor…' : 'Koltuk ata' }}</button>
                  }
                </li>
              }
            </ul>
            <div class="pager">
              <button type="button" class="secondary" (click)="changeStudentPage(-1)" [disabled]="studentPage() <= 1">Önceki</button>
              <span>Sayfa {{ studentPage() }} / {{ studentTotalPages() }} · {{ studentTotalCount() }} atanmış öğrenci</span>
              <button type="button" class="secondary" (click)="changeStudentPage(1)" [disabled]="studentPage() >= studentTotalPages()">Sonraki</button>
            </div>
          } @else {
            <p class="muted">Koçlukta size atanmış öğrenci yok. Öğrenci ataması oluştuğunda burada kontenjan verebilirsiniz.</p>
          }
        } @else {
          <p class="muted">Aktif bir bağımsız öğretmen planınız yok. Kurumunuza bağlıysanız kurum yöneticinizin lisansını kullanın; bağımsızsanız aşağıdaki planlardan birini seçin.</p>
        }
      </section>

      <section class="card" aria-labelledby="teacher-plans-title">
        <h3 id="teacher-plans-title">Öğretmen planları</h3>
        @if (plans().length === 0) {
          <p class="muted">Şu anda yayımlanmış öğretmen planı bulunmuyor.</p>
        } @else {
          <ul class="plans">
            @for (plan of plans(); track plan.id) {
              <li [class.selected]="selectedPlanId === plan.id">
                <label><input type="radio" name="teacherPlan" [attr.data-testid]="'teacher-plan-' + plan.id" [value]="plan.id" [(ngModel)]="selectedPlanId" [disabled]="savingRequest() || isSmallerPlan(plan)" />
                  <span><strong>{{ plan.name }}</strong>@if (planChangeLabel(plan); as change) { <em class="plan-tag">{{ change }}</em> }<small>{{ plan.description }}</small><small>{{ plan.includedStudentSeats }} öğrenci koltuğu · {{ plan.durationDays }} gün · {{ plan.isContactOnly ? 'Fiyat için iletişime geçin' : (plan.price | number:'1.2-2') + ' ' + (settings()?.currency || 'TRY') }}</small></span>
                </label>
                @if ((plan.trialDays ?? 0) > 0 && !plan.isContactOnly && !seatSummary()) {
                  <button type="button" class="secondary trial" [attr.data-testid]="'teacher-trial-' + plan.id" [disabled]="trialPlanId() !== null" (click)="startTrial(plan)">{{ trialPlanId() === plan.id ? 'Başlatılıyor…' : plan.trialDays + ' gün ücretsiz deneyin' }}</button>
                }
              </li>
            }
          </ul>
        }

        @if (settings()?.bankTransferEnabled) {
          <div class="bank-details">
            <h4>Havale / EFT bilgileri</h4>
            <p><strong>{{ settings()?.accountHolder }}</strong> · {{ settings()?.bankName }}</p>
            <p class="iban">{{ settings()?.iban }}</p>
            @if (settings()?.paymentInstructions) { <p>{{ settings()?.paymentInstructions }}</p> }
            @if (seatSummary()) { <p class="muted">Planınız sürerken aynı planı yenileyebilir veya daha fazla koltuklu bir plana geçebilirsiniz. Yükseltme onaylanınca koltuklarınız korunur, yeni süre mevcut sürenin sonuna eklenir.</p> }
          </div>
          <form class="payment-form" (ngSubmit)="submitRequest()">
            <label>Ödeme referansı<input name="paymentReference" [(ngModel)]="paymentReference" required maxlength="120" autocomplete="off" /></label>
            <label>Ödeyen adı<input name="payerName" [(ngModel)]="payerName" maxlength="200" autocomplete="name" /></label>
            <label>Not<textarea name="paymentNote" [(ngModel)]="paymentNote" maxlength="1000" rows="2"></textarea></label>
            <edu-coaching-checkout [api]="sales" [planId]="selectedPlanId" [price]="selectedPlan?.price ?? 0" [currency]="settings()?.currency || 'TRY'" [formal]="true" (choiceChange)="checkout = $event" />
            <label><input type="checkbox" name="adultPayerDeclaration" [(ngModel)]="adultPayerDeclaration" required />Ödemeyi yapan kişi olarak 18 yaş ve üzeri olduğumu beyan ediyorum. Bu beyan yaş doğrulaması değildir.</label>
            <button type="submit" [disabled]="savingRequest() || !selectedPlanId || selectedPlan?.isContactOnly || !paymentReference.trim() || !settings()?.bankTransferEnabled || !adultPayerDeclaration || !checkout.acceptSalesContract">{{ savingRequest() ? 'Gönderiliyor…' : 'Ödeme bildirimi gönder' }}</button>
          </form>
        } @else {
          <p class="muted">Havale / EFT bilgileri şu anda yayımlanmıyor; ödeme bildirimi gönderilemez.</p>
        }
      </section>

      <section class="card" aria-labelledby="teacher-payment-history-title">
        <h3 id="teacher-payment-history-title">Ödeme bildirimlerim</h3>
        @for (request of requests(); track request.id) {
          <div class="request-row"><span><strong>{{ request.plan.name }}</strong><small>{{ request.amount | number:'1.2-2' }} {{ request.currency }} · {{ request.paymentReference }} · {{ request.createdAt | date:'dd.MM.yyyy HH:mm' }}@if (request.couponCode) { · {{ request.couponCode }} ile {{ money(request.discountAmount) }} {{ request.currency }} indirim }</small></span><span>{{ statusLabel(request.status) }} @if (request.reviewNote) {<small>{{ request.reviewNote }}</small>}
            @if (request.status === 'Pending' && editingRequestId() !== request.id) { <span class="request-actions"><button type="button" class="secondary" [attr.data-testid]="'edit-request-' + request.id" [disabled]="busyRequestId() === request.id" (click)="startEdit(request)">Düzelt</button><button type="button" class="secondary" [attr.data-testid]="'withdraw-request-' + request.id" [disabled]="busyRequestId() === request.id" (click)="withdraw(request)">Geri çek</button></span> }</span></div>
          @if (editingRequestId() === request.id) {
            <form class="payment-form" (ngSubmit)="saveEdit()">
              <label>Ödeme referansı<input name="editReference" [(ngModel)]="editReference" required maxlength="120" autocomplete="off" /></label>
              <label>Ödeyen adı<input name="editPayerName" [(ngModel)]="editPayerName" maxlength="200" autocomplete="name" /></label>
              <label>Not<input name="editNote" [(ngModel)]="editNote" maxlength="1000" /></label>
              <span class="request-actions"><button type="submit" [disabled]="busyRequestId() === request.id">Kaydet</button><button type="button" class="secondary" (click)="editingRequestId.set(null)">Vazgeç</button></span>
            </form>
          }
        } @empty { <p class="muted">Henüz ödeme bildiriminiz yok.</p> }
      </section>

      <section class="card" aria-label="Onaylanan ödemelerim">
        <edu-coaching-my-payments [api]="sales" [formal]="true" heading="Ödemelerim, faturalar ve cayma" />
      </section>
    </section>
  `,
  styles: [`
    .teacher-plan { display: grid; gap: 1rem; margin-top: 1rem; color: var(--ui-text); }
    .plan-heading { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; }
    .plan-heading h2 { margin: 0; font: 400 1.55rem var(--ui-font-body); }
    .plan-heading p:not(.eyebrow), .muted { color: var(--ui-text-muted); font-size: .875rem; line-height: 1.55; }
    .eyebrow { margin: 0 0 .4rem; color: var(--ui-text-muted); font-size: .7rem; font-weight: 800; letter-spacing: .12em; }
    .card { padding: 1rem; border: 1px solid var(--ui-border); border-radius: .8rem; background: var(--ui-surface); }
    .card h3 { margin: 0 0 .8rem; font-size: 1rem; }
    .capacity { color: var(--ui-text); font-size: .9rem; }
    .student-list, .plans { display: grid; gap: .5rem; padding: 0; list-style: none; }
    .student-list li, .request-row { display: flex; justify-content: space-between; align-items: center; gap: .75rem; padding: .7rem 0; border-top: 1px solid var(--ui-border); }
    small { display: block; margin-top: .2rem; color: var(--ui-text-muted); font-size: .78rem; }
    .plans li { padding: .75rem; border: 1px solid var(--ui-border); border-radius: .6rem; }
    .plans li.selected { border-color: var(--ui-border); background: var(--ui-surface-muted); }
    .plans label { display: flex; align-items: flex-start; gap: .65rem; cursor: pointer; }
    .bank-details { margin-top: 1rem; padding: .8rem; border-radius: .6rem; background: var(--ui-surface); }
    .bank-details h4 { margin: 0 0 .4rem; }
    .iban { font-family: inherit; font-size: 1rem; letter-spacing: .03em; }
    .payment-form { display: grid; gap: .7rem; margin-top: 1rem; }
    .payment-form label { display: grid; gap: .25rem; font-size: .85rem; }
    input:not([type=radio]), textarea { width: 100%; border: 1px solid var(--ui-border); border-radius: .45rem; padding: .55rem .7rem; font: inherit; }
    button { min-height: 2.4rem; border: 1px solid var(--ui-border); border-radius: .5rem; background: var(--ui-surface); padding: .35rem .7rem; color: var(--ui-text); cursor: pointer; font: inherit; font-size: .83rem; font-weight: 650; }
    button:disabled { cursor: not-allowed; opacity: .55; }
    .payment-form button { justify-self: start; border-color: var(--ui-border); background: var(--ui-brand); color: white; }
    .pager { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: .5rem; margin-top: .75rem; color: var(--ui-text); font-size: .8rem; }
    .message { padding: .75rem; border-radius: .5rem; background: var(--ui-surface); font-size: .85rem; }
    .error { border: 1px solid var(--ui-border); background: var(--coaching-danger-soft); color: var(--ui-danger); }
    .success { border: 1px solid var(--ui-border); background: var(--ui-brand-soft); color: var(--ui-success); }
    .plan-tag { margin-left: .4rem; padding: .05rem .4rem; border-radius: 999px; background: var(--ui-surface-muted); font-size: .72rem; font-style: normal; font-weight: 700; }
    .plans .trial { margin: .5rem 0 0 1.6rem; }
    .request-actions { display: inline-flex; flex-wrap: wrap; gap: .4rem; margin-left: .5rem; }
    @media (max-width: 640px) { .plan-heading, .student-list li, .request-row { align-items: stretch; flex-direction: column; } }
  `]
})
export class CoachingTeacherSubscriptionComponent implements OnInit {
  private readonly subscriptions = inject(CoachingTeacherSubscriptionService);
  private readonly teacherStudents = inject(CoachingTeacherStudentsService);

  readonly plans = signal<CoachingTeacherSubscriptionPlan[]>([]);
  readonly settings = signal<CoachingTeacherBankTransferSettings | null>(null);
  readonly requests = signal<CoachingTeacherBankTransferRequest[]>([]);
  readonly seatSummary = signal<CoachingTeacherSeatSummary | null>(null);
  readonly students = signal<CoachingTeacherStudent[]>([]);
  readonly loading = signal(false);
  readonly savingRequest = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly studentPage = signal(1);
  readonly studentTotalPages = signal(1);
  readonly studentTotalCount = signal(0);
  private readonly busyStudents = signal<ReadonlySet<string>>(new Set());
  selectedPlanId = '';
  paymentReference = '';
  payerName = '';
  paymentNote = '';
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
    this.load();
  }

  get selectedPlan(): CoachingTeacherSubscriptionPlan | null {
    return this.plans().find(plan => plan.id === this.selectedPlanId) ?? null;
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      plans: this.subscriptions.getTeacherPlans().pipe(catchError(() => { this.error.set('Öğretmen planları yüklenemedi.'); return of([]); })),
      settings: this.subscriptions.getBankTransferSettings().pipe(catchError(() => of(null))),
      requests: this.subscriptions.getMyBankTransferRequests().pipe(catchError(() => { this.error.set('Ödeme bildirimleriniz yüklenemedi.'); return of([]); })),
      seats: this.subscriptions.getMySeatSummary().pipe(catchError(() => { this.error.set('Öğretmen kontenjanı yüklenemedi.'); return of(null); }))
    }).subscribe(result => {
      this.plans.set(result.plans);
      this.settings.set(result.settings);
      this.requests.set(result.requests);
      this.seatSummary.set(result.seats);
      if (!this.selectedPlanId && result.plans.length) this.selectedPlanId = (result.plans.find(plan => !this.isSmallerPlan(plan)) ?? result.plans[0]).id;
      this.loading.set(false);
      this.loadStudents();
    });
  }

  loadStudents(): void {
    this.teacherStudents.getMyStudents(this.studentPage(), 25).subscribe({
      next: page => {
        this.students.set(page.items);
        this.studentTotalCount.set(page.totalCount);
        this.studentTotalPages.set(Math.max(1, page.totalPages ?? Math.ceil(page.totalCount / Math.max(1, page.pageSize))));
      },
      error: () => this.error.set('Size atanmış Koçluk öğrencileri yüklenemedi.')
    });
  }

  changeStudentPage(delta: number): void {
    const next = Math.min(this.studentTotalPages(), Math.max(1, this.studentPage() + delta));
    if (next === this.studentPage()) return;
    this.studentPage.set(next);
    this.loadStudents();
  }

  isSeatActive(studentId: string): boolean {
    return this.seatSummary()?.students.some(seat => seat.studentId === studentId && !seat.isSuspended) ?? false;
  }

  isAtCapacity(summary: CoachingTeacherSeatSummary): boolean {
    return summary.usedStudentSeats >= summary.includedStudentSeats;
  }

  isStudentBusy(studentId: string): boolean {
    return this.busyStudents().has(studentId);
  }

  async submitRequest(): Promise<void> {
    if (!this.selectedPlanId || this.selectedPlan?.isContactOnly || !this.paymentReference.trim() || !this.settings()?.bankTransferEnabled || this.savingRequest()) return;
    if (!this.adultPayerDeclaration) { this.error.set('Ödeme için 18 yaş ve üzeri olduğunuzu beyan etmeniz gerekir.'); return; }
    if (!this.checkout.acceptSalesContract) { this.error.set('Ödeme bildiriminden önce ön bilgilendirme formunu ve mesafeli satış sözleşmesini açıp onaylamanız gerekir.'); return; }
    this.savingRequest.set(true);
    this.error.set(null);
    this.success.set(null);
    try {
      await firstValueFrom(this.subscriptions.createBankTransferRequest({
        planId: this.selectedPlanId,
        paymentReference: this.paymentReference,
        payerName: this.payerName,
        note: this.paymentNote,
        adultPayerDeclaration: this.adultPayerDeclaration,
        couponCode: this.checkout.couponCode,
        acceptSalesContract: this.checkout.acceptSalesContract
      }));
      this.paymentReference = '';
      this.payerName = '';
      this.paymentNote = '';
      this.adultPayerDeclaration = false;
      this.checkoutForm()?.reset();
      this.success.set('Ödeme bildiriminiz alındı. Onay sonrasında öğretmen planınız etkinleşir.');
      this.subscriptions.getMyBankTransferRequests().subscribe({ next: requests => this.requests.set(requests) });
    } catch (error) {
      this.error.set(this.errorMessage(error, 'Ödeme bildirimi gönderilemedi. Bilgileri kontrol edip tekrar deneyin.'));
    } finally {
      this.savingRequest.set(false);
    }
  }

  async assignStudent(student: CoachingTeacherStudent): Promise<void> {
    const summary = this.seatSummary();
    if (!summary || this.isAtCapacity(summary) || this.isStudentBusy(student.userId)) return;
    await this.updateSeat(student, () => this.subscriptions.assignStudent(student.userId), 'Öğrenci plan kontenjanınıza eklendi.');
  }

  async removeStudent(student: CoachingTeacherStudent): Promise<void> {
    if (this.isStudentBusy(student.userId)) return;
    await this.updateSeat(student, () => this.subscriptions.removeStudent(student.userId), 'Öğrenci için öğretmen planı koltuğu kaldırıldı.');
  }

  /** Roadmap O10: a teacher plan with trial days opens the plan right away, once per account. */
  async startTrial(plan: CoachingTeacherSubscriptionPlan): Promise<void> {
    if (this.trialPlanId() || !(plan.trialDays ?? 0)) return;
    this.trialPlanId.set(plan.id); this.error.set(null); this.success.set(null);
    try {
      const trial = await firstValueFrom(this.sales.startTrial(plan.id, 'Teacher'));
      this.success.set(`${plan.trialDays} günlük ücretsiz denemeniz başladı. Planınız ${new Date(trial.endDate).toLocaleDateString('tr-TR')} tarihine kadar açık.`);
      this.load();
    } catch (error) {
      this.error.set(coachingErrorMessage(error, 'Ücretsiz deneme başlatılamadı.'));
    } finally { this.trialPlanId.set(null); }
  }

  statusLabel(status: string): string {
    return ({ Pending: 'İncelemede', Approved: 'Onaylandı', Rejected: 'Reddedildi', Cancelled: 'İptal edildi', Withdrawn: 'Geri çekildi', Expired: 'Süresi doldu' } as Record<string, string>)[status] ?? status;
  }

  /** While a plan runs only the same plan or a larger one can be bought; a smaller one would take seats away. */
  isSmallerPlan(plan: CoachingTeacherSubscriptionPlan): boolean {
    const current = this.seatSummary();
    if (!current) return false;
    const seats = plan.includedStudentSeats ?? 0;
    return seats < current.includedStudentSeats || (seats === current.includedStudentSeats && plan.name !== current.planName);
  }

  planChangeLabel(plan: CoachingTeacherSubscriptionPlan): string {
    const current = this.seatSummary();
    if (!current) return '';
    if (this.isSmallerPlan(plan)) return 'Mevcut planınızdan küçük';
    return (plan.includedStudentSeats ?? 0) > current.includedStudentSeats ? 'Yükseltme' : 'Yenileme';
  }

  startEdit(request: CoachingTeacherBankTransferRequest): void {
    if (request.status !== 'Pending') return;
    this.editingRequestId.set(request.id);
    this.editReference = request.paymentReference; this.editPayerName = request.payerName ?? ''; this.editNote = request.note ?? '';
    this.error.set(null); this.success.set(null);
  }

  async saveEdit(): Promise<void> {
    const id = this.editingRequestId();
    if (!id || this.busyRequestId()) return;
    if (!this.editReference.trim()) { this.error.set('Ödeme referansı zorunludur.'); return; }
    this.busyRequestId.set(id); this.error.set(null);
    try {
      const updated = await firstValueFrom(this.subscriptions.updateBankTransferRequest(id, {
        paymentReference: this.editReference.trim(), payerName: this.editPayerName.trim() || null, note: this.editNote.trim() || null
      }));
      this.replaceRequest(updated); this.editingRequestId.set(null);
      this.success.set('Ödeme bildiriminiz güncellendi.');
    } catch (error) {
      this.error.set(this.errorMessage(error, 'Ödeme bildirimi güncellenemedi.'));
    } finally { this.busyRequestId.set(null); }
  }

  async withdraw(request: CoachingTeacherBankTransferRequest): Promise<void> {
    if (request.status !== 'Pending' || this.busyRequestId()) return;
    if (!await this.toaster.confirm(`${request.paymentReference} referanslı ödeme bildirimi geri çekilsin mi?`)) return;
    this.busyRequestId.set(request.id); this.error.set(null);
    try {
      this.replaceRequest(await firstValueFrom(this.subscriptions.withdrawBankTransferRequest(request.id)));
      if (this.editingRequestId() === request.id) this.editingRequestId.set(null);
      this.success.set('Ödeme bildiriminiz geri çekildi.');
    } catch (error) {
      this.error.set(this.errorMessage(error, 'Ödeme bildirimi geri çekilemedi.'));
    } finally { this.busyRequestId.set(null); }
  }

  private replaceRequest(updated: CoachingTeacherBankTransferRequest): void {
    this.requests.update(items => items.map(item => item.id === updated.id ? { ...item, ...updated } : item));
  }

  private async updateSeat(student: CoachingTeacherStudent, request: () => ReturnType<CoachingTeacherSubscriptionService['assignStudent']>, message: string): Promise<void> {
    const busy = new Set(this.busyStudents());
    busy.add(student.userId);
    this.busyStudents.set(busy);
    this.error.set(null);
    this.success.set(null);
    try {
      await firstValueFrom(request());
      this.success.set(message);
      const summary = await firstValueFrom(this.subscriptions.getMySeatSummary());
      this.seatSummary.set(summary);
    } catch (error) {
      this.error.set(this.errorMessage(error, 'Öğrenci kontenjanı güncellenemedi.'));
    } finally {
      const nextBusy = new Set(this.busyStudents());
      nextBusy.delete(student.userId);
      this.busyStudents.set(nextBusy);
    }
  }

  private errorMessage(error: unknown, fallback: string): string {
    if (!(error instanceof HttpErrorResponse) || !error.error || typeof error.error !== 'object') return fallback;
    const body = error.error as { message?: unknown };
    return typeof body.message === 'string' && body.message.trim() ? body.message : fallback;
  }

  private readonly errorFeedback = coachingFeedback(undefined, this.error);
  private readonly successFeedback = coachingFeedback(this.success);
}
