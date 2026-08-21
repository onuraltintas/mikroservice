import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, CoachingSession } from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-coaching-sessions',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './coaching-sessions.component.html',
  styleUrl: './coaching-sessions.component.scss'
})
export class CoachingSessionsComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);

  readonly sessions = signal<CoachingSession[]>([]);
  readonly isTeacher = signal(false);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit() {
    const profile = this.authService.userProfile();
    this.isTeacher.set(profile?.role === 'Teacher');
    if (!profile?.id || !['Student', 'Teacher'].includes(profile.role)) {
      this.isLoading.set(false);
      this.errorMessage.set('Seans bilgisi için öğrenci veya öğretmen profili bulunamadı.');
      return;
    }

    const request = this.isTeacher()
      ? this.coachingService.getTeacherSessions(profile.id, 1, 100)
      : this.coachingService.getStudentSessions(profile.id, 1, 100);

    request.subscribe({
      next: page => this.sessions.set(page.items),
      error: () => {
        this.errorMessage.set('Seanslar yüklenemedi. Lütfen tekrar deneyin.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
    });
  }

  upcomingSessions() {
    return this.sessions().filter(session => new Date(session.startTime).getTime() >= Date.now());
  }

  trackById(_: number, session: CoachingSession) {
    return session.id;
  }
}
