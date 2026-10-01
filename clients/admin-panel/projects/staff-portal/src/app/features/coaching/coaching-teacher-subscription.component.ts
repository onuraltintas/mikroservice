import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { CoachingTeacherStudentsService, CoachingTeacherStudent } from './coaching-teacher-students.service';
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
  imports: [CommonModule, FormsModule],
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
                <label><input type="radio" name="teacherPlan" [value]="plan.id" [(ngModel)]="selectedPlanId" [disabled]="savingRequest()" />
                  <span><strong>{{ plan.name }}</strong><small>{{ plan.description }}</small><small>{{ plan.includedStudentSeats }} öğrenci koltuğu · {{ plan.durationDays }} gün · {{ plan.isContactOnly ? 'Fiyat için iletişime geçin' : (plan.price | number:'1.2-2') + ' ' + (settings()?.currency || 'TRY') }}</small></span>
                </label>
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
          </div>
          <form class="payment-form" (ngSubmit)="submitRequest()">
            <label>Ödeme referansı<input name="paymentReference" [(ngModel)]="paymentReference" required maxlength="120" autocomplete="off" /></label>
            <label>Ödeyen adı<input name="payerName" [(ngModel)]="payerName" maxlength="200" autocomplete="name" /></label>
            <label>Not<textarea name="paymentNote" [(ngModel)]="paymentNote" maxlength="1000" rows="2"></textarea></label>
            <button type="submit" [disabled]="savingRequest() || !selectedPlanId || selectedPlan?.isContactOnly || !paymentReference.trim() || !settings()?.bankTransferEnabled">{{ savingRequest() ? 'Gönderiliyor…' : 'Ödeme bildirimi gönder' }}</button>
          </form>
        } @else {
          <p class="muted">Havale / EFT bilgileri şu anda yayımlanmıyor; ödeme bildirimi gönderilemez.</p>
        }
      </section>

      <section class="card" aria-labelledby="teacher-payment-history-title">
        <h3 id="teacher-payment-history-title">Ödeme bildirimlerim</h3>
        @for (request of requests(); track request.id) {
          <div class="request-row"><span><strong>{{ request.plan.name }}</strong><small>{{ request.amount | number:'1.2-2' }} {{ request.currency }} · {{ request.paymentReference }} · {{ request.createdAt | date:'dd.MM.yyyy HH:mm' }}</small></span><span>{{ statusLabel(request.status) }} @if (request.reviewNote) {<small>{{ request.reviewNote }}</small>}</span></div>
        } @empty { <p class="muted">Henüz ödeme bildiriminiz yok.</p> }
      </section>
    </section>
  `,
  styles: [`
    .teacher-plan { display: grid; gap: 1rem; margin-top: 1rem; color: #243b36; }
    .plan-heading { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; }
    .plan-heading h2 { margin: 0; font: 400 1.55rem Georgia, 'Times New Roman', serif; }
    .plan-heading p:not(.eyebrow), .muted { color: #687872; font-size: .875rem; line-height: 1.55; }
    .eyebrow { margin: 0 0 .4rem; color: #64876e; font-size: .7rem; font-weight: 800; letter-spacing: .12em; }
    .card { padding: 1rem; border: 1px solid #e0e5db; border-radius: .8rem; background: #fffefa; }
    .card h3 { margin: 0 0 .8rem; font-size: 1rem; }
    .capacity { color: #52645e; font-size: .9rem; }
    .student-list, .plans { display: grid; gap: .5rem; padding: 0; list-style: none; }
    .student-list li, .request-row { display: flex; justify-content: space-between; align-items: center; gap: .75rem; padding: .7rem 0; border-top: 1px solid #e8ece5; }
    small { display: block; margin-top: .2rem; color: #718079; font-size: .78rem; }
    .plans li { padding: .75rem; border: 1px solid #e0e5db; border-radius: .6rem; }
    .plans li.selected { border-color: #64876e; background: #f3f7f1; }
    .plans label { display: flex; align-items: flex-start; gap: .65rem; cursor: pointer; }
    .bank-details { margin-top: 1rem; padding: .8rem; border-radius: .6rem; background: #f3f4ed; }
    .bank-details h4 { margin: 0 0 .4rem; }
    .iban { font-family: monospace; font-size: 1rem; letter-spacing: .03em; }
    .payment-form { display: grid; gap: .7rem; margin-top: 1rem; }
    .payment-form label { display: grid; gap: .25rem; font-size: .85rem; }
    input:not([type=radio]), textarea { width: 100%; border: 1px solid #cbd5cd; border-radius: .45rem; padding: .55rem .7rem; font: inherit; }
    button { min-height: 2.4rem; border: 1px solid #9aa99b; border-radius: .5rem; background: #fffefa; padding: .35rem .7rem; color: #29473b; cursor: pointer; font: inherit; font-size: .83rem; font-weight: 650; }
    button:disabled { cursor: not-allowed; opacity: .55; }
    .payment-form button { justify-self: start; border-color: #29473b; background: #29473b; color: white; }
    .pager { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: .5rem; margin-top: .75rem; color: #687872; font-size: .8rem; }
    .message { padding: .75rem; border-radius: .5rem; background: #f3f4ed; font-size: .85rem; }
    .error { border: 1px solid #fecaca; background: #fef2f2; color: #991b1b; }
    .success { border: 1px solid #bbf7d0; background: #f0fdf4; color: #166534; }
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
      if (!this.selectedPlanId && result.plans.length) this.selectedPlanId = result.plans[0].id;
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
    this.savingRequest.set(true);
    this.error.set(null);
    this.success.set(null);
    try {
      await firstValueFrom(this.subscriptions.createBankTransferRequest({
        planId: this.selectedPlanId,
        paymentReference: this.paymentReference,
        payerName: this.payerName,
        note: this.paymentNote
      }));
      this.paymentReference = '';
      this.payerName = '';
      this.paymentNote = '';
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

  statusLabel(status: string): string {
    return status === 'Pending' ? 'İncelemede' : status === 'Approved' ? 'Onaylandı' : status === 'Rejected' ? 'Reddedildi' : status;
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
}
