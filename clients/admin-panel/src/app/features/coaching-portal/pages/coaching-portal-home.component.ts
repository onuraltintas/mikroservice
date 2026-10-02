import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import { CoachingPortalService, StudentAssignment } from '../../../core/services/coaching-portal.service';
import { StaffPortalRedirectService } from '../staff-portal-redirect.service';
import { StudentStudyTodayComponent } from './student-study-today.component';

@Component({
  selector: 'app-coaching-portal-home',
  standalone: true,
  imports: [CommonModule, RouterLink, StudentStudyTodayComponent],
  templateUrl: './coaching-portal-home.component.html',
  styleUrl: './coaching-portal-home.component.scss'
})
export class CoachingPortalHomeComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly views = inject(CoachingPortalViewService);
  private readonly coachingService = inject(CoachingPortalService);
  private readonly staffPortalRedirect = inject(StaffPortalRedirectService);

  readonly user = this.authService.userProfile;
  readonly isStudent = signal(false);
  readonly isTeacher = signal(false);
  readonly assignments = signal<StudentAssignment[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit() {
    const profile = this.user();
    this.isTeacher.set(this.views.current() === 'Teacher');
    this.isStudent.set(this.views.current() === 'Student');
    if (this.isTeacher()) {
      this.isLoading.set(false);
      this.staffPortalRedirect.redirect('coaching');
      return;
    }

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

    this.isLoading.set(false);
  }
}
