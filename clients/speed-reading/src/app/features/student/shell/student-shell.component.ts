import {
  Component, OnInit, OnDestroy, inject, signal, computed,
  HostListener, ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { filter, takeUntil } from 'rxjs/operators';
import { Subject } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { NavigationService, MenuItem } from '../../../core/services/navigation.service';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-student-shell',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './student-shell.component.html',
  styleUrls: ['./student-shell.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentShellComponent implements OnInit, OnDestroy {
  private readonly authService  = inject(AuthService);
  private readonly navService   = inject(NavigationService);
  private readonly router       = inject(Router);
  private readonly destroy$     = new Subject<void>();
  readonly themeService         = inject(ThemeService);

  // ── State ──────────────────────────────────────────────────────────────────
  menuItems        = signal<MenuItem[]>([]);
  sidebarOpen      = signal(false);   // mobile drawer
  userMenuOpen     = signal(false);
  currentUrl       = signal(this.router.url);
  isTeacherPreview = signal(false);

  // ── Computed ───────────────────────────────────────────────────────────────
  /** Hide shell header & sidebar for the exercise player */
  showHeader = computed(() =>
    !this.currentUrl().includes('/exercises/universal-player/')
  );

  user = computed(() => this.authService.currentUserValue);

  avatarLetters = computed(() => {
    const u = this.user();
    if (!u) return '?';
    const first = u.firstName?.[0] ?? '';
    const last  = u.lastName?.[0]  ?? '';
    return (first + last).toUpperCase() || u.email?.[0]?.toUpperCase() || '?';
  });

  // ── Lifecycle ──────────────────────────────────────────────────────────────
  ngOnInit(): void {
    const isTeacher        = this.authService.hasRole('Teacher');
    const isInstitutionAdmin = this.authService.hasRole('InstitutionAdmin');
    const preview          = isTeacher || isInstitutionAdmin;
    this.isTeacherPreview.set(preview);

    if (preview) {
      const label = isInstitutionAdmin ? 'Kurum Paneli' : 'Öğretmen Paneli';
      this.menuItems.set([{ label, icon: 'arrow_back', route: '/teacher/dashboard' }]);
    } else {
      this.menuItems.set(this.navService.getStudentMenuItems());
    }

    // Track URL changes to highlight active nav items and handle player visibility
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      takeUntil(this.destroy$)
    ).subscribe((e: any) => {
      this.currentUrl.set(e.urlAfterRedirects ?? e.url);
      this.sidebarOpen.set(false); // close mobile drawer on navigation
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Helpers ────────────────────────────────────────────────────────────────
  isActive(route?: string): boolean {
    if (!route) return false;
    const url = this.currentUrl();
    return url === route || url.startsWith(route + '/');
  }

  toggleSidebar(): void {
    this.sidebarOpen.update(v => !v);
  }

  toggleUserMenu(): void {
    this.userMenuOpen.update(v => !v);
  }

  closeUserMenu(): void {
    this.userMenuOpen.set(false);
  }

  logout(): void {
    this.authService.logout();
  }

  navigate(route?: string): void {
    if (route) this.router.navigate([route]);
  }

  // Close dropdown when clicking outside
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.user-menu-container')) {
      this.userMenuOpen.set(false);
    }
  }
}
