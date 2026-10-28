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
import { DarkModeService } from '../../../core/services/dark-mode.service';
import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { NotificationService } from '../../../core/services/notification.service';

interface PortalLink { label: string; path: string; icon: CoachingIcon; exact?: boolean }
interface MobileLink extends PortalLink { short: string; badge?: number }

@Component({
  selector: 'app-coaching-portal-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, CoachingIconComponent, CdkTrapFocus],
  templateUrl: './coaching-portal-layout.component.html',
  styleUrl: './coaching-portal-layout.component.scss',
  encapsulation: ViewEncapsulation.None
})
export class CoachingPortalLayoutComponent {
  readonly studentNavigation = inject(CoachingStudentNavigationService);
  readonly theme = inject(DarkModeService);
  readonly unreadCount = inject(NotificationService).unreadCount;
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly views = inject(CoachingPortalViewService);
  private readonly staffPortalRedirect = inject(StaffPortalRedirectService);

  readonly user = this.authService.userProfile;
  @ViewChild('menuToggle') private menuToggle?: ElementRef<HTMLButtonElement>;
  readonly isStudent = computed(() => this.views.current() === 'Student');
  // Roadmap Y7: a parent who is not also a student sees the parent panel menu.
  readonly isParent = computed(() => !!this.user()?.roles?.includes('Parent') && !this.user()?.roles?.includes('Student'));
  readonly parentLinks: PortalLink[] = [
    { label: 'Çocuklarım', path: '/coaching-portal/parent', icon: 'home', exact: true },
    { label: 'Mesajlar', path: '/coaching-portal/messages', icon: 'chat' },
  ];
  readonly menuOpen = signal(false);
  readonly pageLabel = signal('Genel Bakış');
  readonly initials = computed(() => (this.user()?.firstName?.charAt(0) || 'Ö') + (this.user()?.lastName?.charAt(0) || ''));
  readonly navigation = computed<{ label: string; links: PortalLink[] }[]>(() => [
    { label: 'Çalışmam', links: [
      { label: 'Genel Bakış', path: '/coaching-portal', icon: 'home', exact: true },
      { label: 'Çalışma planlarım', path: '/coaching-portal/study-plans', icon: 'plan' },
      ...(this.studentNavigation.showAssignments() ? [{ label: 'Ödevlerim', path: '/coaching-portal/assignments', icon: 'book' as const }] : []),
      { label: 'Mesajlar', path: '/coaching-portal/messages', icon: 'chat' },
      { label: 'Plan sihirbazı', path: '/coaching-portal/automatic-plan', icon: 'spark' },
      { label: 'Haftalık düzenim', path: '/coaching-portal/weekly-plan', icon: 'calendar' }
    ] },
    { label: 'Gelişimim', links: [
      { label: 'Hedefim', path: '/coaching-portal/goals', icon: 'target' },
      { label: 'İlerlemem', path: '/coaching-portal/progress', icon: 'chart' },
      { label: 'Sınav sonuçlarım', path: '/coaching-portal/exam-results', icon: 'target' },
      ...(this.studentNavigation.showSessions() ? [{ label: 'Seanslar', path: '/coaching-portal/sessions', icon: 'calendar' as const }] : [])
    ] },
    { label: 'Hesabım', links: [
      { label: 'Profil ve tercihlerim', path: '/coaching-portal/setup', icon: 'book' },
      { label: 'Ayarlar', path: '/coaching-portal/settings', icon: 'clock' },
      { label: 'Bildirimler', path: '/coaching-portal/notifications', icon: 'bell' },
      ...((this.studentNavigation.showFees?.() ?? true) ? [{ label: 'Koçluk ücretlerim', path: '/coaching-portal/finance', icon: 'wallet' as const }] : []),
      { label: 'Abonelik ve ödeme', path: '/coaching-portal/subscription', icon: 'card' }
    ] }
  ]);
  /** Roadmap O1: the four things a student opens most on the phone, with the unread notification count. */
  readonly mobileLinks = computed<MobileLink[]>(() => [
    { label: 'Genel Bakış', short: 'Bugün', path: '/coaching-portal', icon: 'home', exact: true },
    { label: 'Çalışma planlarım', short: 'Planım', path: '/coaching-portal/study-plans', icon: 'plan' },
    this.studentNavigation.showAssignments()
      ? { label: 'Ödevlerim', short: 'Ödevlerim', path: '/coaching-portal/assignments', icon: 'book' }
      : { label: 'Hedefim', short: 'Hedefim', path: '/coaching-portal/goals', icon: 'target' },
    { label: 'Bildirimler', short: 'Bildirimler', path: '/coaching-portal/notifications', icon: 'bell', badge: this.unreadCount() }
  ]);
  readonly notificationLabel = computed(() => this.unreadCount() ? `Bildirimlerimi aç (${this.unreadCount()} okunmamış)` : 'Bildirimlerimi aç');

  constructor() {
    if (!this.isParent()) this.studentNavigation.refresh(true);
    this.updatePageLabel();
    this.router.events.pipe(filter(event => event instanceof NavigationEnd), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => { this.closeMenu(); this.updatePageLabel(); if (!this.isParent()) this.studentNavigation.refresh(); });
  }

  @HostListener('document:keydown.escape')
  closeMenu(): void {
    const wasOpen = this.menuOpen();
    this.menuOpen.set(false);
    if (wasOpen) this.menuToggle?.nativeElement.focus();
  }

  private updatePageLabel(): void {
    const currentPath = this.router.url.split(/[?#]/)[0];
    const path = currentPath === '/coaching-portal/profile' ? '/coaching-portal/setup' : currentPath === '/coaching-portal/study-availability' ? '/coaching-portal/weekly-plan' : currentPath;
    if (this.isParent()) { this.pageLabel.set(path.startsWith('/coaching-portal/messages') ? 'Mesajlar' : path.startsWith('/coaching-portal/parent/children/') ? 'Çocuğumun haftası' : 'Çocuklarım'); return; }
    const link = this.navigation().flatMap(group => group.links).find(item => item.exact ? path === item.path : path.startsWith(item.path));
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
