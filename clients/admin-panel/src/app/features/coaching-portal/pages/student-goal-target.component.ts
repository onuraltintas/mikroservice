import { LearningContext } from '../../../core/services/coaching-learning-setup.service';
import { contextExamCodes } from './student-goal-context';
import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subject, finalize, takeUntil } from 'rxjs';
import { CoachingStudyPlanningService, GoalTarget } from '../../../core/services/coaching-study-planning.service';
import { TargetAdmissionSearchComponent } from './target-admission-search.component';
import { PracticeExamProfile } from '../../../core/models/practice-exam';

import { CoachingLabelPipe } from '../coaching-labels';
@Component({
  selector: 'app-student-goal-target', standalone: true, imports: [CoachingLabelPipe, CommonModule, FormsModule, TargetAdmissionSearchComponent],
  templateUrl: './student-goal-target.component.html',
  styles: [`
    :host-context(staff-coaching-teacher-goals) { display: block; margin-top: 1rem; }
    :host-context(staff-coaching-teacher-goals) button { padding: .55rem .8rem; border: 1px solid var(--ui-border); border-radius: .5rem; background: var(--ui-brand-soft); color: var(--ui-text-muted); font: inherit; cursor: pointer; }
    :host-context(staff-coaching-teacher-goals) button:disabled { opacity: .5; cursor: default; }
    :host-context(staff-coaching-teacher-goals) fieldset { display: grid; gap: .75rem; border: 0; padding: .75rem 0; }
    :host-context(staff-coaching-teacher-goals) label { display: grid; gap: .3rem; }
    :host-context(staff-coaching-teacher-goals) input, :host-context(staff-coaching-teacher-goals) select { width: 100%; box-sizing: border-box; padding: .6rem; border: 1px solid var(--ui-border); border-radius: .5rem; background: var(--ui-surface); font: inherit; }
  `]
})
export class StudentGoalTargetComponent implements OnChanges {
  @Input({ required: true }) goalId = '';
  @Input() context: LearningContext | null = null;
  @Input() autoOpen = false;
  @Input() staffStudentId: string | null = null;
  @Output() readonly targetChanged = new EventEmitter<GoalTarget>();
  private readonly scopeChanged = new Subject<void>();
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  readonly expanded = signal(false);
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly target = signal<GoalTarget | null>(null);
  readonly error = signal('');
  readonly success = signal('');
  readonly stale = signal(false);
  readonly netExpanded = signal(false);
  readonly netProfiles = signal<PracticeExamProfile[]>([]);
  netExamCode = ''; targetNet: number | null = null;
  kind: 'school' | 'program' = 'school';
  score: number | null = null;
  maxScore: number | null = null;
  examType = '';
  readonly examTypes = ['Mock', 'Weekly', 'Monthly', 'LGS', 'YKS', 'AGS', 'YDS', 'YOKDIL', 'MidTerm', 'Final', 'Quiz'];

  private get studentArgument(): [] | [string] { return this.staffStudentId ? [this.staffStudentId] : []; }

  ngOnChanges(): void {
    this.scopeChanged.next();
    this.expanded.set(false); this.target.set(null); this.busy.set(false); this.saving.set(false);
    this.stale.set(false); this.error.set(''); this.success.set(''); this.kind = 'school';
    this.score = null; this.maxScore = null; this.examType = '';
    this.netExpanded.set(false); this.netProfiles.set([]); this.netExamCode = ''; this.targetNet = null;
    this.kind = this.context?.purpose === 'Yks' ? 'program' : 'school';
    if (this.autoOpen && this.goalId) this.open();
  }

  open() {
    if (this.busy() || this.saving()) return;
    this.expanded.set(true); this.target.set(null); this.stale.set(false); this.error.set(''); this.success.set('');
    this.busy.set(true);
    this.service.getGoalTarget(this.goalId, ...this.studentArgument)
      .pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({ next: value => { this.target.set(value); this.loadScore(value); }, error: () => this.error.set('Hedef bilgisi yüklenemedi. Yeniden yükleyebilirsin.') });
  }

  changeKind(kind: 'school' | 'program') {
    if (this.busy() || this.saving()) return;
    this.kind = kind;
  }

  chooseVerified(id: string) { this.save(this.kind === 'program' ? id : null, this.kind === 'school' ? id : null); }
  clear() { this.save(null, null); }

  private loadScore(value: GoalTarget) {
    this.score = value.scoreTarget?.targetScore ?? null;
    this.maxScore = value.scoreTarget?.maxScore ?? null;
    this.examType = value.scoreTarget?.examType ?? '';
    this.netExamCode = value.netTarget?.examCode ?? ''; this.targetNet = value.netTarget?.targetNet ?? null;
  }

