import { Component, Input, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatBadgeModule } from '@angular/material/badge';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Observable } from 'rxjs';
import { map, shareReplay } from 'rxjs/operators';
import { AuthService } from '../../../core/services/auth.service';
import { NavigationService, MenuItem } from '../../../core/services/navigation.service';
import { NotificationBellComponent } from '../../components/notification-bell/notification-bell.component';
import { AdminContextService } from '../../../core/services/admin-context.service';
import { MatDialog } from '@angular/material/dialog';
import { InstitutionSelectorDialogComponent } from '../../dialogs/institution-selector-dialog.component';
import { TeacherSelectorDialogComponent } from '../../dialogs/teacher-selector-dialog.component';

@Component({
  selector: 'app-base-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatSidenavModule,
    MatListModule,
    MatMenuModule,
    MatBadgeModule,
    MatDividerModule,
    MatTooltipModule,
    NotificationBellComponent
  ],
  templateUrl: './base-layout.component.html',
  styleUrls: ['./base-layout.component.scss']
})
export class BaseLayoutComponent implements OnInit {
  @Input() menuItems: MenuItem[] = [];
  @Input() title: string = 'Speed Reading Platform';
  @Input() role: 'student' | 'teacher' | 'admin' = 'student';

  private breakpointObserver = inject(BreakpointObserver);
  private authService = inject(AuthService);
  private navigationService = inject(NavigationService);

  private router = inject(Router);
  private adminContext = inject(AdminContextService);
  private dialog = inject(MatDialog);

  // Expose context signals to template
  instName = this.adminContext.institutionName;
  teacherName = this.adminContext.teacherName;
  isImpersonatingInst = this.adminContext.isImpersonatingInstitution;
  isImpersonatingTeacher = this.adminContext.isImpersonatingTeacher;

  currentUser = this.authService.currentUserValue;
  expandedMenuItems: Set<string> = new Set();

  // ... existing code ...
  isHandset$: Observable<boolean> = this.breakpointObserver.observe([Breakpoints.Handset, Breakpoints.Tablet])
    .pipe(
      map(result => result.matches),
      shareReplay()
    );

  // Signal or getter to control header visibility
  get showHeader(): boolean {
    const url = this.router.url;

    // Hide header for Exercise Player
    if (url.includes('/student/exercise/')) return false;

    // Hide header for Reading Activity
    if (url.includes('/student/reading/activity/')) return false;

    // Hide header for specific exercises (but show for the list)
    if (url.includes('/student/exercises/') && url !== '/student/exercises') return false;

    return true;
  }

  ngOnInit(): void {
    // ... existing code ...
    // Auto-expand menu items that contain active route
    this.menuItems.forEach(item => {
      if (item.children) {
        const hasActiveChild = item.children.some(child =>
          child.route && this.navigationService.isActive(child.route)
        );
        if (hasActiveChild) {
          this.expandedMenuItems.add(item.label);
        }
      }
    });
  }

  toggleMenuItem(item: MenuItem): void {
    if (this.expandedMenuItems.has(item.label)) {
      this.expandedMenuItems.delete(item.label);
    } else {
      this.expandedMenuItems.add(item.label);
    }
  }

  isMenuItemExpanded(item: MenuItem): boolean {
    return this.expandedMenuItems.has(item.label);
  }

  navigateTo(route: string): void {
    this.navigationService.navigateTo(route);
  }

  isActive(route: string): boolean {
    return this.navigationService.isActive(route);
  }

  isParentActive(item: MenuItem): boolean {
    return !!item.children && item.children.some(child => !!child.route && this.isActive(child.route));
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/auth/login']);
  }

  getUserDisplayName(): string {
    const user = this.currentUser;
    if (!user) return 'Kullanıcı';
    return `${user.firstName} ${user.lastName}`;
  }

  getUserRole(): string {
    switch (this.role) {
      case 'student': return 'Öğrenci';
      case 'teacher': return 'Öğretmen';
      case 'admin': return 'Yönetici';
      default: return '';
    }
  }

  getAvatarLetters(): string {
    const user = this.currentUser;
    if (!user) return 'U';
    return `${user.firstName?.charAt(0) || ''}${user.lastName?.charAt(0) || ''}`.toUpperCase();
  }

  isAdmin(): boolean {
    return this.authService.hasAdminAccess();
  }

  isInstitutionAdmin(): boolean {
    return this.authService.hasRole('InstitutionAdmin');
  }

  isOnlyEditor(): boolean {
    return this.authService.hasRole('Editor') && !this.authService.hasAdminAccess();
  }

  isCoach(): boolean {
    return this.authService.hasRole('Coach');
  }

  openInstitutionSelector() {
    this.dialog.open(InstitutionSelectorDialogComponent, { width: '600px' })
      .afterClosed().subscribe(inst => {
        if (inst) {
          this.adminContext.setInstitutionContext(inst.id, inst.name);
          // Navigate to "Teacher" dashboard which serves Institution Admin
          this.router.navigate(['/teacher/dashboard']);
        }
      });
  }

  openTeacherSelector() {
    this.dialog.open(TeacherSelectorDialogComponent, { width: '600px' })
      .afterClosed().subscribe(teacher => {
        if (teacher) {
          this.adminContext.setTeacherContext(teacher.id, `${teacher.firstName} ${teacher.lastName}`);
          // Navigate to Teacher dashboard
          this.router.navigate(['/teacher/dashboard']);
        }
      });
  }

  exitImpersonation() {
    this.adminContext.clearAll();
    this.router.navigate(['/admin/dashboard']);
  }
  handleItemClick(event: Event, item: MenuItem): void {
    if (item.route === '/admin/impersonate/institution') {
      event.preventDefault();
      this.openInstitutionSelector();
    } else if (item.route === '/admin/impersonate/teacher') {
      event.preventDefault();
      this.openTeacherSelector();
    }
  }

  getRouterLink(route: string | undefined): string | null {
    if (!route) return null;
    if (route === '/admin/impersonate/institution' || route === '/admin/impersonate/teacher') {
      return null;
    }
    return route;
  }
}
