import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, catchError, firstValueFrom, forkJoin, of } from 'rxjs';
import {
  CoachingBankTransferRequest,
  CoachingManagementService,
  CoachingSubscriptionAccess,
  CoachingSubscriptionPlan,
  CoachingSubscriptionSettings
} from '../../../core/services/coaching-management.service';
import { CoachingSession, ExamResult, StudentAssignment, StudentProgressSummary } from '../../../core/services/coaching-portal.service';
import { CoachingDatePipe, CoachingLabelPipe } from '../coaching-labels';
import { CoachingParentService, ParentFeeAccount, ParentLink } from './coaching-parent.service';
import { apiMessage } from './parent-home.component';
import { StudentScoreEstimateComponent } from '../pages/student-score-estimate.component';

/**
 * Roadmap Y7: a parent's read-only view of one linked child — progress, exams, assignments, sessions, fees and the
 * Coaching subscription, plus a bank-transfer notice the parent can send for the child.
 */
@Component({
  selector: 'app-parent-child',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CoachingDatePipe, CoachingLabelPipe, StudentScoreEstimateComponent],
  template: `
    <main class="space-y-6" aria-labelledby="parent-child-title">
      <a routerLink="/coaching-portal/parent" class="text-sm text-indigo-600 dark:text-indigo-300">← Çocuklarım</a>
      @if (notLinked()) {
        <div role="alert" class="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-100">Bu öğrenciyle doğrulanmış bir veli bağlantın yok. Çocuğunun bağlantı isteğini onaylamasını bekle.</div>
      } @else if (link(); as child) {
        <header><p class="text-sm font-medium text-indigo-600 dark:text-indigo-300">Veli paneli · salt okunur</p><h1 id="parent-child-title" class="mt-1 text-2xl font-bold">{{ child.studentName }}</h1></header>
        @if (error()) { <div role="alert" class="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-200">{{ error() }}</div> }
        @if (success()) { <div role="status" class="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800 dark:border-emerald-900/50 dark:bg-emerald-950/30 dark:text-emerald-200">{{ success() }}</div> }

        @if (progress(); as p) {
          <section class="grid gap-3 sm:grid-cols-3" aria-label="Özet">
            <div class="rounded-2xl border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900"><p class="text-sm text-slate-500">Ödevler</p><strong class="text-xl">{{ p.submittedAssignments }} / {{ p.totalAssignments }}</strong><p class="text-xs text-slate-500">teslim edildi</p></div>
            <div class="rounded-2xl border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900"><p class="text-sm text-slate-500">Seanslar</p><strong class="text-xl">{{ p.attendedSessions }} / {{ p.totalSessions }}</strong><p class="text-xs text-slate-500">katıldı · {{ p.upcomingSessions }} yaklaşan</p></div>
            <div class="rounded-2xl border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900"><p class="text-sm text-slate-500">Denemeler</p><strong class="text-xl">{{ p.totalExams }}</strong><p class="text-xs text-slate-500">kayıtlı sonuç</p></div>
          </section>
        }

        <section class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" aria-labelledby="exams-heading">
          <h2 id="exams-heading" class="font-semibold">Son denemeler</h2>
          @for (exam of exams(); track exam.examId) { <p class="mt-2 text-sm"><strong>{{ exam.examTitle }}</strong> · {{ exam.examDate | coachingDate }} · {{ exam.score }} / {{ exam.maxScore }}</p> }
          @empty { <p class="mt-2 text-sm text-slate-500">{{ sectionError()['exams'] || 'Henüz deneme sonucu yok.' }}</p> }
        </section>
        <app-student-score-estimate [studentId]="childId" />
        <section class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" aria-labelledby="assignments-heading">
          <h2 id="assignments-heading" class="font-semibold">Ödevler</h2>
          @for (assignment of assignments(); track assignment.id) { <p class="mt-2 text-sm"><strong>{{ assignment.title }}</strong> · son gün {{ assignment.dueDate | coachingDate }} · {{ assignment.status | coachingLabel }}@if (assignment.isOverdue) { · <span class="text-red-600">gecikti</span> }</p> }
          @empty { <p class="mt-2 text-sm text-slate-500">{{ sectionError()['assignments'] || 'Ödev yok.' }}</p> }
        </section>
        <section class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" aria-labelledby="sessions-heading">
          <h2 id="sessions-heading" class="font-semibold">Seanslar</h2>
          @for (session of sessions(); track session.id) { <p class="mt-2 text-sm"><strong>{{ session.subject || 'Koçluk seansı' }}</strong> · {{ session.startTime | coachingDate }} · {{ session.type | coachingLabel }} · {{ session.status | coachingLabel }}</p> }
          @empty { <p class="mt-2 text-sm text-slate-500">{{ sectionError()['sessions'] || 'Seans yok.' }}</p> }
        </section>
        <section class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" aria-labelledby="fees-heading">
          <h2 id="fees-heading" class="font-semibold">Koçluk ücretleri</h2>
          @if (fees(); as account) {
            <p class="mt-2 text-sm">Kalan borç: <strong>{{ account.balance | number:'1.2-2' }} TL</strong>@if (account.overdueAmount > 0) { · gecikmiş: <strong class="text-red-600">{{ account.overdueAmount | number:'1.2-2' }} TL</strong> }</p>
            @for (fee of account.agreements; track fee.id) { <p class="mt-1 text-sm text-slate-600 dark:text-slate-300">{{ fee.title }} · {{ fee.totalAmount | number:'1.2-2' }} TL</p> }
          } @else { <p class="mt-2 text-sm text-slate-500">{{ sectionError()['fees'] || 'Ücret kaydı yok.' }}</p> }
        </section>

        <section class="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900" aria-labelledby="subscription-heading" data-testid="parent-subscription">
          <h2 id="subscription-heading" class="font-semibold">Koçluk aboneliği</h2>
          @if (access(); as current) {
            @if (current.subscriptions.length) {
              @for (subscription of current.subscriptions; track subscription.id) { <p class="mt-2 text-sm"><strong>{{ subscription.plan.name }}</strong> · {{ subscription.endDate | coachingDate }} tarihine kadar</p> }
            } @else if (current.enforcementEnabled) {
              <p class="mt-2 text-sm text-amber-800 dark:text-amber-200">Çocuğunun etkin bir Koçluk aboneliği yok. Aşağıdan onun adına ödeme bildirimi gönderebilirsin.</p>
            } @else { <p class="mt-2 text-sm text-slate-600 dark:text-slate-300">Koçluk erişimi açık; abonelik zorunluluğu şu an etkin değil.</p> }
          } @else { <p class="mt-2 text-sm text-slate-500">{{ sectionError()['access'] || 'Abonelik bilgisi yükleniyor…' }}</p> }
          @for (transfer of transfers(); track transfer.id) { <p class="mt-2 text-sm text-slate-600 dark:text-slate-300">{{ transfer.plan.name }} · {{ transfer.paymentReference }} · {{ transfer.status | coachingLabel }}</p> }

          @if (plans().length && settings()) {
            <form class="mt-4 grid gap-3 border-t border-slate-200 pt-4 sm:grid-cols-2 dark:border-slate-800" (ngSubmit)="sendTransfer()">
              <p class="text-sm sm:col-span-2">Havale/EFT: <strong>{{ settings()?.accountHolder }}</strong> · {{ settings()?.bankName }} · <span class="font-mono">{{ settings()?.iban }}</span></p>
              <label class="text-sm">Plan<select class="mt-1 w-full rounded-lg border border-slate-300 bg-transparent px-3 py-2 dark:border-slate-700" [(ngModel)]="planId" name="planId" required>@for (plan of plans(); track plan.id) { <option [value]="plan.id">{{ plan.name }} · {{ plan.price | number:'1.2-2' }} {{ settings()?.currency || 'TRY' }}</option> }</select></label>
              <label class="text-sm">Banka işlem referansı<input class="mt-1 w-full rounded-lg border border-slate-300 bg-transparent px-3 py-2 dark:border-slate-700" [(ngModel)]="paymentReference" name="paymentReference" maxlength="120" required autocomplete="off" /></label>
              <label class="text-sm">Ödemeyi yapan<input class="mt-1 w-full rounded-lg border border-slate-300 bg-transparent px-3 py-2 dark:border-slate-700" [(ngModel)]="payerName" name="payerName" maxlength="200" autocomplete="name" /></label>
              <label class="text-sm">Not<input class="mt-1 w-full rounded-lg border border-slate-300 bg-transparent px-3 py-2 dark:border-slate-700" [(ngModel)]="note" name="note" maxlength="1000" /></label>
              <label class="flex items-start gap-2 text-sm sm:col-span-2"><input type="checkbox" [(ngModel)]="adultPayerDeclaration" name="adultPayerDeclaration" />Ödemeyi yapan kişi olarak 18 yaş ve üzeri olduğumu beyan ediyorum.</label>
              <button type="submit" class="rounded-lg bg-indigo-600 px-4 py-2 font-medium text-white disabled:opacity-50 sm:col-span-2" [disabled]="sending()">{{ sending() ? 'Gönderiliyor…' : 'Ödeme yaptım, bildir' }}</button>
            </form>
          }
        </section>
      }
    </main>
  `
})
export class ParentChildComponent implements OnInit {
  private readonly service = inject(CoachingParentService);
  private readonly management = inject(CoachingManagementService);
  private readonly studentId = inject(ActivatedRoute).snapshot.paramMap.get('studentId') ?? '';
  get childId(): string { return this.studentId; }
  readonly link = signal<ParentLink | null>(null);
  readonly notLinked = signal(false);
  readonly loaded = signal(false);
  readonly progress = signal<StudentProgressSummary | null>(null);
  readonly exams = signal<ExamResult[]>([]);
  readonly assignments = signal<StudentAssignment[]>([]);
  readonly sessions = signal<CoachingSession[]>([]);
  readonly fees = signal<ParentFeeAccount | null>(null);
  readonly access = signal<CoachingSubscriptionAccess | null>(null);
  readonly transfers = signal<CoachingBankTransferRequest[]>([]);
  readonly plans = signal<CoachingSubscriptionPlan[]>([]);
  readonly settings = signal<CoachingSubscriptionSettings | null>(null);
  readonly sectionError = signal<Record<string, string>>({});
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly sending = signal(false);
  planId = '';
  paymentReference = '';
  payerName = '';
  note = '';
  adultPayerDeclaration = false;

