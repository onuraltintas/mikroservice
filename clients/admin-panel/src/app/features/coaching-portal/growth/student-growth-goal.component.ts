import { ChangeDetectionStrategy, Component, viewChild } from '@angular/core';
import { StudentPrimaryGoalComponent } from '../pages/student-primary-goal.component';
import { CsPageHeaderComponent } from '../ui';
import { StudentGoalSummaryComponent } from './student-goal-summary.component';

/** T6 (31.10.2026): Gelişimim › Hedefim — where the student stands against the target; editing the goal folds away below. */
@Component({
  selector: 'app-student-growth-goal',
  standalone: true,
  imports: [CsPageHeaderComponent, StudentGoalSummaryComponent, StudentPrimaryGoalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <cs-page-header title="Hedefin" subtitle="Hedef okuluna ya da bölümüne ne kadar yaklaştığını gör." />
    <app-student-goal-summary />
    <details class="cs-goal-edit">
      <summary><span>Hedefini düzenle</span><small>Okul, bölüm ve net hedeflerin</small></summary>
      <app-student-primary-goal [embedded]="true" (changed)="summary()?.load()" />
    </details>`
})
export class StudentGrowthGoalComponent {
  readonly summary = viewChild(StudentGoalSummaryComponent);
  private readonly editor = viewChild(StudentPrimaryGoalComponent);
  canLeavePage() { return this.editor()?.canLeavePage() ?? true; }
}
