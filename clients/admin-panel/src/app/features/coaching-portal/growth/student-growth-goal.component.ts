import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, forkJoin, of } from 'rxjs';
import { CoachingLearningSetupService, LearningContext } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { coachingToaster } from '../coaching-feedback';
import { StudentOnboardingComponent } from '../onboarding/student-onboarding.component';
import { StudentGoalTargetComponent } from '../pages/student-goal-target.component';
import { CsPageHeaderComponent } from '../ui';
import { StudentGoalSummaryComponent } from './student-goal-summary.component';

/**
 * T6 (31.10.2026): Gelişimim › Hedefim — where the student stands against the target.
 * E7 (#30, live test 06.11.2026): the target is edited with the first-login wizard's target step (same lists and endpoints:
 * LGS city → type → school; YKS level → score type → city → university → programme, or TYT and field scores; YDS/YÖKDİL;
 * AGS P1/P2), the current target first with "Değiştir". A teacher's goal stays locked there. The net targets of the main goal
 * follow below. Choosing among several goals is no longer part of the student UI: the target step writes the main goal.
 */
@Component({
  selector: 'app-student-growth-goal',
  standalone: true,
  imports: [CsPageHeaderComponent, StudentGoalSummaryComponent, StudentOnboardingComponent, StudentGoalTargetComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <cs-page-header title="Hedefin" subtitle="Hedef okuluna ya da bölümüne ne kadar yaklaştığını gör." />
    <app-student-goal-summary #goalSummary />
    <section class="cs-goal-edit" aria-labelledby="goal-edit-title">
      <h2 id="goal-edit-title">Hedefini düzenle</h2>
      <app-student-onboarding #wizard [embedded]="true" (targetSaved)="targetSaved()" />
    </section>
    <section class="cs-goal-edit" aria-label="Deneme net hedeflerin">
      @if (loadError()) {
        <p class="cs-growth-alert" role="alert">Net hedeflerin yüklenemedi. <button type="button" (click)="loadGoal()">Yeniden yükle</button></p>
      } @else if (goalId(); as goal) {
        <app-student-goal-target #nets [goalId]="goal" [context]="context()" [autoOpen]="true" [netOnly]="true" [revision]="revision()" (targetChanged)="summary()?.load()" />
      } @else if (loaded()) {
        <p class="cs-growth-muted">Net hedefi eklemek için önce hedefini seç.</p>
      }
    </section>`
})
export class StudentGrowthGoalComponent implements OnInit {
  private readonly planning = inject(CoachingStudyPlanningService);
  private readonly setup = inject(CoachingLearningSetupService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toaster = coachingToaster();
  readonly summary = viewChild<{ load(): void }>('goalSummary');
  private readonly wizard = viewChild<{ busy(): boolean; dirty(): boolean }>('wizard');
  private readonly nets = viewChild<{ canLeavePage(): boolean }>('nets');
  /** Bumped after a saved target: the net editor loads the goal's new version (its inputs change). */
  readonly revision = signal(0);
  readonly goalId = signal<string | null>(null);
  readonly context = signal<LearningContext | null>(null);
  readonly loaded = signal(false);
  readonly loadError = signal(false);

  ngOnInit() { this.loadGoal(); }

  /** A new target can create the main goal and changes its version, so the net editor is loaded again. */
  targetSaved() {
    this.summary()?.load();
    this.loadGoal(true);
  }

  loadGoal(afterSave = false) {
    this.loadError.set(false);
    forkJoin({ primary: this.planning.getPrimaryGoal(), setup: this.setup.get().pipe(catchError(() => of(null))) })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: ({ primary, setup }) => {
          this.context.set(setup?.context ?? null);
          this.goalId.set(primary?.goalId ?? null);
          if (afterSave) this.revision.update(x => x + 1);
          this.loaded.set(true);
        },
        error: () => { this.loadError.set(true); this.loaded.set(true); }
      });
  }

  /** Review of #120: a started target change (or a save in flight) is not lost without asking. */
  async canLeavePage() {
    const wizard = this.wizard();
    if (wizard?.busy()) return false;
    if (wizard?.dirty() && !await this.toaster.confirm('Hedef değişikliğin kaydedilmedi. Sayfadan çıkılsın mı?')) return false;
    return this.nets()?.canLeavePage() ?? true;
  }
}
