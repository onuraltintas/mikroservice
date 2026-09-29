import { Component, signal } from '@angular/core';
import { CoachingTeacherAssignmentsComponent } from './coaching-teacher-assignments.component';
import { CoachingTeacherSessionsComponent } from './coaching-teacher-sessions.component';
import { CoachingTeacherGoalsComponent } from './coaching-teacher-goals.component';
import { CoachingTeacherStudentsComponent } from './coaching-teacher-students.component';

type CoachingTeacherSection = 'students' | 'assignments' | 'sessions' | 'goals';

@Component({
  selector: 'staff-coaching-teacher-workspace',
  standalone: true,
  imports: [CoachingTeacherStudentsComponent, CoachingTeacherAssignmentsComponent, CoachingTeacherSessionsComponent, CoachingTeacherGoalsComponent],
  templateUrl: './coaching-teacher-workspace.component.html',
  styleUrl: './coaching-teacher-workspace.component.scss'
})
export class CoachingTeacherWorkspaceComponent {
  readonly activeSection = signal<CoachingTeacherSection>('students');

  selectSection(section: CoachingTeacherSection): void {
    this.activeSection.set(section);
  }
}
