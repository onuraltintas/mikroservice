import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { RoleManagementModalComponent } from './role-management-modal';
import { IdentityService, UserDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

describe('Service-scoped role management', () => {
  const user: UserDto = {
    userId: 'user-1', email: 'teacher@example.test', fullName: 'Test Teacher',
    role: 'Teacher', roles: ['Teacher', 'Teacher'], permissions: [], isActive: true, emailConfirmed: true,
    productRoles: [{ role: 'Teacher', product: 'coaching' }, { role: 'Teacher', product: 'speed-reading' }]
  };
  let identity: { getRoles: ReturnType<typeof vi.fn>; assignRole: ReturnType<typeof vi.fn>;
    removeRole: ReturnType<typeof vi.fn>; getUserById: ReturnType<typeof vi.fn> };
  let toaster: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    identity = { getRoles: vi.fn(() => of(['Teacher', 'Student', 'SystemAdmin'])),
      assignRole: vi.fn(() => of(null)), removeRole: vi.fn(() => of(null)),
      getUserById: vi.fn(() => of({ ...user, roles: ['Teacher'],
        productRoles: [{ role: 'Teacher', product: 'coaching' }] })) };
    toaster = { success: vi.fn(), error: vi.fn() };
    TestBed.configureTestingModule({ imports: [RoleManagementModalComponent], providers: [
      { provide: IdentityService, useValue: identity }, { provide: ToasterService, useValue: toaster }
    ] });
  });

  function create(value = user) {
    const fixture = TestBed.createComponent(RoleManagementModalComponent);
    fixture.componentRef.setInput('userData', value);
    fixture.detectChanges();
    return fixture;
  }

  it('shows both assignments with distinct service names and removal labels', () => {
    const fixture = create();
    expect(fixture.nativeElement.textContent).toContain('Teacher · Koçluk');
    expect(fixture.nativeElement.textContent).toContain('Teacher · Hızlı Okuma');
    expect(fixture.nativeElement.querySelector('[aria-label="Teacher · Hızlı Okuma rolünü kaldır"]')).toBeTruthy();
  });

  it('requires an explicit service before adding a role', () => {
    const fixture = create();
    fixture.componentInstance.assignRole('Student');
    expect(identity.assignRole).not.toHaveBeenCalled();
  });

  it('checks existing roles only within the selected service', () => {
    const fixture = create({ ...user, roles: ['Teacher'], productRoles: [{ role: 'Teacher', product: 'coaching' }] });
    const component = fixture.componentInstance;
    component.selectedProduct.set('speed-reading');
    expect(component.hasRole('Teacher')).toBe(false);
    component.assignRole('Teacher');
    expect(identity.assignRole).toHaveBeenCalledWith('user-1', 'Teacher', 'speed-reading');
    component.selectedProduct.set('coaching');
    expect(component.hasRole('Teacher')).toBe(true);
  });

  it('removes only the clicked service assignment and refreshes scoped roles', () => {
    const fixture = create();
    fixture.nativeElement.querySelector('[aria-label="Teacher · Hızlı Okuma rolünü kaldır"]').click();
    fixture.detectChanges();
    expect(identity.removeRole).toHaveBeenCalledWith('user-1', 'Teacher', 'speed-reading');
    expect(fixture.nativeElement.textContent).toContain('Teacher · Koçluk');
    expect(fixture.nativeElement.textContent).not.toContain('Teacher · Hızlı Okuma');
  });

  it('does not treat a missing service as coaching', () => {
    const fixture = create({ ...user, roles: ['Teacher'], productRoles: undefined });
    const component = fixture.componentInstance;
    component.removeRole({ role: 'Teacher', product: null });
    expect(identity.removeRole).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Servis belirtilmemiş');
  });

  it('uses explicit global removal for SystemAdmin and excludes it from product assignment', () => {
    const fixture = create({ ...user, roles: ['SystemAdmin'], productRoles: [{ role: 'SystemAdmin', product: null }] });
    const component = fixture.componentInstance;
    expect(fixture.nativeElement.textContent).toContain('SystemAdmin · Tüm servisler');
    expect(component.serviceRoles()).not.toContain('SystemAdmin');
    component.selectedProduct.set('speed-reading');
    component.assignRole('SystemAdmin');
    expect(identity.assignRole).not.toHaveBeenCalled();
    component.removeRole({ role: 'SystemAdmin', product: null });
    expect(identity.removeRole).toHaveBeenCalledWith('user-1', 'SystemAdmin', null);
  });

  it('requires a successful reload after a refresh error before further changes', () => {
    identity.getUserById.mockReturnValueOnce(throwError(() => ({ status: 500 })));
    const fixture = create();
    const component = fixture.componentInstance;
    component.removeRole(user.productRoles![1]);
    expect(component.refreshFailed()).toBe(true);
    component.removeRole(user.productRoles![0]);
    expect(identity.removeRole).toHaveBeenCalledTimes(1);
    component.refreshUser();
    expect(component.refreshFailed()).toBe(false);
    expect(component.user()?.productRoles).toEqual([{ role: 'Teacher', product: 'coaching' }]);
  });

  it('keeps roles intact when removal fails', () => {
    identity.removeRole.mockReturnValue(throwError(() => ({ status: 403 })));
    const fixture = create();
    fixture.componentInstance.removeRole(user.productRoles![1]);
    expect(fixture.componentInstance.user()?.productRoles).toEqual(user.productRoles);
    expect(toaster.error).toHaveBeenCalled();
    expect(fixture.componentInstance.actionLoading()).toBeNull();
  });

  it('blocks additional mutations while the refreshed profile is pending', () => {
    const refresh = new Subject<UserDto>();
    identity.getUserById.mockReturnValue(refresh);
    const fixture = create();
    fixture.componentInstance.removeRole(user.productRoles![1]);
    fixture.componentInstance.selectedProduct.set('coaching');
    fixture.componentInstance.assignRole('Student');
    expect(identity.assignRole).not.toHaveBeenCalled();
    refresh.next({ ...user, roles: ['Teacher'], productRoles: [user.productRoles![0]] });
    expect(fixture.componentInstance.actionLoading()).toBeNull();
  });
});
