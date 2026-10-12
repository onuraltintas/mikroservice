import { DOCUMENT } from '@angular/common';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { StaffAuthService } from './auth/staff-auth.service';
import { StaffProduct, StaffProductAccess } from './auth/staff-auth.models';
import { HttpRequest } from '@angular/common/http';
import { getStaffAuthRecaptchaAction } from './auth/staff-auth-recaptcha.interceptor';

describe('Course management product isolation', () => {
  it('protects course login with the shared CAPTCHA action', () => {
    expect(getStaffAuthRecaptchaAction(new HttpRequest('POST', '/api/auth/course-management/login', {}))).toBe('auth_login');
  });
  function create(product: StaffProduct, roles: string[]) {
    TestBed.configureTestingModule({ providers: [
      { provide: DOCUMENT, useValue: { defaultView: { location: { search: '?product=course-management' } } } },
      { provide: StaffAuthService, useValue: {
        activeProduct: signal<StaffProduct | null>(product),
        products: signal<StaffProductAccess[]>([{ product, roles }])
      } }
    ] });
    return TestBed.runInInjectionContext(() => new AppComponent());
  }

  it('selects course management from the URL and gives it its own label', () => {
    const component = create('course-management', ['Teacher']);
    expect(component.selectedProduct).toBe('course-management');
    expect(component.currentProductLabel).toBe('Kurs Yönetimi');
  });

  it('does not render existing workspaces for a course teacher', () => {
    const component = create('course-management', ['Teacher']);
    expect(component.activeWorkspaceRole).toBe('teacher');
    expect(component.isTeacherWorkspaceSelected).toBe(false);
    expect(component.isInstitutionWorkspaceSelected).toBe(false);
    expect(component.isSpeedReadingTeacherWorkspaceSelected).toBe(false);
    expect(component.isSpeedReadingInstitutionWorkspaceSelected).toBe(false);
  });

  it('allows the institution and independent teacher views without activating another product', () => {
    const component = create('course-management', ['Teacher', 'InstitutionOwner']);
    expect(component.activeWorkspaceRole).toBe('institution');
    component.selectWorkspaceRole('teacher');
    expect(component.activeWorkspaceRole).toBe('teacher');
    expect(component.isTeacherWorkspaceSelected).toBe(false);
  });
});
