import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, forkJoin, of } from 'rxjs';
import { CoachingLearningSetupService, LearningContext } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { StudentOnboardingComponent } from '../onboarding/student-onboarding.component';
import { StudentGoalTargetComponent } from '../pages/student-goal-target.component';
import { CsPageHeaderComponent } from '../ui';
import { StudentGoalSummaryComponent } from './student-goal-summary.component';

/**
 * T6 (31.10.2026): Gelişimim › Hedefim — where the student stands against the target.
 * E7 (#30, live test 06.11.2026): the target is edited with the first-login wizard's target step (same lists and endpoints:
 * LGS city → type → school; YKS level → score type → city → university → programme, or TYT and field scores; YDS/YÖKDİL;
 * AGS P1/P2), the current target first with "Değiştir". A teacher's goal stays locked there. The net targets of the main goal
 * follow below.
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
      <app-student-onboarding [embedded]="true" (targetSaved)="targetSaved()" />
    </section>
    <section class="cs-goal-edit" aria-label="Deneme net hedeflerin">
      @if (goalId(); as goal) {
        <app-student-goal-target [goalId]="goal" [context]="context()" [autoOpen]="true" [netOnly]="true" (targetChanged)="summary()?.load()" />
      } @else if (loaded()) {
        <p class="cs-growth-muted">Net hedefi eklemek için önce hedefini seç.</p>
      }
    </section>`
})
export class StudentGrowthGoalComponent implements OnInit {
  private readonly planning = inject(CoachingStudyPlanningService);
  private readonly setup = inject(CoachingLearningSetupService);
  private readonly destroyRef = inject(DestroyRef);
  readonly summary = viewChild<{ load(): void }>('goalSummary');
  private readonly wizard = viewChild(StudentOnboardingComponent);
  private readonly nets = viewChild(StudentGoalTargetComponent);
  readonly goalId = signal<string | null>(null);
  readonly context = signal<LearningContext | null>(null);
  readonly loaded = signal(false);

  ngOnInit() { this.loadGoal(); }

  /** A new target can create the main goal and changes its version, so the net editor reloads it. */
  targetSaved() {
    this.summary()?.load();
    this.loadGoal(true);
  }

  private loadGoal(reopen = false) {
    forkJoin({ primary: this.planning.getPrimaryGoal().pipe(catchError(() => of(null))), setup: this.setup.get().pipe(catchError(() => of(null))) })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe(({ primary, setup }) => {
        const id = primary?.goalId ?? null;
        const same = id !== null && id === this.goalId();
        this.context.set(setup?.context ?? null);
        this.goalId.set(id);
        this.loaded.set(true);
        if (reopen && same) this.nets()?.open();
      });
  }

  canLeavePage() { return !this.wizard()?.busy() && (this.nets()?.canLeavePage() ?? true); }
}
