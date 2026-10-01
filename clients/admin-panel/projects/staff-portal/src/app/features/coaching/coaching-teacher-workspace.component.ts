import { Component, signal } from '@angular/core';
import { CoachingTeacherAssignmentsComponent } from './coaching-teacher-assignments.component';
import { CoachingTeacherSessionsComponent } from './coaching-teacher-sessions.component';
import { CoachingTeacherGoalsComponent } from './coaching-teacher-goals.component';
import { CoachingTeacherStudentsComponent } from './coaching-teacher-students.component';
import { CoachingTeacherExamsComponent } from './coaching-teacher-exams.component';
import { CoachingTeacherSubscriptionComponent } from './coaching-teacher-subscription.component';

type CoachingTeacherSection = 'students' | 'assignments' | 'sessions' | 'goals' | 'exams' | 'subscription';

@Component({
  selector: 'staff-coaching-teacher-workspace',
  standalone: true,
  imports: [CoachingTeacherStudentsComponent, CoachingTeacherAssignmentsComponent, CoachingTeacherSessionsComponent, CoachingTeacherGoalsComponent, CoachingTeacherExamsComponent, CoachingTeacherSubscriptionComponent],
  templateUrl: './coaching-teacher-workspace.component.html',
  styleUrl: './coaching-teacher-workspace.component.scss'
})
export class CoachingTeacherWorkspaceComponent {
  readonly activeSection = signal<CoachingTeacherSection>('students');

  selectSection(section: CoachingTeacherSection): void {
    this.activeSection.set(section);
  }
}
