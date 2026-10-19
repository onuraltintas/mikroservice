import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, HostListener, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Observable, Subject, finalize, map, takeUntil } from 'rxjs';
import { CoachingStudyPlanningService, GoalTarget, TargetPage } from '../../../core/services/coaching-study-planning.service';
import { TargetAdmissionSearchComponent } from './target-admission-search.component';

interface TargetResult { id: string; label: string; detail: string; minimumScore: number | null; scoreYear: number | null }
@Component({
  selector: 'app-student-goal-target', standalone: true, imports: [CommonModule, FormsModule, TargetAdmissionSearchComponent],
  templateUrl: './student-goal-target.component.html',
  styles: [`
    :host-context(staff-coaching-teacher-goals) { display: block; margin-top: 1rem; }
    :host-context(staff-coaching-teacher-goals) button { padding: .55rem .8rem; border: 1px solid #d5dfd2; border-radius: .5rem; background: #f5f8f1; color: #355348; font: inherit; cursor: pointer; }
    :host-context(staff-coaching-teacher-goals) button:disabled { opacity: .5; cursor: default; }
    :host-context(staff-coaching-teacher-goals) fieldset { display: grid; gap: .75rem; border: 0; padding: .75rem 0; }
    :host-context(staff-coaching-teacher-goals) label { display: grid; gap: .3rem; }
    :host-context(staff-coaching-teacher-goals) input, :host-context(staff-coaching-teacher-goals) select { width: 100%; box-sizing: border-box; padding: .6rem; border: 1px solid #d5dfd2; border-radius: .5rem; background: white; font: inherit; }
    :host-context(staff-coaching-teacher-goals) ul { list-style: none; padding: 0; }
    :host-context(staff-coaching-teacher-goals) li { padding: .75rem; margin-top: .5rem; border: 1px solid #e2e7dd; border-radius: .5rem; }
  `]
})
export class StudentGoalTargetComponent implements OnChanges {
  @Input({ required: true }) goalId = '';
  @Input() staffStudentId: string | null = null;
  @Output() readonly targetChanged = new EventEmitter<GoalTarget>();
  private readonly scopeChanged = new Subject<void>();
  ngOnChanges(): void {
    this.scopeChanged.next(); this.expanded.set(false); this.target.set(null); this.results.set([]);
    this.busy.set(false); this.saving.set(false); this.stale.set(false); this.error.set(''); this.success.set('');
    this.query = ''; this.city = ''; this.district = ''; this.scoreType = ''; this.page = 1; this.total = 0; this.searched = false;
    this.score = null; this.maxScore = null; this.examType = ''; this.showVerified = false;
  }
  private get studentArgument(): [] | [string] { return this.staffStudentId ? [this.staffStudentId] : []; }
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  readonly expanded = signal(false);
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly target = signal<GoalTarget | null>(null);
  readonly results = signal<TargetResult[]>([]);
  readonly error = signal('');
  readonly success = signal('');
  readonly stale = signal(false);
  kind: 'school' | 'program' = 'school';
  showVerified = false;
  chooseVerified(id: string) { this.save(this.kind === 'program' ? id : null, this.kind === 'school' ? id : null); }
  query = ''; city = ''; district = ''; scoreType = '';
  page = 1; total = 0; searched = false;
  score: number | null = null;
  maxScore: number | null = null;
  examType = '';
  readonly examTypes = ['Mock', 'Weekly', 'Monthly', 'LGS', 'YKS', 'MidTerm', 'Final', 'Quiz'];

