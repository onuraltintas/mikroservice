import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, Injector, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingDailyAgendaService, DailyAgendaItem } from '../../../core/services/coaching-daily-agenda.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudentTodayService, StudentTodayView } from '../../../core/services/coaching-student-today.service';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import { StaffPortalRedirectService } from '../staff-portal-redirect.service';
import { CoachingWhatsNewCardComponent } from '../layout/coaching-whats-new-card.component';
import { CsCardComponent, CsProgressRingComponent, CsStatusChipComponent } from '../ui';

export interface TodayRow { item: DailyAgendaItem; overdue: boolean; }
interface RowLink { path: string[]; query: Record<string, string> | null; }

/**
 * T3 (31.10.2026): the student's "Bugün" screen. One summary call (api/coaching/students/me/today) gives the next step,
 * today's work with overdue items, the exam countdown, streak, week progress, the goal gap, a due weekly review and the
 * next session. The next step is the one bold element; everything else stays quiet.
 */
@Component({
  selector: 'app-coaching-portal-home',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, DecimalPipe, CsCardComponent, CsProgressRingComponent, CsStatusChipComponent, CoachingWhatsNewCardComponent],
  templateUrl: './coaching-portal-home.component.html',
  styleUrl: './coaching-portal-home.component.scss'
})
export class CoachingPortalHomeComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly views = inject(CoachingPortalViewService);
  private readonly staffPortalRedirect = inject(StaffPortalRedirectService);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);

  readonly isStudent = signal(false);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly view = signal<StudentTodayView | null>(null);
  readonly setupReady = signal(true);
  readonly upcoming = signal<DailyAgendaItem[] | null>(null);
  readonly upcomingError = signal(false);

  /** Overdue work first (oldest first), then today in time order; each row says whether it is late. */
  readonly rows = computed<TodayRow[]>(() => {
    const view = this.view();
    if (!view) return [];
    return [...view.overdue.map(item => ({ item, overdue: true })), ...view.items.map(item => ({ item, overdue: false }))];
  });
  readonly moreToday = computed(() => {
    const view = this.view();
    return view ? Math.max(0, view.todayCount - view.items.length) + Math.max(0, view.overdueCount - view.overdue.length) : 0;
  });

  ngOnInit() {
    this.isStudent.set(this.views.current() === 'Student');
    if (this.views.current() === 'Teacher') {
      this.staffPortalRedirect.redirect('coaching');
      return;
    }
    if (!this.authService.userProfile()?.id) {
      this.loading.set(false);
      this.errorMessage.set('Oturum bilgin yüklenemedi. Yeniden giriş yap.');
      return;
    }
    if (!this.isStudent()) { this.loading.set(false); return; }
    this.load();
    this.injector.get(CoachingLearningSetupService).get().pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: setup => this.setupReady.set(setup.isReady), error: () => this.setupReady.set(true) });
  }

  load() {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.injector.get(CoachingStudentTodayService).get().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: view => { this.view.set(view); this.loading.set(false); },
      error: () => { this.loading.set(false); this.errorMessage.set('Bugünün özeti yüklenemedi. Bağlantını kontrol edip tekrar dene.'); }
    });
  }

  /** Upcoming work is loaded only when the student opens the section. */
  toggleUpcoming(event: Event) {
    if (!(event.target as HTMLDetailsElement).open || this.upcoming()) return;
    this.upcomingError.set(false);
    this.injector.get(CoachingDailyAgendaService).get('upcoming').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: page => this.upcoming.set(page.items),
      error: () => this.upcomingError.set(true)
    });
  }

  link(item: DailyAgendaItem): RowLink {
    switch (item.kind) {
      case 'assignment': return { path: ['/coaching-portal/assignments', item.recordId], query: null };
      case 'decision': return { path: ['/coaching-portal/growth/week'], query: { week: item.weekStart ?? '', decision: item.recordId } };
      default: return { path: ['/coaching-portal/plan'], query: { plan: item.planId ?? '', task: item.recordId } };
    }
  }

  kindLabel(item: DailyAgendaItem) {
    return item.kind === 'assignment' ? 'Ödev' : item.kind === 'decision' ? 'Haftalık karar' : 'Çalışma';
  }

  actionLabel(item: DailyAgendaItem) {
    return item.kind === 'assignment' ? 'Ödevi aç' : item.kind === 'decision' ? 'Kararı aç' : 'Göreve başla';
  }

  clock(minute: number | null) {
    if (minute === null || minute === undefined) return '';
    return `${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`;
  }

  /** "Saat 14:00, 30 dk" for today; overdue rows add the day they were due. */
  meta(row: TodayRow, dateText: string | null) {
    const parts = [this.kindLabel(row.item)];
    if (row.overdue && dateText) parts.push(dateText);
    if (row.item.startMinute !== null) parts.push(`saat ${this.clock(row.item.startMinute)}`);
    if (row.item.minutes) parts.push(`${row.item.minutes} dk`);
    return parts.join(', ');
  }
}
