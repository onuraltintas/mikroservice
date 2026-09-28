import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService, hasRole } from '../../../core/auth/auth.service';
import { CoachingPortalView, CoachingPortalViewService } from '../coaching-portal-view.service';

@Component({
  selector: 'app-coaching-portal-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './coaching-portal-layout.component.html',
  styleUrl: './coaching-portal-layout.component.scss'
})
export class CoachingPortalLayoutComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly views = inject(CoachingPortalViewService);

  readonly user = this.authService.userProfile;
  readonly canViewTeacher = computed(() => hasRole(this.user(), 'Teacher'));
  readonly canViewStudent = computed(() => hasRole(this.user(), 'Student'));
  readonly canViewParent = computed(() => hasRole(this.user(), 'Parent'));
  readonly isTeacher = computed(() => this.views.current() === 'Teacher');
  readonly isStudent = computed(() => this.views.current() === 'Student');
  readonly isParent = computed(() => this.views.current() === 'Parent');

  selectView(view: CoachingPortalView): void {
    if (!this.views.canSelect(view)) return;
    this.views.select(view);
    const destination = view === 'Teacher' ? '/coaching-portal/teacher/students'
      : view === 'Student' ? '/coaching-portal/assignments' : '/coaching-portal/children';
    void this.router.navigate([destination]);
  }

  async logout() {
    await this.authService.logout();
  }

  goToAdmin() {
    void this.router.navigate(['/dashboard']);
  }
}
