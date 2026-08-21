import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';

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
  readonly isStudent = computed(() => this.user()?.role === 'Student');
  readonly isTeacher = computed(() => this.user()?.role === 'Teacher');
  readonly isParent = computed(() => this.user()?.role === 'Parent');

  async logout() {
    await this.authService.logout();
  }

  goToAdmin() {
    void this.router.navigate(['/dashboard']);
  }
}
