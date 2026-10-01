import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalView, CoachingPortalViewService } from '../coaching-portal-view.service';
import { StaffPortalRedirectService } from '../staff-portal-redirect.service';

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
  private readonly staffPortalRedirect = inject(StaffPortalRedirectService);

  readonly user = this.authService.userProfile;
  readonly isStudent = computed(() => this.views.current() === 'Student');

  selectView(view: CoachingPortalView): void {
    if (!this.views.canSelect(view)) return;
    if (view === 'Teacher') {
      this.staffPortalRedirect.redirect('coaching');
      return;
    }
    this.views.select(view);
    void this.router.navigate(['/coaching-portal/assignments']);
  }

  async logout() {
    await this.authService.logout();
  }

  goToAdmin() {
    void this.router.navigate(['/dashboard']);
  }
}
