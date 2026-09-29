import { Component, signal } from '@angular/core';
import { CoachingTeacherAssignmentsComponent } from './coaching-teacher-assignments.component';
import { CoachingTeacherSessionsComponent } from './coaching-teacher-sessions.component';
import { CoachingTeacherStudentsComponent } from './coaching-teacher-students.component';

type CoachingTeacherSection = 'students' | 'assignments' | 'sessions';

@Component({
  selector: 'staff-coaching-teacher-workspace',
  standalone: true,
  imports: [CoachingTeacherStudentsComponent, CoachingTeacherAssignmentsComponent, CoachingTeacherSessionsComponent],
  templateUrl: './coaching-teacher-workspace.component.html',
  styleUrl: './coaching-teacher-workspace.component.scss'
})
export class CoachingTeacherWorkspaceComponent {
  readonly activeSection = signal<CoachingTeacherSection>('students');

  selectSection(section: CoachingTeacherSection): void {
    this.activeSection.set(section);
  }
}