  openNet() {
    this.netExpanded.set(true); this.error.set('');
    this.service.practiceProfiles().pipe(takeUntil(this.scopeChanged),takeUntilDestroyed(this.destroyRef)).subscribe({
      next: profiles => { const codes = contextExamCodes(this.context); this.netProfiles.set(codes.length ? profiles.filter(x=>codes.includes(x.code)) : profiles); }, error: () => this.error.set('Sınav kapsamları yüklenemedi. Yeniden dene.')
    });
  }
  saveNet(clear = false) {
    const target = this.target(); if (!target?.canEdit || this.saving() || this.busy() || this.stale()) return;
    const profile = this.netProfiles().find(x=>x.code===this.netExamCode);
    if (!clear && (!profile || this.targetNet === null || !Number.isFinite(this.targetNet) || this.targetNet < 0
      || this.targetNet > profile.sections.reduce((sum,x)=>sum+x.questionCount,0) || Math.abs(this.targetNet*100-Math.round(this.targetNet*100))>0.000001)) {
      this.error.set('Geçerli bir sınav kapsamı ve bu sınırları aşmayan net hedefi gir.'); return;
    }
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.service.saveGoalNetTarget(this.goalId,target.version,clear?null:this.netExamCode,clear?null:this.targetNet,...this.studentArgument)
      .pipe(takeUntil(this.scopeChanged),takeUntilDestroyed(this.destroyRef),finalize(()=>this.saving.set(false))).subscribe({
        next: value=>{this.target.set(value);this.loadScore(value);this.targetChanged.emit(value);this.success.set(clear?'Net hedefi kaldırıldı.':'Net hedefi kaydedildi. Gelişim raporunu yenileyebilirsin.');},
        error: err=>{this.stale.set(err.status===409);this.error.set(err.status===409?'Hedef değişti. Yeniden yükle.':err.error?.message || 'Net hedefi kaydedilemedi. Seçili okul/program için sınav kapsamını kontrol et.');}
      });
  }

  saveScore() {
    if (this.score === null || this.maxScore === null || !Number.isFinite(this.score) || !Number.isFinite(this.maxScore)
      || this.score <= 0 || this.maxScore <= 0 || this.maxScore > 999.99 || this.score > this.maxScore
      || Math.abs(this.score * 100 - Math.round(this.score * 100)) > 0.000001
      || Math.abs(this.maxScore * 100 - Math.round(this.maxScore * 100)) > 0.000001
      || !this.examTypes.includes(this.examType)) {
      this.error.set('Sınav türünü ve geçerli hedef puanı / puan ölçeğini birlikte gir. Hedef puanı ölçeği aşamaz.'); return;
    }
    this.writeScore(this.score, this.maxScore, this.examType);
  }

  clearScore() { this.writeScore(null, null, null); }

  private writeScore(score: number | null, maxScore: number | null, examType: string | null) {
    const target = this.target();
    if (!target?.canEdit || this.busy() || this.saving() || this.stale()) return;
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.service.saveGoalScoreTarget(this.goalId, target.version, score, maxScore, examType, ...this.studentArgument)
      .pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false)))
      .subscribe({
        next: value => { this.target.set(value); this.loadScore(value); this.targetChanged.emit(value); this.success.set(score === null ? 'Puan hedefi kaldırıldı.' : 'Puan hedefi kaydedildi. Raporu yenileyerek hesabı görebilirsin.'); },
        error: err => { this.stale.set(err.status === 409); this.error.set(err.status === 409
          ? 'Hedef değişti. Güncel bilgileri yeniden yükle.' : 'Puan hedefi kaydedilemedi. Bilgileri kontrol edip yeniden dene.'); }
      });
  }

  private save(programId: string | null, schoolId: string | null) {
    const target = this.target();
    if (!target?.canEdit || this.busy() || this.saving() || this.stale()) return;
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.service.saveGoalTarget(this.goalId, target.version, programId, schoolId, ...this.studentArgument)
      .pipe(takeUntil(this.scopeChanged), takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false)))
      .subscribe({
        next: value => { this.target.set(value); this.targetChanged.emit(value); this.success.set(programId || schoolId ? 'Hedef bağlantısı kaydedildi.' : 'Hedef bağlantısı kaldırıldı.'); },
        error: err => {
          this.stale.set(err.status === 409);
          this.error.set(err.status === 409 ? 'Hedef değişti. Güncel bilgileri yeniden yükle.' : 'Bağlantı kaydedilemedi. Seçilen kayıt artık aktif olmayabilir; yeniden ara.');
        }
      });
  }

  canLeavePage() { return !this.saving(); }
  private readonly feedback = coachingFeedback(this.success, this.error);
}
