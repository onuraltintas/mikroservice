import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CoachingStudentExamOverviewService, StudentExamOverview, StudentExamOverviewItem } from '../../../core/services/coaching-student-exam-overview.service';
import { coachingLabel } from '../coaching-labels';
import { CsCardComponent, CsEmptyStateComponent, CsSegmentedTabsComponent, CsStatusChipComponent, CsTabOption } from '../ui';
import { ExamSubjectBarsComponent } from './exam-subject-bars.component';
import { ExamTrendChartComponent } from './exam-trend-chart.component';
import { examPlace, examSourceLabel, examSourceTone, formatDay, formatNet } from './growth-model';
import { possessive } from '../parent/parent-model';

/**
 * T6 (31.10.2026): the student's exams in one place — net trend per scope, subject averages and one list with where each
 * exam came from. The student opens their own entries from the list; a parent sees the same view read-only.
 */
@Component({
  selector: 'app-student-exam-overview',
  standalone: true,
  imports: [NgTemplateOutlet, CsCardComponent, CsEmptyStateComponent, CsSegmentedTabsComponent, CsStatusChipComponent, ExamSubjectBarsComponent, ExamTrendChartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'cs-growth-exams' },
  template: `
    @if (error()) { <p class="cs-growth-alert" role="alert">{{ error() }} <button type="button" (click)="reload()">Yeniden dene</button></p> }
    @if (loading() && !data()) { <div class="cs-growth-skeleton" role="status" [attr.aria-label]="parent() ? 'Denemeler yükleniyor' : 'Denemelerin yükleniyor'"></div> }
    @if (data(); as view) {
      @if (!view.exams.length) {
        <cs-empty-state [title]="readonly() ? 'Henüz deneme yok' : 'İlk denemeni ekle'"
          [text]="readonly() ? 'Deneme sonuçları girildiğinde burada görünecek.' : 'Netini gir; gidişatını ve en çok zorlandığın dersi burada görelim.'">
          @if (!readonly()) { <button type="button" class="cs-growth-primary" (click)="add.emit()">+ Deneme ekle</button> }
        </cs-empty-state>
      } @else {
        @if (view.trends.length) {
          <div class="cs-growth-pair">
            <cs-card tone="raised" class="cs-growth-trend">
              <div class="cs-growth-card-head"><h2>{{ parent() ? possessive(name()) + ' net gidişatı' : 'Net gidişatın' }}</h2>
                @if (scopeOptions().length > 1) { <cs-segmented-tabs label="Sınav türü" [options]="scopeOptions()" [(value)]="scope" /> }
                @else { <cs-status-chip [label]="trend()!.examName" tone="brand" /> }
              </div>
              @if (trend(); as current) {
                <p class="cs-growth-lead"><strong>{{ last(current) }}</strong> net <span>/ {{ current.maxNet }}</span>
                  @if (current.points.length > 1) { <cs-status-chip [label]="changeText(current)" [tone]="change(current) >= 0 ? 'success' : 'warning'" /> }</p>
                <cs-exam-trend-chart [trend]="current" [childName]="parent() ? name() : ''" />
              }
            </cs-card>
            <cs-card class="cs-growth-subjects"><h2>{{ parent() ? 'Dersleri' : 'Derslerin' }}</h2><p class="cs-growth-muted">Son {{ subjectExamCount() }} denemenin ortalaması</p>
              <cs-exam-subject-bars [subjects]="view.subjects" [examCode]="scope()" [childName]="parent() ? name() : ''" />
            </cs-card>
          </div>
        }
        <cs-card class="cs-growth-list">
          <div class="cs-growth-card-head"><h2>Tüm denemeler</h2><span class="cs-growth-muted">{{ view.exams.length }} deneme</span></div>
          <ul>
            @for (exam of view.exams; track exam.resultId) {
              <li>
                @if (!readonly() && exam.source === 'Student') {
                  <button type="button" class="exam-row" data-testid="exam-row" (click)="edit.emit(exam.examId)" [attr.aria-label]="exam.title + ', düzenle'">
                    <ng-container *ngTemplateOutlet="row; context: { $implicit: exam }" /></button>
                } @else { <div class="exam-row" data-testid="exam-row"><ng-container *ngTemplateOutlet="row; context: { $implicit: exam }" /></div> }
              </li>
            }
          </ul>
        </cs-card>
      }
    }
    <ng-template #row let-exam>
      <span class="when"><strong>{{ day(exam.examDate) }}</strong></span>
      <span class="what"><strong>{{ exam.title }}</strong><small>{{ kind(exam) }}@if (place(exam)) { · {{ place(exam) }} }</small></span>
      <cs-status-chip [label]="source(exam)" [tone]="tone(exam)" />
      <span class="result">@if (exam.net !== null) { <strong>{{ net(exam.net) }} net</strong> } @else { <strong>{{ net(exam.score) }}</strong><small>/ {{ net(exam.maxScore) }} puan</small> }</span>
    </ng-template>`
})
export class StudentExamOverviewComponent implements OnInit {
  private readonly api = inject(CoachingStudentExamOverviewService);
  private readonly destroyRef = inject(DestroyRef);
  /** null: the signed-in student. */
  readonly studentId = input<string | null>(null);
  readonly readonly = input(false);
  /** V4: 'parent' talks about the child by name ("Elif'in net gidişatı") instead of to the student. */
  readonly audience = input<'student' | 'parent'>('student');
  readonly childName = input('');
  readonly parent = computed(() => this.audience() === 'parent');
  readonly name = computed(() => this.childName() || 'Çocuğun');
  readonly possessive = possessive;
  readonly add = output<void>();
  readonly edit = output<string>();
  readonly data = signal<StudentExamOverview | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly scope = signal('');
  readonly scopeOptions = computed<CsTabOption[]>(() => (this.data()?.trends ?? []).map(x => ({ value: x.examCode, label: x.examName })));
  readonly trend = computed(() => this.data()?.trends.find(x => x.examCode === this.scope()) ?? this.data()?.trends[0] ?? null);
  readonly subjectExamCount = computed(() => this.data()?.subjects.find(x => x.examCode === this.scope())?.examsUsed ?? 0);

