import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, HostListener, ViewChild, ViewEncapsulation, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalView, CoachingPortalViewService } from '../coaching-portal-view.service';
import { StaffPortalRedirectService } from '../staff-portal-redirect.service';
import { CoachingIcon, CoachingIconComponent } from '../coaching-icon.component';

interface PortalLink { label: string; path: string; icon: CoachingIcon; exact?: boolean }

@Component({
  selector: 'app-coaching-portal-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, CoachingIconComponent, CdkTrapFocus],
  templateUrl: './coaching-portal-layout.component.html',
  styleUrls: ['./coaching-portal-layout.component.scss', './coaching-student-surfaces.scss'],
  encapsulation: ViewEncapsulation.None
})
export class CoachingPortalLayoutComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly views = inject(CoachingPortalViewService);
  private readonly staffPortalRedirect = inject(StaffPortalRedirectService);

  readonly user = this.authService.userProfile;
  @ViewChild('menuToggle') private menuToggle?: ElementRef<HTMLButtonElement>;
  readonly isStudent = computed(() => this.views.current() === 'Student');
  readonly menuOpen = signal(false);
  readonly pageLabel = signal('Genel Bakış');
  readonly initials = computed(() => (this.user()?.firstName?.charAt(0) || 'Ö') + (this.user()?.lastName?.charAt(0) || ''));
  readonly navigation: { label: string; links: PortalLink[] }[] = [
    { label: 'Çalışmam', links: [
      { label: 'Genel Bakış', path: '/coaching-portal', icon: 'home', exact: true },
      { label: 'Çalışma planlarım', path: '/coaching-portal/study-plans', icon: 'plan' },
      { label: 'Ödevlerim', path: '/coaching-portal/assignments', icon: 'book' },
      { label: 'Çalışma saatlerim', path: '/coaching-portal/study-availability', icon: 'clock' },
      { label: 'Otomatik plan', path: '/coaching-portal/automatic-plan', icon: 'spark' }
    ] },
    { label: 'Gelişimim', links: [
      { label: 'İlerlemem', path: '/coaching-portal/progress', icon: 'chart' },
      { label: 'Sınav sonuçlarım', path: '/coaching-portal/exam-results', icon: 'target' },
      { label: 'Seanslar', path: '/coaching-portal/sessions', icon: 'calendar' }
    ] },
    { label: 'Hesabım', links: [
      { label: 'Bildirimler', path: '/coaching-portal/notifications', icon: 'bell' },
      { label: 'Ücretlerim', path: '/coaching-portal/finance', icon: 'wallet' },
      { label: 'Abonelik ve ödeme', path: '/coaching-portal/subscription', icon: 'card' }
    ] }
  ];
  readonly mobileLinks = this.navigation[0].links.slice(0, 3).concat(this.navigation[1].links[0]);

  constructor() {
    this.updatePageLabel();
    this.router.events.pipe(filter(event => event instanceof NavigationEnd), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => { this.closeMenu(); this.updatePageLabel(); });
  }

  @HostListener('document:keydown.escape')
  closeMenu(): void {
    const wasOpen = this.menuOpen();
    this.menuOpen.set(false);
    if (wasOpen) this.menuToggle?.nativeElement.focus();
  }

  private updatePageLabel(): void {
    const path = this.router.url.split(/[?#]/)[0];
    const link = this.navigation.flatMap(group => group.links).find(item => item.exact ? path === item.path : path.startsWith(item.path));
    this.pageLabel.set(link?.label ?? 'Çalışma alanın');
  }

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
