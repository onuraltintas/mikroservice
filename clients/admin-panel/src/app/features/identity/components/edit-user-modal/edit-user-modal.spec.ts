import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../../core/auth/auth.service';
import { UserProfileDto } from '../../../../core/services/identity.service';
import { InstitutionService } from '../../../../core/services/institution.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { RoleProduct } from '../../../../core/services/user-product-roles';
import { EditUserModalComponent } from './edit-user-modal';

describe('EditUserModalComponent service access', () => {
  it('saves only common account fields through Identity for a Coaching student', () => {
    const { component, http, fixture } = setup('Student');
    expect(fixture.nativeElement.querySelector('[formControlName="studentGradeLevel"]')).toBeNull();
    component.save();
    const request = http.expectOne('/api/users/user-1');
    expect(request.request.body).toEqual({ firstName: 'Test', lastName: 'User', phoneNumber: undefined });
    request.flush(null);
  });
  const administrator: UserProfile = { id: 'admin', email: 'admin@example.test', firstName: 'System', lastName: 'Admin',
    username: 'admin', role: 'SystemAdmin', roles: ['SystemAdmin'], permissions: ['Permissions.Users.Edit'], mfaVerified: true };
  const auth = { userProfile: signal<UserProfile | null>(administrator) };

  function setup(role = 'Student') {
    TestBed.configureTestingModule({ imports: [EditUserModalComponent], providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: AuthService, useValue: auth },
      { provide: InstitutionService, useValue: { getAll: vi.fn(() => of({ items: [] })) } },
      { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } }] });
    auth.userProfile.set(administrator);
    const fixture = TestBed.createComponent(EditUserModalComponent);
    const component = fixture.componentInstance;
    const user: UserProfileDto = { userId: 'user-1', firstName: 'Test', lastName: 'User', fullName: 'Test User', email: 'user@example.test',
      role, roles: [role], isActive: true, emailConfirmed: true, permissions: [],
      productRoles: [{ role, product: 'coaching' }, { role, product: 'speed-reading' }],
      productAccesses: [{ product: 'coaching', isActive: true }, { product: 'speed-reading', isActive: true }, { product: 'course-management', isActive: true }] };
    component.userData = user;
    fixture.detectChanges();
    return { fixture, component, user, http: TestBed.inject(HttpTestingController) };
  }

  it.each(['Student', 'Teacher', 'InstitutionAdmin', 'InstitutionOwner'].flatMap(role =>
    (['coaching', 'speed-reading'] as RoleProduct[]).map(product => ({ role, product }))))(
    'closes and reopens $product access for $role without changing roles', ({ role, product }) => {
      const { component, http, user } = setup(role);
      const other = product === 'coaching' ? 'speed-reading' : 'coaching';
      const close = vi.spyOn(component.close, 'emit');
      component.setProductAccess(product, false);
      component.setProductAccess(other, false);
      component.closeModal();
      const request = http.expectOne(`/api/users/user-1/products/${product}/access`);
      expect(request.request.body).toEqual({ isActive: false });
      expect(close).not.toHaveBeenCalled();
      expect(component.hasProductAccess(product)).toBe(true);
      request.flush(null);
      expect(component.hasProductAccess(product)).toBe(false);
      expect(component.hasProductAccess(other)).toBe(true);
      expect(component.user()?.productRoles).toEqual(user.productRoles);
      expect(component.user()?.productAccesses).toContainEqual({ product: 'course-management', isActive: true });
      expect(component.form.dirty).toBe(false);
      component.setProductAccess(product, true);
      http.expectOne(`/api/users/user-1/products/${product}/access`).flush(null);
      expect(component.hasProductAccess(product)).toBe(true);
      component.closeModal();
      expect(close).toHaveBeenCalledWith(true);
      http.verify();
    });

  it('can close both service accesses without changing profile fields', () => {
    const { component, http } = setup();
    const profile = component.form.getRawValue();
    for (const product of ['coaching', 'speed-reading'] as const) {
      component.setProductAccess(product, false);
      http.expectOne(`/api/users/user-1/products/${product}/access`).flush(null);
    }
    expect(component.hasProductAccess('coaching')).toBe(false);
    expect(component.hasProductAccess('speed-reading')).toBe(false);
    expect(component.form.getRawValue()).toEqual(profile);
    http.verify();
  });

  it('preserves the existing state and shows the server message on failure', () => {
    const { component, fixture, http } = setup();
    component.setProductAccess('coaching', false);
    http.expectOne('/api/users/user-1/products/coaching/access').flush({ message: 'MFA doğrulaması gerekiyor.' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(component.hasProductAccess('coaching')).toBe(true);
    expect(component.accessLoading()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('MFA doğrulaması gerekiyor.');
    component.setProductAccess('coaching', false);
    http.expectOne('/api/users/user-1/products/coaching/access').flush(null);
    expect(component.hasProductAccess('coaching')).toBe(false);
    http.verify();
  });

  it.each(['mfa', 'permission', 'role'])('does not mutate access without %s', requirement => {
    const { component, fixture, http } = setup();
    auth.userProfile.set({ ...administrator, mfaVerified: requirement !== 'mfa',
      permissions: requirement === 'permission' ? [] : administrator.permissions,
      roles: requirement === 'role' ? ['Teacher'] : ['SystemAdmin'] });
    fixture.detectChanges();
    component.setProductAccess('coaching', false);
    expect(fixture.nativeElement.querySelectorAll('button[aria-label$="erişimini kapat"]').length).toBe(0);
    http.expectNone('/api/users/user-1/products/coaching/access');
    http.verify();
  });

  it('explains that global administrators cannot be restricted per service', () => {
    const { component, fixture, http } = setup('SystemAdmin');
    component.setProductAccess('coaching', false);
    expect(fixture.nativeElement.textContent).toContain('servis erişimiyle kısıtlanamaz');
    http.expectNone('/api/users/user-1/products/coaching/access');
    http.verify();
  });

  it('allows access without copying a role to the other service', () => {
    const { component, fixture, http, user } = setup();
    component.userData = { ...user, productAccesses: [], productRoles: [{ role: 'Student', product: 'coaching' }] };
    component.setProductAccess('speed-reading', true);
    http.expectOne('/api/users/user-1/products/speed-reading/access').flush(null);
    fixture.detectChanges();
    expect(component.hasProductAccess('speed-reading')).toBe(true);
    expect(component.rolesForProduct('speed-reading')).toBe('');
    expect(fixture.nativeElement.textContent).toContain('Roller bölümünden ayrıca tanımlayın.');
    http.verify();
  });
});
