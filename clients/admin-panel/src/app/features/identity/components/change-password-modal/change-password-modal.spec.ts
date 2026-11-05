import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { IdentityService, UserDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { ChangePasswordModalComponent } from './change-password-modal';

const user = { userId: '7c0b1a52-1d7a-4c39-9d43-4a40c41d1f0e', fullName: 'Ayşe Yılmaz' } as UserDto;

function setup(response: () => unknown) {
  const changePassword = vi.fn(() => response());
  const toaster = { success: vi.fn(), error: vi.fn() };
  TestBed.configureTestingModule({
    imports: [ChangePasswordModalComponent],
    providers: [
      { provide: IdentityService, useValue: { changePassword } },
      { provide: ToasterService, useValue: toaster },
    ],
  });
  const fixture = TestBed.createComponent(ChangePasswordModalComponent);
  fixture.componentRef.setInput('userData', signal(user));
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, changePassword, toaster };
}

function fill(component: ChangePasswordModalComponent, password: string) {
  component.form.setValue({ password, confirmPassword: password });
}

describe('ChangePasswordModalComponent', () => {
  it('shows the server password policy before anything is typed', () => {
    const { fixture } = setup(() => of({}));
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('En az 8 karakter; büyük harf, küçük harf, rakam ve özel karakter');
  });

  it('accepts exactly the passwords the server accepts', () => {
    const { component } = setup(() => of({}));

    for (const valid of ['Sifre-2026', 'Sifre_2026', 'Yeni+Sifre9', 'Sifre 2026']) {
      fill(component, valid);
      expect(component.form.valid).toBe(true);
    }
    for (const invalid of ['Abcde1', 'Sifre2026', 'sifre-2026', 'Sifre-Yeni']) {
      fill(component, invalid);
      expect(component.form.valid).toBe(false);
    }
  });

  it('sends the selected user id and closes on success', () => {
    const { component, changePassword, toaster } = setup(() => of({}));
    const closed = vi.fn();
    component.close.subscribe(closed);
    fill(component, 'Sifre-2026');

    component.save();

    expect(changePassword).toHaveBeenCalledWith(user.userId, 'Sifre-2026');
    expect(toaster.success).toHaveBeenCalled();
    expect(closed).toHaveBeenCalledWith(true);
  });

  it('shows the Turkish validation message from the server and recovers', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const { component, fixture, toaster } = setup(() => throwError(() => new HttpErrorResponse({
      status: 400,
      url: `/api/users/${user.userId}/change-password`,
      error: {
        title: 'Validation Error',
        detail: 'One or more validation errors occurred.',
        errors: { NewPassword: ['Şifre en az bir özel karakter içermelidir.'] },
      },
    })));
    fill(component, 'Sifre-2026');

    component.save();
    fixture.detectChanges();

    expect(component.error()).toBe('Şifre en az bir özel karakter içermelidir.');
    expect(component.loading()).toBe(false);
    expect(toaster.error).toHaveBeenCalledWith('Şifre en az bir özel karakter içermelidir.');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Şifre en az bir özel karakter içermelidir.');
    for (const call of consoleError.mock.calls) {
      expect(JSON.stringify(call)).not.toContain('Sifre-2026');
    }
    consoleError.mockRestore();
  });

  it('tells a missing permission apart from missing MFA', () => {
    const { component } = setup(() => throwError(() => new HttpErrorResponse({
      status: 403, error: { code: 'Auth.Forbidden', detail: 'Bu işlem için yetkiniz yok.' },
    })));
    fill(component, 'Sifre-2026');

    component.save();

    expect(component.error()).toBe('Bu işlem için yetkiniz yok.');
  });

  it('asks for MFA when the server says MFA is missing', () => {
    const { component } = setup(() => throwError(() => new HttpErrorResponse({
      status: 403, error: { code: 'Auth.MfaRequired', detail: 'Bu işlem için MFA doğrulaması gerekiyor.' },
    })));
    fill(component, 'Sifre-2026');

    component.save();

    expect(component.error()).toContain('MFA doğrulaması gerekiyor');
  });
});
