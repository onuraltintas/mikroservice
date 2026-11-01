import { ChangeDetectionStrategy, Component, viewChild } from '@angular/core';
import { CoachingPortalProgressComponent } from '../pages/coaching-portal-progress.component';
import { CsPageHeaderComponent } from '../ui';
import { StudentWeekSummaryComponent } from './student-week-summary.component';

/** T6 (31.10.2026): Gelişimim › Haftam — the week at a glance, then the weekly review and the study goals in full. */
@Component({
  selector: 'app-student-growth-week',
  standalone: true,
  imports: [CoachingPortalProgressComponent, CsPageHeaderComponent, StudentWeekSummaryComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <cs-page-header title="Haftan" subtitle="Ne kadar çalıştın, hangi kararları aldın, hangi rozete yaklaştın." />
    <app-student-week-summary />
    <app-coaching-portal-progress [embedded]="true" />`
})
export class StudentGrowthWeekComponent {
  private readonly progress = viewChild(CoachingPortalProgressComponent);
  async canLeavePage() { return (await this.progress()?.canLeavePage()) ?? true; }
}
