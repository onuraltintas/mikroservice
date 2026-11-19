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

  it('requires the SystemAdmin role and loads the admin screens lazily', () => {
    for (const path of ['maxkampus/overview', 'maxkampus/site/:view', 'maxkampus/messages', 'maxkampus/subscribers']) {
      const route = DASHBOARD_ROUTES.find(candidate => candidate.path === path);
      expect(route?.canActivate).toContain(permissionGuard);
      expect(route?.data?.['role']).toBe('SystemAdmin');
      expect(route?.loadComponent).toBeTypeOf('function');
    }
    expect(DASHBOARD_ROUTES.find(candidate => candidate.path === 'maxkampus/site/:view')?.data?.['site']).toBe('maxkampus');
    expect(DASHBOARD_ROUTES.find(candidate => candidate.path === 'course-management')?.redirectTo).toBe('maxkampus/overview');
  });

  it('shows the maxkampüs menu to the system admin only', () => {
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
    const menu = () => component.menuItems().find(item => item.label === 'maxkampüs (Kurs Yönetimi)');

    expect(menu()).toBeUndefined();
    // The administrative menus sit under the management permissions every admin has.
    profile.set({ roles: ['InstitutionAdmin'], permissions: ['Permissions.Users.View'] });
    expect(menu()).toBeUndefined();
    profile.set({ roles: ['SystemAdmin'], permissions: ['Permissions.Users.View'] });
    const routes = menu()?.children?.map((child: { route?: string }) => child.route) ?? [];
    expect(routes).toContain('/dashboard/maxkampus/overview');
    expect(routes).toContain('/dashboard/maxkampus/site/blog');
    expect(routes).toContain('/dashboard/maxkampus/messages');
    expect(routes).toContain('/dashboard/maxkampus/subscribers');
  });
});
