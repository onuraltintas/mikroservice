import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { LAST_PRODUCT_ROLE_CONFIRMATION, RoleManagementModalComponent } from './role-management-modal';
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
  let toaster: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn>; warning: ReturnType<typeof vi.fn>; confirm: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    identity = { getRoles: vi.fn(() => of(['Teacher', 'Student', 'SystemAdmin'])),
      assignRole: vi.fn(() => of(null)), removeRole: vi.fn(() => of(null)),
      getUserById: vi.fn(() => of({ ...user, roles: ['Teacher'],
        productRoles: [{ role: 'Teacher', product: 'coaching' }] })) };
    toaster = { success: vi.fn(), error: vi.fn(), warning: vi.fn(), confirm: vi.fn().mockResolvedValue(true) };
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

  it('shows both assignments with distinct service names and removal labels', async () => {
    const fixture = create();
    expect(fixture.nativeElement.textContent).toContain('Teacher · Koçluk');
    expect(fixture.nativeElement.textContent).toContain('Teacher · Hızlı Okuma');
    expect(fixture.nativeElement.querySelector('[aria-label="Teacher · Hızlı Okuma rolünü kaldır"]')).toBeTruthy();
  });

  it('requires an explicit service before adding a role', async () => {
    const fixture = create();
    fixture.componentInstance.assignRole('Student');
    expect(identity.assignRole).not.toHaveBeenCalled();
  });

  it('checks existing roles only within the selected service', async () => {
    const fixture = create({ ...user, roles: ['Teacher'], productRoles: [{ role: 'Teacher', product: 'coaching' }] });
    const component = fixture.componentInstance;
    component.selectedProduct.set('speed-reading');
    expect(component.hasRole('Teacher')).toBe(false);
    component.assignRole('Teacher');
    expect(identity.assignRole).toHaveBeenCalledWith('user-1', 'Teacher', 'speed-reading');
    component.selectedProduct.set('coaching');
    expect(component.hasRole('Teacher')).toBe(true);
  });

  it('removes only the clicked service assignment and refreshes scoped roles', async () => {
    const fixture = create();
    fixture.nativeElement.querySelector('[aria-label="Teacher · Hızlı Okuma rolünü kaldır"]').click();
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();
    expect(identity.removeRole).toHaveBeenCalledWith('user-1', 'Teacher', 'speed-reading');
    expect(fixture.nativeElement.textContent).toContain('Teacher · Koçluk');
    expect(fixture.nativeElement.textContent).not.toContain('Teacher · Hızlı Okuma');
  });

  // Paket F F4 (user decision 07.11.2026): removing a service's last role also closes its access, so ask first.
  it('asks before removing the last role of a service and keeps it when cancelled', async () => {
    toaster.confirm.mockResolvedValueOnce(false);
    const fixture = create({ ...user, roles: ['Student', 'Student'],
      productRoles: [{ role: 'Student', product: 'coaching' }, { role: 'Student', product: 'speed-reading' }] });
    await fixture.componentInstance.removeRole({ role: 'Student', product: 'coaching' });
    expect(toaster.confirm).toHaveBeenCalledWith(LAST_PRODUCT_ROLE_CONFIRMATION, expect.anything());
    expect(LAST_PRODUCT_ROLE_CONFIRMATION).toBe(
      'Bu, kullanıcının bu servisteki son rolü. Kaldırırsan bu servise erişimi de kapanır ve giriş yapamaz. Devam edilsin mi?');
    expect(identity.removeRole).not.toHaveBeenCalled();
  });

  it('removes the last role of a service after confirmation', async () => {
    const fixture = create({ ...user, roles: ['Student', 'Student'],
      productRoles: [{ role: 'Student', product: 'coaching' }, { role: 'Student', product: 'speed-reading' }] });
    await fixture.componentInstance.removeRole({ role: 'Student', product: 'coaching' });
    expect(identity.removeRole).toHaveBeenCalledWith('user-1', 'Student', 'coaching');
  });

  // PR #125 re-review M-2: a soft-deleted role is shown but does not count as another live role.
  it('asks before removing the last live role even when a soft-deleted role remains', async () => {
    const fixture = create({ ...user, roles: ['Student', 'RetiredRole'],
      productRoles: [{ role: 'Student', product: 'coaching' }, { role: 'RetiredRole', product: 'coaching', roleDeleted: true }] });
    await fixture.componentInstance.removeRole({ role: 'Student', product: 'coaching' });
    expect(toaster.confirm).toHaveBeenCalledWith(LAST_PRODUCT_ROLE_CONFIRMATION, expect.anything());
  });

  it('does not ask while the service still has another role', async () => {
    const fixture = create({ ...user, roles: ['Teacher', 'Student'],
      productRoles: [{ role: 'Teacher', product: 'coaching' }, { role: 'Student', product: 'coaching' }] });
    await fixture.componentInstance.removeRole({ role: 'Student', product: 'coaching' });
    expect(toaster.confirm).not.toHaveBeenCalled();
    expect(identity.removeRole).toHaveBeenCalledWith('user-1', 'Student', 'coaching');
  });

  it('explains that removing the last role closes the access and only Düzenle reopens it', async () => {
    const fixture = create();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Bir servisteki son rol kaldırılınca o servise erişim de kapanır');
    expect(text).toContain('Kullanıcı Düzenle ekranından açabilirsiniz');
  });

  it('does not treat a missing service as coaching', async () => {
    const fixture = create({ ...user, roles: ['Teacher'], productRoles: undefined });
    const component = fixture.componentInstance;
    await component.removeRole({ role: 'Teacher', product: null });
    expect(identity.removeRole).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Servis belirtilmemiş');
  });

  it('uses explicit global removal for SystemAdmin and excludes it from product assignment', async () => {
    const fixture = create({ ...user, roles: ['SystemAdmin'], productRoles: [{ role: 'SystemAdmin', product: null }] });
    const component = fixture.componentInstance;
    expect(fixture.nativeElement.textContent).toContain('SystemAdmin · Tüm servisler');
    expect(component.serviceRoles()).not.toContain('SystemAdmin');
    component.selectedProduct.set('speed-reading');
    component.assignRole('SystemAdmin');
    expect(identity.assignRole).not.toHaveBeenCalled();
    await component.removeRole({ role: 'SystemAdmin', product: null });
    expect(identity.removeRole).toHaveBeenCalledWith('user-1', 'SystemAdmin', null);
  });

  it('requires a successful reload after a refresh error before further changes', async () => {
    identity.getUserById.mockReturnValueOnce(throwError(() => ({ status: 500 })));
    const fixture = create();
    const component = fixture.componentInstance;
    await component.removeRole(user.productRoles![1]);
    expect(component.refreshFailed()).toBe(true);
    await component.removeRole(user.productRoles![0]);
    expect(identity.removeRole).toHaveBeenCalledTimes(1);
    component.refreshUser();
    expect(component.refreshFailed()).toBe(false);
    expect(component.user()?.productRoles).toEqual([{ role: 'Teacher', product: 'coaching' }]);
  });

  it('keeps roles intact when removal fails', async () => {
    identity.removeRole.mockReturnValue(throwError(() => ({ status: 403 })));
    const fixture = create();
    await fixture.componentInstance.removeRole(user.productRoles![1]);
    expect(fixture.componentInstance.user()?.productRoles).toEqual(user.productRoles);
    expect(toaster.error).toHaveBeenCalled();
    expect(fixture.componentInstance.actionLoading()).toBeNull();
  });

  it('blocks additional mutations while the refreshed profile is pending', async () => {
    const refresh = new Subject<UserDto>();
    identity.getUserById.mockReturnValue(refresh);
    const fixture = create();
    await fixture.componentInstance.removeRole(user.productRoles![1]);
    fixture.componentInstance.selectedProduct.set('coaching');
    fixture.componentInstance.assignRole('Student');
    expect(identity.assignRole).not.toHaveBeenCalled();
    refresh.next({ ...user, roles: ['Teacher'], productRoles: [user.productRoles![0]] });
    expect(fixture.componentInstance.actionLoading()).toBeNull();
  });
});
