import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
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
  readonly canViewTeacher = computed(() => this.views.canSelect('Teacher'));
  readonly canViewStudent = computed(() => this.views.canSelect('Student'));
  readonly canViewParent = computed(() => this.views.canSelect('Parent'));
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
