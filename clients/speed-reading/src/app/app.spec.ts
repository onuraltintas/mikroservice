import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { App } from './app';
import { routes } from './app.routes';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient()]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the application shell', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });

  it('keeps legacy admin URLs as a central-panel redirect only', () => {
    const adminRoute = routes.find(route => route.path === 'admin');

    expect(adminRoute?.canActivate).toBeUndefined();
    expect(adminRoute?.children?.some(route => route.path === '**')).toBeTrue();
  });

  it('allows platform administrators to open the Master exercise preview', () => {
    const studentRoute = routes.find(route => route.path === 'student');
    const roles = studentRoute?.data?.['role'] as string[] | undefined;

    expect(roles ?? []).toContain('Admin');
    expect(roles ?? []).toContain('SystemAdmin');

    for (const path of ['teacher', 'coaching']) {
      const route = routes.find(candidate => candidate.path === path);
      const routeRoles = route?.data?.['role'] as string[] | undefined;
      expect(routeRoles ?? []).not.toContain('Admin');
      expect(routeRoles ?? []).not.toContain('SystemAdmin');
    }
  });
});