  ngOnInit(): void {
    this.service.getLinks().subscribe({
      next: links => {
        const link = links.find(item => item.studentUserId === this.studentId && item.status === 'Verified') ?? null;
        this.link.set(link); this.notLinked.set(!link);
        if (link) this.loadChild();
      },
      error: () => this.error.set('Veli bağlantıların yüklenemedi.')
    });
  }

  async sendTransfer(): Promise<void> {
    const reference = this.paymentReference.trim();
    if (!this.planId || !reference) { this.error.set('Plan ve banka işlem referansı gerekli.'); return; }
    if (!this.adultPayerDeclaration) { this.error.set('Ödeme için 18 yaş ve üzeri olduğunu beyan etmeniz gerekir.'); return; }
    this.sending.set(true); this.error.set(null); this.success.set(null);
    try {
      const sent = await firstValueFrom(this.service.sendTransfer(this.studentId, {
        planId: this.planId, paymentReference: reference, payerName: this.payerName.trim() || null, note: this.note.trim() || null,
        adultPayerDeclaration: true
      }));
      this.transfers.update(items => [sent, ...items.filter(item => item.id !== sent.id)]);
      this.paymentReference = ''; this.note = ''; this.adultPayerDeclaration = false;
      this.success.set('Ödeme bildirimin incelemeye alındı. Onaylanınca çocuğunun erişimi açılır.');
    } catch (response) {
      this.error.set(apiMessage(response, 'Ödeme bildirimi gönderilemedi.'));
    } finally { this.sending.set(false); }
  }

