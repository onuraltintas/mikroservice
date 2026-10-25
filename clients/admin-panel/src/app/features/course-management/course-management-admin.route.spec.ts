import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../../core/auth/auth.service';
import { permissionGuard } from '../../core/auth/auth.guard';
import { ToasterService } from '../../core/services/toaster.service';
import { LayoutService } from '../dashboard/services/layout.service';
import { DASHBOARD_ROUTES } from '../dashboard/dashboard.routes';
import { SidebarComponent } from '../dashboard/layout/sidebar/sidebar';

describe('Course Management admin entry points', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('requires the SystemAdmin role and loads the admin screen lazily', () => {
    const route = DASHBOARD_ROUTES.find(candidate => candidate.path === 'course-management');

    expect(route?.canActivate).toContain(permissionGuard);
    expect(route?.data?.['role']).toBe('SystemAdmin');
    expect(route?.loadComponent).toBeTypeOf('function');
  });

  it('shows the Course Management navigation item only to SystemAdmins', () => {
    const profile = signal<{ roles: string[]; permissions: string[] }>({ roles: ['InstitutionAdmin'], permissions: [] });
    TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile, hasPermission: () => false } },
        { provide: ToasterService, useValue: { confirm: vi.fn() } },
        { provide: LayoutService, useValue: { isSidebarCollapsed: signal(false), toggleSidebar: vi.fn() } }
      ]
    });

    const component = TestBed.createComponent(SidebarComponent).componentInstance;
    const hasCourseManagement = () => component.menuItems().some(item => item.route === '/dashboard/course-management');

    expect(hasCourseManagement()).toBe(false);
    profile.set({ roles: ['SystemAdmin'], permissions: [] });
    expect(hasCourseManagement()).toBe(true);
  });
});
