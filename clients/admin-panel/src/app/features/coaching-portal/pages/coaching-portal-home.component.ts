import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AuthService, hasRole } from '../../../core/auth/auth.service';
import { CoachingPortalService, StudentAssignment, TeacherAssignment } from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-coaching-portal-home',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './coaching-portal-home.component.html',
  styleUrl: './coaching-portal-home.component.scss'
})
export class CoachingPortalHomeComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);

  readonly user = this.authService.userProfile;
  readonly isStudent = signal(false);
  readonly isTeacher = signal(false);
  readonly isParent = signal(false);
  readonly assignments = signal<StudentAssignment[]>([]);
  readonly teacherAssignments = signal<TeacherAssignment[]>([]);
  readonly teacherAssignmentCount = signal(0);
  readonly teacherStudentCount = signal(0);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit() {
    const profile = this.user();
    this.isTeacher.set(hasRole(profile, 'Teacher'));
    this.isStudent.set(!this.isTeacher() && hasRole(profile, 'Student'));
    this.isParent.set(!this.isTeacher() && !this.isStudent() && hasRole(profile, 'Parent'));

    if (!profile?.id) {
      this.isLoading.set(false);
      this.errorMessage.set('Oturum profili yüklenemedi. Lütfen tekrar giriş yapın.');
      return;
    }

    if (this.isStudent()) {
      this.coachingService.getStudentAssignments(profile.id, 1, 5).subscribe({
        next: page => this.assignments.set(page.items),
        error: () => {
          this.errorMessage.set('Ödevler şu anda yüklenemedi.');
          this.isLoading.set(false);
        },
        complete: () => this.isLoading.set(false)
      });
      return;
    }

    if (this.isTeacher()) {
      forkJoin({
        assignments: this.coachingService.getTeacherAssignments(profile.id, 1, 5),
        students: this.coachingService.getTeacherStudents(1, 1)
      }).subscribe({
        next: result => {
          this.teacherAssignments.set(result.assignments.items);
          this.teacherAssignmentCount.set(result.assignments.totalCount);
          this.teacherStudentCount.set(result.students.totalCount);
        },
        error: () => {
          this.errorMessage.set('Öğretmen paneli verileri şu anda yüklenemedi.');
          this.isLoading.set(false);
        },
        complete: () => this.isLoading.set(false)
      });
      return;
    }

    this.isLoading.set(false);
  }
}