  private loadChild(): void {
    const id = this.studentId;
    const safe = <T>(name: string, message: string, source: Observable<T>, fallback: T) => source.pipe(catchError(() => {
      this.sectionError.update(errors => ({ ...errors, [name]: message })); return of(fallback);
    }));
    forkJoin({
      progress: safe('progress', 'Özet yüklenemedi.', this.service.getProgress(id), null),
      exams: safe('exams', 'Deneme sonuçları yüklenemedi.', this.service.getExams(id), { items: [] as ExamResult[] }),
      assignments: safe('assignments', 'Ödevler yüklenemedi.', this.service.getAssignments(id), { items: [] as StudentAssignment[] }),
      sessions: safe('sessions', 'Seanslar yüklenemedi.', this.service.getSessions(id), { items: [] as CoachingSession[] }),
      fees: safe('fees', 'Ücret bilgisi yüklenemedi.', this.service.getFees(id), null),
      access: safe('access', 'Abonelik bilgisi yüklenemedi.', this.service.getAccess(id), null),
      transfers: safe('transfers', 'Ödeme bildirimleri yüklenemedi.', this.service.getTransfers(id), [] as CoachingBankTransferRequest[]),
      plans: safe('plans', '', this.management.getPublicSubscriptionPlans(), [] as CoachingSubscriptionPlan[]),
      settings: safe('settings', '', this.management.getPublicBankTransferSettings(), null)
    }).subscribe(result => {
      this.progress.set(result.progress as StudentProgressSummary | null);
      this.exams.set(result.exams.items); this.assignments.set(result.assignments.items); this.sessions.set(result.sessions.items);
      this.fees.set(result.fees); this.access.set(result.access); this.transfers.set(result.transfers);
      const plans = result.plans.filter(plan => plan.audience === 'Individual' && plan.isActive && plan.isPublic && !plan.isContactOnly);
      this.plans.set(plans); this.settings.set(result.settings as CoachingSubscriptionSettings | null);
      if (!this.planId && plans.length) this.planId = plans[0].id;
      this.loaded.set(true);
    });
  }
}
