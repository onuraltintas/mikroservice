import { Component, ViewChild, signal } from '@angular/core';
import { CoachingTeacherAssignmentsComponent } from './coaching-teacher-assignments.component';
import { CoachingTeacherSessionsComponent } from './coaching-teacher-sessions.component';
import { CoachingTeacherGoalsComponent } from './coaching-teacher-goals.component';
import { CoachingTeacherStudentsComponent } from './coaching-teacher-students.component';
import { CoachingTeacherExamsComponent } from './coaching-teacher-exams.component';
import { CoachingTeacherSubscriptionComponent } from './coaching-teacher-subscription.component';
import { CoachingFinanceComponent } from '../../../../../../src/app/features/coaching-portal/pages/coaching-finance.component';
import { CoachingProfileComponent } from '../../../../../../shared/coaching-profile.component';
import { environment } from '../../../environments/environment';
import {CoachingTeacherStudyOverviewComponent} from './coaching-teacher-study-overview.component';

import { CoachingTeacherTrackingComponent } from './coaching-teacher-tracking.component';
import { CoachingTeacherTrackingContext } from './coaching-teacher-tracking.service';
import type { WeeklyDestination } from '../../../../../../shared/coaching-notification-target';

type CoachingTeacherSection = 'students' | 'assignments' | 'sessions' | 'goals' | 'exams' | 'subscription' | 'finance' | 'profile' | 'study' | 'tracking';

@Component({
  selector: 'staff-coaching-teacher-workspace',
  standalone: true,
  providers: [CoachingTeacherTrackingContext],
  imports: [CoachingTeacherStudentsComponent, CoachingTeacherAssignmentsComponent, CoachingTeacherSessionsComponent, CoachingTeacherGoalsComponent, CoachingTeacherExamsComponent, CoachingTeacherSubscriptionComponent, CoachingFinanceComponent, CoachingProfileComponent,CoachingTeacherStudyOverviewComponent, CoachingTeacherTrackingComponent],
  templateUrl: './coaching-teacher-workspace.component.html',
  styleUrl: './coaching-teacher-workspace.component.scss'
})
export class CoachingTeacherWorkspaceComponent {
  readonly apiUrl = environment.apiUrl;
  @ViewChild(CoachingProfileComponent) private profile?: CoachingProfileComponent;
  @ViewChild('trackingPanel') private tracking?: Pick<CoachingTeacherTrackingComponent, 'canLeavePage' | 'openWeekly'>;
  @ViewChild(CoachingTeacherStudyOverviewComponent) private study?:CoachingTeacherStudyOverviewComponent;
  @ViewChild(CoachingTeacherStudentsComponent) private students?: CoachingTeacherStudentsComponent;
  @ViewChild(CoachingTeacherGoalsComponent) private goals?: CoachingTeacherGoalsComponent;
  @ViewChild(CoachingTeacherAssignmentsComponent) private assignments?: CoachingTeacherAssignmentsComponent;
  readonly notificationAssignmentId = signal<string | null>(null);
  readonly notificationWeeklyTarget = signal<WeeklyDestination | null>(null);
  async openWeeklyFromNotification(target: WeeklyDestination): Promise<boolean> {
    if (this.activeSection() === 'tracking' && this.tracking) return this.tracking.openWeekly(target);
    if (!await this.canLeavePage()) return false;
    this.notificationWeeklyTarget.set(target); this.activeSection.set('tracking');
    return true;
  }
  async openAssignmentFromNotification(id: string): Promise<boolean> {
    if (this.assignments) return this.assignments.reviewAssignment(id);
    if (!await this.canLeavePage()) return false;
    this.notificationAssignmentId.set(id);
    this.activeSection.set('assignments');
    return true;
  }
  @ViewChild(CoachingFinanceComponent) financePanel?: Pick<CoachingFinanceComponent, 'canLeavePage'>;
  async canLeavePage(): Promise<boolean> { return (await this.tracking?.canLeavePage() ?? true) && (await this.study?.canLeavePage()??true) && (await this.profile?.canLeavePage() ?? true) && (await this.financePanel?.canLeavePage() ?? true) && (await this.students?.canLeavePage() ?? true) && (await this.goals?.canLeavePage() ?? true) && (await this.assignments?.canLeavePage() ?? true); }
  readonly activeSection = signal<CoachingTeacherSection>('students');

  async selectSection(section: CoachingTeacherSection): Promise<void> {
    if (section === this.activeSection()) return;
    if (!await this.canLeavePage()) return;
    this.notificationAssignmentId.set(null);
    this.notificationWeeklyTarget.set(null);
    this.activeSection.set(section);
  }

}