  ngOnInit() { this.reload(); }

  reload() {
    this.loading.set(true); this.error.set(null);
    this.api.get(this.studentId()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => {
        this.data.set(data); this.loading.set(false);
        if (!data.trends.some(x => x.examCode === this.scope())) this.scope.set(data.trends[0]?.examCode ?? '');
      },
      error: () => { this.loading.set(false); this.error.set(this.parent() ? 'Denemeler yüklenemedi. Bağlantını kontrol edip yeniden dene.' : 'Denemelerin yüklenemedi. Bağlantını kontrol edip yeniden dene.'); }
    });
  }

  source(exam: StudentExamOverviewItem) { return examSourceLabel(exam, this.parent() ? this.name() : null); }
  tone(exam: StudentExamOverviewItem) { return examSourceTone(exam.source); }
  place(exam: StudentExamOverviewItem) { return examPlace(exam); }
  kind(exam: StudentExamOverviewItem) { return exam.examName ?? coachingLabel(exam.examType, 'examType'); }
  day(iso: string) { return formatDay(iso); }
  net(value: number) { return formatNet(value); }
  last(trend: { points: { net: number }[] }) { return formatNet(trend.points.at(-1)?.net ?? 0); }
  change(trend: { points: { net: number }[] }) { return (trend.points.at(-1)?.net ?? 0) - (trend.points[0]?.net ?? 0); }
  changeText(trend: { points: { net: number }[] }) {
    const value = this.change(trend);
    return `${this.parent() ? 'İlk denemesine' : 'İlk denemene'} göre ${value >= 0 ? '+' : '−'}${formatNet(Math.abs(value))} net`;
  }
}
