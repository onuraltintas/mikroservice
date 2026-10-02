import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../../core/auth/permissions';
import { ToasterService } from '../../../../core/services/toaster.service';
import { LayoutService } from '../../services/layout.service';
import { SidebarComponent } from './sidebar';

describe('SidebarComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('shows the shared coaching catalog only to a permitted global admin', () => {
    const profile = signal<{ roles: string[]; permissions: string[] }>({ roles: ['SystemAdmin'], permissions: [ADMIN_PERMISSIONS.coachingView] });
    TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile, hasPermission: (permission: string) => profile().permissions.includes(permission) } },
        { provide: ToasterService, useValue: { confirm: vi.fn() } },
        { provide: LayoutService, useValue: { isSidebarCollapsed: signal(false), toggleSidebar: vi.fn() } }
      ]
    });
    const component = TestBed.createComponent(SidebarComponent).componentInstance;
    const hasCatalog = () => component.menuItems().some(menu => menu.children?.some((child: { route?: string }) => child.route === '/dashboard/coaching/catalog'));
    expect(hasCatalog()).toBe(true);
    profile.set({ roles: ['InstitutionAdmin'], permissions: [ADMIN_PERMISSIONS.coachingView] });
    expect(hasCatalog()).toBe(false);
    profile.set({ roles: ['SystemAdmin'], permissions: [] });
    expect(hasCatalog()).toBe(false);
  });

  it('shows shared legal pages to SystemAdmins with the legal-management permission', () => {
    const profile = signal<{ roles: string[]; permissions: string[] }>({
      roles: ['SystemAdmin'],
      permissions: [ADMIN_PERMISSIONS.usersView, ADMIN_PERMISSIONS.privacyManage]
    });
    TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile, hasPermission: (permission: string) => profile().permissions.includes(permission) } },
        { provide: ToasterService, useValue: { confirm: vi.fn() } },
        { provide: LayoutService, useValue: { isSidebarCollapsed: signal(false), toggleSidebar: vi.fn() } }
      ]
    });

    const component = TestBed.createComponent(SidebarComponent).componentInstance;
    const userMenu = component.menuItems().find(item => item.label === 'Kullanıcı İşlemleri');

    expect(userMenu?.children?.some((item: { route?: string }) => item.route === '/dashboard/identity/legal-pages')).toBe(true);
  });

  it('keeps shared legal pages hidden without the legal-management permission', () => {
    const profile = signal<{ roles: string[]; permissions: string[] }>({ roles: ['SystemAdmin'], permissions: [ADMIN_PERMISSIONS.usersView] });
    TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile, hasPermission: (permission: string) => profile().permissions.includes(permission) } },
        { provide: ToasterService, useValue: { confirm: vi.fn() } },
        { provide: LayoutService, useValue: { isSidebarCollapsed: signal(false), toggleSidebar: vi.fn() } }
      ]
    });

    const component = TestBed.createComponent(SidebarComponent).componentInstance;
    const userMenu = component.menuItems().find(item => item.label === 'Kullanıcı İşlemleri');

    expect(userMenu?.children?.some((item: { route?: string }) => item.route === '/dashboard/identity/legal-pages')).toBe(false);
  });
});
