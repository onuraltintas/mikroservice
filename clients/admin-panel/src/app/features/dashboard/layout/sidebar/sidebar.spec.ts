import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../../core/auth/permissions';
import { ToasterService } from '../../../../core/services/toaster.service';
import { LayoutService } from '../../services/layout.service';
import { SidebarComponent } from './sidebar';
import { IDENTITY_ROUTES } from '../../../identity/identity.routes';

describe('SidebarComponent', () => {
  it('offers curriculum management only to global content managers', () => {
    const profile = signal({roles:['SystemAdmin'],permissions:[ADMIN_PERMISSIONS.coachingView,ADMIN_PERMISSIONS.coachingContentManage] as string[]});
    TestBed.configureTestingModule({imports:[SidebarComponent],providers:[
      {provide:AuthService,useValue:{userProfile:profile,hasPermission:(p:string)=>profile().permissions.includes(p)}},
      {provide:ToasterService,useValue:{confirm:vi.fn()}},
      {provide:LayoutService,useValue:{isSidebarCollapsed:signal(false),toggleSidebar:vi.fn()}}
    ]});
    const component=TestBed.createComponent(SidebarComponent).componentInstance;
    const visible=()=>component.menuItems().some(x=>x.children?.some((child:{route?:string})=>child.route==='/dashboard/coaching/curriculum'));
    expect(visible()).toBe(true);
    profile.set({roles:['InstitutionAdmin'],permissions:[ADMIN_PERMISSIONS.coachingView,ADMIN_PERMISSIONS.coachingContentManage]});expect(visible()).toBe(false);
    profile.set({roles:['SystemAdmin'],permissions:[ADMIN_PERMISSIONS.coachingView]});expect(visible()).toBe(false);
  });
  afterEach(() => TestBed.resetTestingModule());

  // Coaching audit K3 (31.10.2026): the old parent–student page had no gateway route; parents link through the parent panel.
  it('no longer offers the removed parent–student relationships page', () => {
    const profile = signal({ roles: ['SystemAdmin'], permissions: Object.values(ADMIN_PERMISSIONS) as string[] });
    TestBed.configureTestingModule({ imports: [SidebarComponent], providers: [
      { provide: AuthService, useValue: { userProfile: profile, hasPermission: () => true } },
      { provide: ToasterService, useValue: { confirm: vi.fn() } },
      { provide: LayoutService, useValue: { isSidebarCollapsed: signal(false), toggleSidebar: vi.fn() } }
    ] });
    const component = TestBed.createComponent(SidebarComponent).componentInstance;
    const routes = component.menuItems().flatMap(menu => (menu.children ?? []).map((child: { route?: string }) => child.route));
    expect(routes).not.toContain('/dashboard/identity/parent-student-relationships');
    const children = IDENTITY_ROUTES.flatMap(route => route.children ?? [route]);
    expect(children.some(route => route.path === 'parent-student-relationships')).toBe(false);
  });

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
