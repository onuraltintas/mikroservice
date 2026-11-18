import { coachingFeedback } from '../../../../../../src/app/features/coaching-portal/coaching-feedback';
import { educationLabel } from '../../../../../../src/app/features/coaching-portal/coaching-education';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { StaffMySubscriptionComponent } from '../../shared/billing/my-subscription.component';
import { CoachingTeacherStudentsService, CoachingTeacherStudent } from './coaching-teacher-students.service';
import { CoachingTeacherSeatSummary, CoachingTeacherSubscriptionService } from './coaching-teacher-subscription.service';

/**
 * The independent teacher's plan and seats. The plan is bought, renewed and paid for in the shared "Aboneliğim" page
 * (billing service); here the teacher gives its seats to the students assigned to them in Koçluk.
 */
@Component({
  selector: 'staff-coaching-teacher-subscription',
  standalone: true,
  imports: [CommonModule, StaffMySubscriptionComponent],
  template: `
    <section class="teacher-plan" aria-labelledby="teacher-plan-title">
      <header class="plan-heading">
        <div>
          <p class="eyebrow">Koçluk, bağımsız öğretmen</p>
          <h2 id="teacher-plan-title">Öğretmen planım</h2>
          <p>Öğretmen planı, yalnızca Koçlukta size atanmış öğrenciler için koltuk sağlar. Kuruma bağlı öğretmenler kurum lisansını kullanır.</p>
        </div>
        <button type="button" class="secondary" (click)="load()" [disabled]="loading()">Yenile</button>
      </header>

      @if (error()) { <div class="message error" role="alert">{{ error() }}</div> }
      @if (success()) { <div class="message success" role="status">{{ success() }}</div> }
      @if (loading()) { <p class="message" role="status">Öğretmen planı bilgileri yükleniyor…</p> }

      <!-- Billing service: the plan is bought, renewed and paid for here; the seats below are given to the teacher's own students. -->
      <section class="card" data-testid="teacher-billing"><staff-my-subscription product="coaching" [roles]="['Teacher']" /></section>

      <section class="card" aria-labelledby="teacher-seats-title">
        <h3 id="teacher-seats-title">Öğrenci kontenjanım</h3>
        @if (seatSummary(); as summary) {
          <p class="capacity"><strong>{{ summary.usedStudentSeats }} / {{ summary.includedStudentSeats }}</strong> öğrenci koltuğu kullanılıyor · {{ summary.planName }} · {{ summary.accessUntil | date:'dd.MM.yyyy' }} tarihine kadar</p>
          @if (students().length > 0) {
            <ul class="student-list">
              @for (student of students(); track student.userId) {
                <li>
                  <span><strong>{{ student.fullName }}</strong><small>{{ educationLabel($any(student).educationStatus, student.gradeLevel, 'Koçluk öğrencisi') }}</small></span>
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
          <p class="muted" data-testid="teacher-no-plan">Aktif bir bağımsız öğretmen planınız yok. Kurumunuza bağlıysanız kurum yöneticinizin lisansını kullanın; bağımsızsanız yukarıdaki Aboneliğim bölümünden bir öğretmen planı alın.</p>
        }
      </section>
    </section>
  `,
  styles: [`
    .teacher-plan { display: grid; gap: 1rem; margin-top: 1rem; color: var(--ui-text); }
    .plan-heading { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; }
    .plan-heading h2 { margin: 0; font: 400 1.55rem var(--ui-font-body); }
    .plan-heading p:not(.eyebrow), .muted { color: var(--ui-text-muted); font-size: .875rem; line-height: 1.55; }
    .eyebrow { margin: 0 0 .4rem; color: var(--ui-text-muted); font-size: .7rem; font-weight: 800; letter-spacing: 0; }
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
  readonly educationLabel = educationLabel;
  private readonly subscriptions = inject(CoachingTeacherSubscriptionService);
  private readonly teacherStudents = inject(CoachingTeacherStudentsService);

  readonly seatSummary = signal<CoachingTeacherSeatSummary | null>(null);
  readonly students = signal<CoachingTeacherStudent[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly studentPage = signal(1);
  readonly studentTotalPages = signal(1);
  readonly studentTotalCount = signal(0);
  private readonly busyStudents = signal<ReadonlySet<string>>(new Set());

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.subscriptions.getMySeatSummary().subscribe({
      next: seats => { this.seatSummary.set(seats); this.loading.set(false); this.loadStudents(); },
      error: () => { this.error.set('Öğretmen kontenjanı yüklenemedi.'); this.seatSummary.set(null); this.loading.set(false); this.loadStudents(); }
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

  async assignStudent(student: CoachingTeacherStudent): Promise<void> {
    const summary = this.seatSummary();
    if (!summary || this.isAtCapacity(summary) || this.isStudentBusy(student.userId)) return;
    await this.updateSeat(student, () => this.subscriptions.assignStudent(student.userId), 'Öğrenci plan kontenjanınıza eklendi.');
  }

  async removeStudent(student: CoachingTeacherStudent): Promise<void> {
    if (this.isStudentBusy(student.userId)) return;
    await this.updateSeat(student, () => this.subscriptions.removeStudent(student.userId), 'Öğrenci için öğretmen planı koltuğu kaldırıldı.');
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
