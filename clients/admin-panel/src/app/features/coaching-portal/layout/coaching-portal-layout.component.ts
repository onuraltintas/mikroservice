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
import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { CoachingMessagesUnreadService } from '../../../core/services/coaching-messages-unread.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CoachingNotificationPanelComponent } from './coaching-notification-panel.component';

interface PortalLink { label: string; path: string; icon: CoachingIcon; exact?: boolean; short?: string; badge?: number }

const ROOT = '/coaching-portal';
/** Header titles for the sections (T2, 31.10.2026). Longest prefix wins. */
const PAGE_LABELS: [string, string][] = [
  [`${ROOT}/account/settings`, 'Profil ve ayarlar'], [`${ROOT}/account/payments`, 'Ödemeler'], [`${ROOT}/account/data`, 'Kişisel verilerim'],
  [`${ROOT}/plan`, 'Planım'], [`${ROOT}/growth`, 'Gelişimim'], [`${ROOT}/assignments`, 'Ödevlerim'], [`${ROOT}/messages`, 'Mesajlar'],
  [`${ROOT}/sessions`, 'Seanslar'], [`${ROOT}/notifications`, 'Bildirimler']
];

/**
 * T2 (31.10.2026): the student shell — six menu items, a five-item phone bar, notifications behind the bell and an
 * account menu (profile and settings, payments, personal data, sign-out) on the avatar. The colour theme moved to
 * Profil ve ayarlar › Görünüm, so the header has no theme button.
 */
@Component({
  selector: 'app-coaching-portal-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, CoachingIconComponent, CdkTrapFocus, CoachingNotificationPanelComponent],
  templateUrl: './coaching-portal-layout.component.html',
  styleUrl: './coaching-portal-layout.component.scss',
  encapsulation: ViewEncapsulation.None
})
export class CoachingPortalLayoutComponent {
  readonly studentNavigation = inject(CoachingStudentNavigationService);
  readonly unreadCount = inject(NotificationService).unreadCount;
  private readonly messagesUnread = inject(CoachingMessagesUnreadService);
  readonly unreadMessages = this.messagesUnread.count;
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly views = inject(CoachingPortalViewService);
  private readonly staffPortalRedirect = inject(StaffPortalRedirectService);

  readonly user = this.authService.userProfile;
  @ViewChild('menuToggle') private menuToggle?: ElementRef<HTMLButtonElement>;
  @ViewChild('accountToggle') private accountToggle?: ElementRef<HTMLButtonElement>;
  @ViewChild('bellToggle') private bellToggle?: ElementRef<HTMLButtonElement>;
  readonly isStudent = computed(() => this.views.current() === 'Student');
  // Roadmap Y7: a parent who is not also a student sees the parent panel menu.
  readonly isParent = computed(() => !!this.user()?.roles?.includes('Parent') && !this.user()?.roles?.includes('Student'));
  readonly parentLinks: PortalLink[] = [
    { label: 'Çocuklarım', path: `${ROOT}/parent`, icon: 'home', exact: true },
    { label: 'Mesajlar', path: `${ROOT}/messages`, icon: 'chat' },
  ];
  readonly menuOpen = signal(false);
  readonly accountOpen = signal(false);
  readonly notificationsOpen = signal(false);
  readonly pageLabel = signal('Bugün');
  readonly initials = computed(() => (this.user()?.firstName?.charAt(0) || 'Ö') + (this.user()?.lastName?.charAt(0) || ''));
  readonly fullName = computed(() => `${this.user()?.firstName || (this.isParent() ? 'Veli' : 'Öğrenci')} ${this.user()?.lastName ?? ''}`.trim());

  /** The six student destinations; Ödevlerim and Seanslar only when the student has them. */
  readonly links = computed<PortalLink[]>(() => [
    { label: 'Bugün', path: ROOT, icon: 'home', exact: true },
    { label: 'Planım', path: `${ROOT}/plan`, icon: 'plan' },
    ...(this.studentNavigation.showAssignments() ? [{ label: 'Ödevlerim', short: 'Ödevler', path: `${ROOT}/assignments`, icon: 'book' as const }] : []),
    { label: 'Gelişimim', path: `${ROOT}/growth`, icon: 'chart' },
    { label: 'Mesajlar', path: `${ROOT}/messages`, icon: 'chat', badge: this.unreadMessages() },
    ...(this.studentNavigation.showSessions() ? [{ label: 'Seanslar', path: `${ROOT}/sessions`, icon: 'calendar' as const }] : [])
  ]);
  /** The phone bar: everything but Seanslar (the next session also shows on Bugün). */
  readonly mobileLinks = computed(() => this.links().filter(link => link.path !== `${ROOT}/sessions`));
  readonly notificationLabel = computed(() => this.unreadCount() ? `Bildirimler (${this.unreadCount()} okunmamış)` : 'Bildirimler');

  constructor() {
    if (!this.isParent()) { this.studentNavigation.refresh(true); this.messagesUnread.refresh(); }
    this.updatePageLabel();
    this.router.events.pipe(filter(event => event instanceof NavigationEnd), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => {
        this.closeMenu(); this.accountOpen.set(false); this.notificationsOpen.set(false); this.updatePageLabel();
        if (!this.isParent()) { this.studentNavigation.refresh(); this.messagesUnread.refresh(); }
      });
  }

  badgeText(count: number): string { return count > 99 ? '99+' : String(count); }

  @HostListener('document:keydown.escape')
  closeMenu(): void {
    if (this.accountOpen()) { this.accountOpen.set(false); this.accountToggle?.nativeElement.focus(); }
    if (this.notificationsOpen()) { this.notificationsOpen.set(false); this.bellToggle?.nativeElement.focus(); }
    const wasOpen = this.menuOpen();
    this.menuOpen.set(false);
    if (wasOpen) this.menuToggle?.nativeElement.focus();
  }

  /** Clicks outside the account menu or the notification panel close them. */
  @HostListener('document:click', ['$event'])
  closeOnOutsideClick(event: MouseEvent): void {
    const target = event.target as Element | null;
    if (!target?.closest) return;
    if (this.accountOpen() && !target.closest('.coaching-account')) this.accountOpen.set(false);
    if (this.notificationsOpen() && !target.closest('app-coaching-notification-panel') && !target.closest('.coaching-bell')) this.notificationsOpen.set(false);
  }

  toggleAccount(): void { this.notificationsOpen.set(false); this.accountOpen.update(open => !open); }
  toggleNotifications(): void { this.accountOpen.set(false); this.notificationsOpen.update(open => !open); }
  closeNotifications(): void { this.notificationsOpen.set(false); this.bellToggle?.nativeElement.focus(); }

  private updatePageLabel(): void {
    const path = this.router.url.split(/[?#]/)[0];
    if (this.isParent()) { this.pageLabel.set(path.startsWith(`${ROOT}/messages`) ? 'Mesajlar' : path.startsWith(`${ROOT}/parent/children/`) ? 'Çocuğumun haftası' : 'Çocuklarım'); return; }
    const match = PAGE_LABELS.find(([prefix]) => path === prefix || path.startsWith(prefix + '/'));
    this.pageLabel.set(match?.[1] ?? 'Bugün');
  }

  selectView(view: CoachingPortalView): void {
    if (!this.views.canSelect(view)) return;
    if (view === 'Teacher') {
      this.staffPortalRedirect.redirect('coaching');
      return;
    }
    this.views.select(view);
    void this.router.navigate([`${ROOT}/assignments`]);
  }

  async logout() {
    this.accountOpen.set(false);
    await this.authService.logout();
  }
}
