import { HttpClient } from '@angular/common/http';
import { Component, OnInit, WritableSignal, computed, inject, input, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { RouterLink } from '@angular/router';
import { CoachingSession, StudentAssignment } from '../../../core/services/coaching-portal.service';
import { environment } from '../../../../environments/environment';
import { CoachingMotivationApi, CoachingMotivationComponent } from '../../../../../shared/coaching-motivation.component';
import { CoachingDatePipe, CoachingLabelPipe, coachingLabel } from '../coaching-labels';
import { StudentExamOverviewComponent } from '../growth/student-exam-overview.component';
import { StudentGoalSummaryComponent } from '../growth/student-goal-summary.component';
import { CsCardComponent, CsProgressRingComponent, CsStatusChipComponent } from '../ui';
import { CoachingParentService, PARENT_PAGE_SIZE } from './coaching-parent.service';
import { ParentBar, ParentWeekBarsComponent } from './parent-charts.component';
import { ParentPaymentsComponent } from './parent-payments.component';
import { ParentAdherenceWeek, ParentCoachingWeek, ParentWeeklyReview, firstName, possessive, shortDate } from './parent-model';

const dayTime = new Intl.DateTimeFormat('tr-TR', { timeZone: 'Europe/Istanbul', weekday: 'long', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' });

interface PagedList<T> { items: T[]; total: number | null; page: number; loading: boolean; error: string | null }
const emptyList = <T>(): PagedList<T> => ({ items: [], total: null, page: 0, loading: true, error: null });

/**
 * V4 (01.11.2026): the Koçluk tab of a child — this week, what needs attention, the last eight weeks, goal and exams,
 * assignments, sessions, the coach's weekly review, streak and payments. Read-only, in the parent's voice.
 */
@Component({
  selector: 'app-parent-coaching-tab',
  standalone: true,
  imports: [RouterLink, CoachingDatePipe, CoachingLabelPipe, CoachingMotivationComponent, CsCardComponent, CsProgressRingComponent, CsStatusChipComponent,
    ParentWeekBarsComponent, StudentExamOverviewComponent, StudentGoalSummaryComponent, ParentPaymentsComponent],
  template: `
    <div class="cp-top">
      <cs-card tone="raised" class="cp-week" data-testid="parent-week">
        <h2>Bu hafta</h2>
        @if (weekError()) { <p class="cp-muted">Bu haftanın özeti şu an yüklenemedi.</p> }
        @else if (week(); as w) {
          <div class="cp-week-head">
            <cs-progress-ring [value]="w.completed" [max]="w.planned" [label]="name() + ' bu hafta ' + w.planned + ' görevin ' + w.completed + ' tanesini tamamladı'" />
            <p><strong>{{ w.completed }}/{{ w.planned }} görev</strong><span>{{ w.planned ? 'bu hafta tamamlandı' : 'bu hafta planlı görev yok' }}</span></p>
          </div>
          <dl class="cp-facts">
            @if (w.nextSession; as session) {
              <div><dt>Sıradaki seans</dt><dd><strong>{{ session.title }}</strong> · {{ when(session.startsAt) }}@if (session.teacherName) { · {{ session.teacherName }} }</dd></div>
            }
            @if (w.examCountdown; as exam) {
              <div><dt>Sınava kalan</dt><dd><strong>{{ exam.name }}</strong> · {{ exam.daysLeft }} gün ({{ exam.date | coachingDate }})</dd></div>
            }
          </dl>
          @if (w.overdue.length) {
            <h3>Geciken</h3>
            <ul class="cp-list">@for (item of w.overdue; track $index) { <li><strong>{{ item.title }}</strong><span class="cp-muted">{{ item.date | coachingDate }}</span></li> }</ul>
          }
        } @else { <div class="cp-skeleton" role="status" aria-label="Bu hafta yükleniyor"></div> }
      </cs-card>
      @if (week()?.attention?.length) {
        <cs-card tone="accent" class="cp-attention" data-testid="parent-attention">
          <h2>Dikkat</h2>
          <ul class="cp-list">@for (item of week()!.attention; track $index) {
            <li>@if (internal(item.link)) { <a [routerLink]="item.link">{{ item.text }}</a> } @else { {{ item.text }} }</li>
          }</ul>
        </cs-card>
      }
    </div>

    <section class="cp-section" aria-labelledby="cp-adherence-title" data-testid="adherence">
      <h2 id="cp-adherence-title">Son 8 hafta</h2>
      <p class="cp-muted">Her hafta planlanan görevlerin kaçının tamamlandığı.</p>
      @if (adherenceError()) { <p class="cp-muted">Haftalık gidişat şu an yüklenemedi.</p> }
      @else if (adherenceBars().length) { <cp-week-bars [bars]="adherenceBars()" [label]="'Son ' + adherenceBars().length + ' hafta plan uyumu'" /> }
      @else if (adherenceLoaded()) { <p class="cp-muted">Henüz planlanmış bir hafta yok.</p> }
    </section>

    <section class="cp-section cs-growth-parent" aria-labelledby="cp-goal-title">
      <h2 id="cp-goal-title" class="cp-sr-only">{{ possessiveName() }} hedefi</h2>
      <app-student-goal-summary [studentId]="studentId()" [readonly]="true" audience="parent" [childName]="name()" />
    </section>
    <section class="cp-section cs-growth-parent" aria-labelledby="cp-exams-title">
      <h2 id="cp-exams-title">Denemeler</h2>
      <app-student-exam-overview [studentId]="studentId()" [readonly]="true" audience="parent" [childName]="name()" />
    </section>

    @if (hasReview()) {
      <section class="cp-section" aria-labelledby="cp-review-title" data-testid="parent-review">
        <h2 id="cp-review-title">Öğretmenin haftalık değerlendirmesi</h2>
        <div class="cp-card">
          @for (item of review()!.reviews; track $index) { <blockquote class="cp-quote"><p>{{ item.note }}</p><footer class="cp-muted">{{ item.reviewedAt | coachingDate }}</footer></blockquote> }
          @if (review()!.decisions.length) {
            <h3>Bu hafta için kararlar</h3>
            <ul class="cp-list">@for (decision of review()!.decisions; track $index) {
              <li><strong>{{ decision.title }}</strong><span class="cp-muted">{{ decision.dueOn | coachingDate }}</span><cs-status-chip [label]="decisionLabel(decision)" [tone]="decisionTone(decision)" /></li>
            }</ul>
          }
        </div>
      </section>
    }

    <section class="cp-section" aria-labelledby="cp-assignments-title" data-testid="parent-assignments">
      <h2 id="cp-assignments-title">Ödevler</h2>
      <ul class="cp-rows">
        @for (assignment of assignments().items; track assignment.id) {
          <li>
            <span class="cp-row-main"><strong>{{ assignment.title }}</strong><small>{{ assignment.subject ? assignment.subject + ' · ' : '' }}son gün {{ assignment.dueDate | coachingDate }}</small>
              @if (assignment.teacherFeedback) { <q class="cp-feedback">{{ assignment.teacherFeedback }}</q> }</span>
            <span class="cp-row-side">
              @if (assignment.score !== null && assignment.score !== undefined) { <strong>{{ assignment.score }}/{{ assignment.maxScore ?? 100 }}</strong> }
              <cs-status-chip [label]="assignment.isOverdue ? 'Gecikti' : (assignment.status | coachingLabel)" [tone]="assignment.isOverdue ? 'danger' : 'neutral'" />
            </span>
          </li>
        } @empty { @if (!assignments().loading) { <li class="cp-muted">{{ assignments().error || name() + ' için henüz ödev yok.' }}</li> } }
      </ul>
      @if (more(assignments())) { <button type="button" class="cp-more" data-testid="more-assignments" [disabled]="assignments().loading" (click)="loadAssignments()">Daha fazla ödev</button> }
    </section>

    <section class="cp-section" aria-labelledby="cp-sessions-title" data-testid="parent-sessions">
      <h2 id="cp-sessions-title">Seanslar</h2>
      <ul class="cp-rows">
        @for (session of sessions().items; track session.id) {
          <li>
            <span class="cp-row-main"><strong>{{ session.subject || 'Koçluk seansı' }}</strong><small>{{ session.startTime | coachingDate }} · {{ session.type | coachingLabel }}</small></span>
            <span class="cp-row-side">
              @if (attendance(session); as present) { <cs-status-chip [label]="present" [tone]="present === 'Katıldı' ? 'success' : 'warning'" /> }
              @else { <cs-status-chip [label]="session.status | coachingLabel" /> }
            </span>
          </li>
        } @empty { @if (!sessions().loading) { <li class="cp-muted">{{ sessions().error || name() + ' için henüz seans yok.' }}</li> } }
      </ul>
      @if (more(sessions())) { <button type="button" class="cp-more" data-testid="more-sessions" [disabled]="sessions().loading" (click)="loadSessions()">Daha fazla seans</button> }
    </section>

    <section class="cp-section" aria-label="Çalışma serisi ve rozetler">
      <edu-coaching-motivation [api]="motivationApi" [student]="studentId()" />
    </section>

    <app-parent-payments [studentId]="studentId()" [childName]="childName()" />`
})
export class ParentCoachingTabComponent implements OnInit {
  private readonly service = inject(CoachingParentService);
  readonly studentId = input.required<string>();
  readonly childName = input('');
  readonly name = computed(() => firstName(this.childName()));
  readonly possessiveName = computed(() => possessive(this.name()));
  readonly week = signal<ParentCoachingWeek | null>(null);
  readonly weekError = signal(false);
  readonly adherence = signal<ParentAdherenceWeek[]>([]);
  readonly adherenceLoaded = signal(false);
  readonly adherenceError = signal(false);
  readonly review = signal<ParentWeeklyReview | null>(null);
  readonly hasReview = computed(() => !!this.review() && (this.review()!.reviews?.length > 0 || this.review()!.decisions?.length > 0));
  readonly assignments = signal<PagedList<StudentAssignment>>(emptyList());
  readonly sessions = signal<PagedList<CoachingSession>>(emptyList());
  readonly motivationApi = new CoachingMotivationApi(inject(HttpClient), environment.apiUrl);
  readonly adherenceBars = computed<ParentBar[]>(() => this.adherence().map((week, index, all) => ({
    label: index === all.length - 1 ? 'Bu hafta' : shortDate(week.weekStart), value: week.completed, max: week.planned,
    text: week.planned ? `${week.completed}/${week.planned}` : '—', detail: week.minutes ? `${week.minutes} dk` : undefined })));

  ngOnInit(): void {
    const id = this.studentId();
    this.service.getCoachingWeek(id).subscribe({ next: week => this.week.set(week), error: () => this.weekError.set(true) });
    this.service.getAdherence(id, 8).subscribe({
      next: weeks => { this.adherence.set(weeks ?? []); this.adherenceLoaded.set(true); },
      error: () => this.adherenceError.set(true)
    });
    // Proposed V3 endpoint: until it exists (404) the section simply stays hidden.
    this.service.getWeeklyReview(id).subscribe({ next: review => this.review.set(review), error: () => this.review.set(null) });
    this.loadAssignments(); this.loadSessions();
  }

  loadAssignments() { this.loadPage(this.assignments, page => this.service.getAssignments(this.studentId(), page), 'Ödevler yüklenemedi.'); }
  loadSessions() { this.loadPage(this.sessions, page => this.service.getSessions(this.studentId(), page), 'Seanslar yüklenemedi.'); }

  more(list: PagedList<unknown>) {
    return !list.error && list.page > 0 && (list.total !== null ? list.items.length < list.total : list.items.length === list.page * PARENT_PAGE_SIZE);
  }
  attendance(session: CoachingSession): string | null {
    const own = session.studentReflections?.find(x => x.studentId === this.studentId());
    return own && own.attendanceStatus && own.attendanceStatus !== 'NotRecorded' ? coachingLabel(own.attendanceStatus) : null;
  }
  when(iso: string) { const date = new Date(iso); return Number.isFinite(date.getTime()) ? dayTime.format(date) : iso; }
  internal(link: string | null | undefined): link is string { return !!link && link.startsWith('/coaching-portal/'); }
  decisionLabel(decision: ParentWeeklyReview['decisions'][number]) { return decision.status === 'Completed' ? 'Tamamlandı' : decision.isOverdue ? 'Gecikti' : 'Açık'; }
  decisionTone(decision: ParentWeeklyReview['decisions'][number]) { return decision.status === 'Completed' ? 'success' as const : decision.isOverdue ? 'danger' as const : 'info' as const; }

  private loadPage<T>(list: WritableSignal<PagedList<T>>, fetch: (page: number) => Observable<{ items: T[]; totalCount?: number }>, message: string) {
    const next = list().page + 1;
    list.update(current => ({ ...current, loading: true, error: null }));
    fetch(next).subscribe({
      next: page => list.update(current => ({ items: [...current.items, ...(page.items ?? [])], total: page.totalCount ?? null, page: next, loading: false, error: null })),
      error: () => list.update(current => ({ ...current, loading: false, error: message }))
    });
  }
}
