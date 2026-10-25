import { coachingFeedback } from '../../../../../../src/app/features/coaching-portal/coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, Input, OnChanges, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, finalize } from 'rxjs';
import { StudentStudyPlansComponent } from '../../../../../../src/app/features/coaching-portal/pages/student-study-plans.component';
import { dateKey } from '../../../../../../src/app/features/coaching-portal/pages/study-plan-calendar';
import { clock } from '../../../../../../src/app/features/coaching-portal/pages/weekly-calendar';
import { CoachingTeacherAssignmentDetailComponent } from './coaching-teacher-assignment-detail.component';
import { CoachingTeacherTrackingContext, CoachingTeacherTrackingService, TrackingFilters, TrackingItem, TrackingPage } from './coaching-teacher-tracking.service';
import { WeeklyReviewComponent } from '../../../../../../src/app/features/coaching-portal/pages/weekly-review.component';
import type { WeeklyDestination } from '../../../../../../shared/coaching-notification-target';

@Component({
  selector: 'staff-coaching-teacher-tracking', standalone: true,
  imports: [CommonModule, FormsModule, StudentStudyPlansComponent, CoachingTeacherAssignmentDetailComponent, WeeklyReviewComponent],
  templateUrl: './coaching-teacher-tracking.component.html', styleUrl: './coaching-teacher-tracking.component.scss'
})
export class CoachingTeacherTrackingComponent implements OnChanges {
  @Input() initialWeeklyTarget: WeeklyDestination | null = null;
  async ngOnChanges() { if (this.initialWeeklyTarget) await this.openWeekly(this.initialWeeklyTarget); }
  private readonly api = inject(CoachingTeacherTrackingService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly context = inject(CoachingTeacherTrackingContext, { optional: true }) ?? new CoachingTeacherTrackingContext();
  private pageNumber = 1;
  pageInput = 1;
  private request?: Subscription;
  @ViewChild(StudentStudyPlansComponent) private plan?: StudentStudyPlansComponent;
  @ViewChild(CoachingTeacherAssignmentDetailComponent) private assignment?: CoachingTeacherAssignmentDetailComponent;
  @ViewChild('weekly') private weekly?: WeeklyReviewComponent;
  readonly clock = clock;
  readonly data = signal<TrackingPage | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly pageError = signal('');
  readonly selected = signal<TrackingItem | null>(null);
  readonly refreshedAt = signal<Date | null>(null);
  filters: TrackingFilters = { fromDate: dateKey(new Date(Date.now() - 30 * 86400000)), toDate: dateKey(new Date()), mode: 'all', search: '' };
  private applied = { ...this.filters };
  get hasUnappliedFilters() {
    return this.filters.fromDate !== this.applied.fromDate || this.filters.toDate !== this.applied.toDate
      || this.filters.mode !== this.applied.mode || this.filters.search !== this.applied.search;
  }
  readonly labels = { all: 'Tüm kayıtlar', study: 'Geciken çalışmalar', assignment: 'Geciken ödevler', review: 'Değerlendirme bekleyen teslimler', reflection: 'Haftalık değerlendirme bekleyenler', decision: 'Geciken kararlar', 'weekly-review':'İnceleme bekleyen haftalık değerlendirmeler' };

  ngOnInit() {
    if (this.context.draft) this.filters = { ...this.context.draft };
    if (this.context.applied) this.applied = { ...this.context.applied };
    this.load(this.context.pageNumber);
  }
  ngOnDestroy() {
    this.context.draft = { ...this.filters }; this.context.applied = { ...this.applied };
    this.context.pageNumber = this.pageNumber;
  }
  async canLeavePage() { return (await this.plan?.canLeavePage() ?? true) && (await this.assignment?.canLeavePage() ?? true) && (await this.weekly?.canLeavePage() ?? true); }
  applyFilters() {
    if (!this.filters.fromDate || !this.filters.toDate || this.filters.fromDate > this.filters.toDate) {
      this.request?.unsubscribe(); this.data.set(null); this.refreshedAt.set(null);
      this.error.set('Geçerli bir tarih aralığı seçin.'); return;
    }
    this.applied = { ...this.filters }; this.load();
  }
  load(page = 1) {
    this.pageError.set('');
    this.request?.unsubscribe(); this.data.set(null); this.error.set(''); this.refreshedAt.set(null); this.loading.set(true);
    this.pageNumber = page;
    this.pageInput = page;
    this.request = this.api.get(this.applied, page).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
      next: value => { this.pageNumber = value.pageNumber; this.pageInput=value.pageNumber; this.data.set(value); this.refreshedAt.set(new Date()); },
      error: e => this.error.set(e.error?.message ?? 'Takip merkezi yüklenemedi. Bağlantınızı ve erişim yetkinizi kontrol ederek tekrar deneyin.')
    });
  }
  refresh() { this.load(this.data()?.pageNumber ?? this.pageNumber); }
  totalPages(page:TrackingPage) {return Math.max(1,Math.ceil(page.totalCount/page.pageSize));}
  goToPage() {
    const page=this.data();
    if(!page||this.loading())return;
    if(!Number.isInteger(this.pageInput)||this.pageInput<1||this.pageInput>this.totalPages(page)) {
      this.pageError.set('Geçerli bir sayfa numarası girin.');return;
    }
    this.load(this.pageInput);
  }
  async open(item: TrackingItem): Promise<boolean> { if (!await this.canLeavePage()) return false; this.selected.set(item); return true; }
  async openWeekly(target: WeeklyDestination) {
    return await this.open({ kind: target.decisionId ? 'decision' : 'reflection', recordId: target.decisionId ?? target.studentId,
      studentId: target.studentId, studentName: 'Öğrenci', title: target.decisionId ? 'Haftalık karar' : 'Haftalık değerlendirme',
      planId: null, date: target.weekStart, weekStart: target.weekStart, startMinute: null, minutes: null, timeZoneId: 'Europe/Istanbul' });
  }
  async back() {
    if (!await this.canLeavePage()) return;
    this.selected.set(null); this.refresh();
  }

  private readonly errorFeedback = coachingFeedback(undefined, this.error);
}
