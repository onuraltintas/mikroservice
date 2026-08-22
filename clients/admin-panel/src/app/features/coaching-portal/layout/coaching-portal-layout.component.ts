import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService, hasRole } from '../../../core/auth/auth.service';

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

  readonly user = this.authService.userProfile;
  readonly isTeacher = computed(() => hasRole(this.user(), 'Teacher'));
  readonly isStudent = computed(() => !this.isTeacher() && hasRole(this.user(), 'Student'));
  readonly isParent = computed(() => !this.isTeacher() && !this.isStudent() && hasRole(this.user(), 'Parent'));

  async logout() {
    await this.authService.logout();
  }

  goToAdmin() {
    void this.router.navigate(['/dashboard']);
  }
}
