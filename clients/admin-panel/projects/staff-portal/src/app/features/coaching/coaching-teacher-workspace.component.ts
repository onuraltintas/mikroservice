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

type CoachingTeacherSection = 'students' | 'assignments' | 'sessions' | 'goals' | 'exams' | 'subscription' | 'finance' | 'profile' | 'study';

@Component({
  selector: 'staff-coaching-teacher-workspace',
  standalone: true,
  imports: [CoachingTeacherStudentsComponent, CoachingTeacherAssignmentsComponent, CoachingTeacherSessionsComponent, CoachingTeacherGoalsComponent, CoachingTeacherExamsComponent, CoachingTeacherSubscriptionComponent, CoachingFinanceComponent, CoachingProfileComponent,CoachingTeacherStudyOverviewComponent],
  templateUrl: './coaching-teacher-workspace.component.html',
  styleUrl: './coaching-teacher-workspace.component.scss'
})
export class CoachingTeacherWorkspaceComponent {
  readonly apiUrl = environment.apiUrl;
  @ViewChild(CoachingProfileComponent) private profile?: CoachingProfileComponent;
  @ViewChild(CoachingTeacherStudyOverviewComponent) private study?:CoachingTeacherStudyOverviewComponent;
  @ViewChild(CoachingTeacherStudentsComponent) private students?: CoachingTeacherStudentsComponent;
  @ViewChild(CoachingTeacherGoalsComponent) private goals?: CoachingTeacherGoalsComponent;
  @ViewChild(CoachingFinanceComponent) financePanel?: Pick<CoachingFinanceComponent, 'canLeavePage'>;
  canLeavePage(): boolean { return (this.study?.canLeavePage()??true) && (this.profile?.canLeavePage() ?? true) && (this.financePanel?.canLeavePage() ?? true) && (this.students?.canLeavePage() ?? true) && (this.goals?.canLeavePage() ?? true); }
  readonly activeSection = signal<CoachingTeacherSection>('students');

  selectSection(section: CoachingTeacherSection): void {
    if (section === this.activeSection()) return;
    if (!this.canLeavePage()) return;
    this.activeSection.set(section);
  }
}