  open() {
    if (this.busy() || this.saving()) return;
    this.expanded.set(true); this.target.set(null); this.results.set([]);
    this.stale.set(false); this.error.set(''); this.success.set(''); this.searched = false;
    this.busy.set(true);
    this.service.getGoalTarget(this.goalId, ...this.studentArgument).pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({
      next: value => { this.target.set(value); this.loadScore(value); },
      error: () => this.error.set('Hedef bilgisi yüklenemedi. Yeniden yükleyebilirsiniz.')
    });
  }
  changeKind(kind: 'school' | 'program') {
    if (this.busy() || this.saving()) return;
    this.kind = kind; this.results.set([]); this.page = 1; this.total = 0; this.searched = false;
  }
  searchTargets(page = 1) {
    if (!this.target()?.canEdit || this.busy() || this.saving() || this.stale()) return;
    this.results.set([]); this.error.set(''); this.success.set(''); this.busy.set(true); this.searched = false;
    const search: Observable<TargetPage<TargetResult>> = this.kind === 'school'
      ? this.service.searchSchools(this.query, this.city, this.district, page).pipe(map(value => ({ ...value, items: value.items.map(x => ({ ...x, label: x.name, detail: `${x.city} / ${x.district}` })) })))
      : this.service.searchPrograms(this.query, this.scoreType, page).pipe(map(value => ({ ...value, items: value.items.map(x => ({ ...x, label: `${x.universityName} — ${x.name}`, detail: [x.programCode, x.scoreType].filter(Boolean).join(' · ') })) })));
    search.pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({
      next: (value: TargetPage<TargetResult>) => { this.results.set(value.items); this.page = value.pageNumber; this.total = value.totalCount; this.searched = true; },
      error: () => this.error.set('Arama sonuçları yüklenemedi. Filtreleri kontrol edip yeniden deneyin.')
    });
  }
  choose(id: string) {
    if (!this.results().some(x => x.id === id)) return;
    this.save(this.kind === 'program' ? id : null, this.kind === 'school' ? id : null);
  }
  clear() { this.save(null, null); }
  private loadScore(value: GoalTarget) {
    this.score = value.scoreTarget?.targetScore ?? null;
    this.maxScore = value.scoreTarget?.maxScore ?? null;
    this.examType = value.scoreTarget?.examType ?? '';
  }
  saveScore() {
    if (this.score === null || this.maxScore === null || !Number.isFinite(this.score) || !Number.isFinite(this.maxScore)
      || this.score <= 0 || this.maxScore <= 0 || this.maxScore > 999.99 || this.score > this.maxScore
      || Math.abs(this.score * 100 - Math.round(this.score * 100)) > 0.000001
      || Math.abs(this.maxScore * 100 - Math.round(this.maxScore * 100)) > 0.000001
      || !this.examTypes.includes(this.examType)) {
      this.error.set('Sınav türünü ve geçerli hedef puanı / puan ölçeğini birlikte girin. Hedef puanı ölçeği aşamaz.'); return;
    }
    this.writeScore(this.score, this.maxScore, this.examType);
  }
  clearScore() { this.writeScore(null, null, null); }
  private writeScore(score: number | null, maxScore: number | null, examType: string | null) {
    const target = this.target();
    if (!target?.canEdit || this.busy() || this.saving() || this.stale()) return;
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.service.saveGoalScoreTarget(this.goalId, target.version, score, maxScore, examType, ...this.studentArgument)
      .pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
        next: value => { this.target.set(value); this.loadScore(value); this.targetChanged.emit(value); this.success.set(score === null ? 'Puan hedefi kaldırıldı.' : 'Puan hedefi kaydedildi. Raporu yenileyerek hesabı görebilirsiniz.'); },
        error: err => { this.stale.set(err.status === 409); this.error.set(err.status === 409
          ? 'Hedef değişti. Güncel bilgileri yeniden yükleyin.' : 'Puan hedefi kaydedilemedi. Bilgileri kontrol edip yeniden deneyin.'); }
      });
  }
  private save(programId: string | null, schoolId: string | null) {
    const target = this.target();
    if (!target?.canEdit || this.busy() || this.saving() || this.stale()) return;
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.service.saveGoalTarget(this.goalId, target.version, programId, schoolId, ...this.studentArgument)
      .pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
        next: value => { this.target.set(value); this.targetChanged.emit(value); this.success.set(programId || schoolId ? 'Hedef bağlantısı kaydedildi.' : 'Hedef bağlantısı kaldırıldı.'); },
        error: err => {
          this.stale.set(err.status === 409);
          this.error.set(err.status === 409 ? 'Hedef değişti. Güncel bilgileri yeniden yükleyin.' : 'Bağlantı kaydedilemedi. Seçilen kayıt artık aktif olmayabilir; yeniden arayın.');
        }
      });
  }
  canLeavePage() { return !this.saving(); }
  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent) {
    if (this.saving()) { event.preventDefault(); event.returnValue = ''; }
  }
}
