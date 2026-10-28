import { StudentHomeGoalComponent } from './student-home-goal.component';
import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import { StaffPortalRedirectService } from '../staff-portal-redirect.service';
import { StudentDailyAgendaComponent } from './student-daily-agenda.component';
import { CoachingIconComponent } from '../coaching-icon.component';
import { LearningSetupEntryComponent } from './learning-setup-entry.component';
import { StudentWeeklyHomeComponent } from './student-weekly-home.component';

@Component({
  selector: 'app-coaching-portal-home',
  standalone: true,
  imports: [StudentHomeGoalComponent, CommonModule, RouterLink, StudentDailyAgendaComponent, CoachingIconComponent, LearningSetupEntryComponent, StudentWeeklyHomeComponent],
  templateUrl: './coaching-portal-home.component.html',
  styleUrl: './coaching-portal-home.component.scss'
})
export class CoachingPortalHomeComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly views = inject(CoachingPortalViewService);
  private readonly staffPortalRedirect = inject(StaffPortalRedirectService);

  readonly user = this.authService.userProfile;
  readonly isStudent = signal(false);
  readonly isTeacher = signal(false);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit() {
    const profile = this.user();
    this.isTeacher.set(this.views.current() === 'Teacher');
    this.isStudent.set(this.views.current() === 'Student');
    if (this.isTeacher()) {
      this.staffPortalRedirect.redirect('coaching');
      return;
    }

    if (!profile?.id) {
      this.errorMessage.set('Oturum profili yüklenemedi. Yeniden giriş yap.');
      return;
    }

  }
  private readonly feedback = coachingFeedback(undefined, this.errorMessage);
}
