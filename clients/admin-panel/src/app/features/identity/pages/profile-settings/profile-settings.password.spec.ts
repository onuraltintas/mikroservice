import { HttpErrorResponse } from '@angular/common/http';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../../core/auth/auth.service';
import { IdentityService } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { ProfileSettingsComponent } from './profile-settings';

function setup(changeMyPassword: () => unknown) {
  const toaster = { success: vi.fn(), error: vi.fn() };
  TestBed.configureTestingModule({
    imports: [ProfileSettingsComponent],
    providers: [
      { provide: PLATFORM_ID, useValue: 'browser' },
      {
        provide: IdentityService, useValue: {
          getMyProfile: () => of({ userId: 'u1', email: 'a@example.test', fullName: 'A B', firstName: 'A', lastName: 'B', roles: [], permissions: [], mfaEnabled: true }),
          changeMyPassword: vi.fn(changeMyPassword),
        },
      },
      { provide: AuthService, useValue: {} },
      { provide: ToasterService, useValue: toaster },
      { provide: SocialAuthService, useValue: { signIn: vi.fn() } },
      provideRouter([]),
    ],
  });
  const fixture = TestBed.createComponent(ProfileSettingsComponent);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, toaster };
}

describe('ProfileSettingsComponent password change', () => {
  it('shows the password policy up front and applies the server rule', () => {
    const { fixture, component } = setup(() => of({}));

    expect((fixture.nativeElement as HTMLElement).textContent)
      .toContain('En az 8 karakter; büyük harf, küçük harf, rakam ve özel karakter');
    component.passwordForm.setValue({ currentPassword: 'eski', newPassword: 'Abcde1', confirmPassword: 'Abcde1' });
    expect(component.passwordForm.valid).toBe(false);
    component.passwordForm.setValue({ currentPassword: 'eski', newPassword: 'Sifre-2026', confirmPassword: 'Sifre-2026' });
    expect(component.passwordForm.valid).toBe(true);
  });

  it('shows the Turkish server message next to the form and recovers', () => {
    const { fixture, component, toaster } = setup(() => throwError(() => new HttpErrorResponse({
      status: 400, error: { error: { code: 'Auth.InvalidCredentials', description: 'Mevcut şifre hatalı.' } },
    })));
    component.passwordForm.setValue({ currentPassword: 'eski', newPassword: 'Sifre-2026', confirmPassword: 'Sifre-2026' });

    component.updatePassword();
    fixture.detectChanges();

    expect(component.savingPassword()).toBe(false);
    expect(component.passwordError()).toBe('Mevcut şifre hatalı.');
    expect(toaster.error).toHaveBeenCalledWith('Mevcut şifre hatalı.');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Mevcut şifre hatalı.');
  });

  it('joins validation messages from a 400 ProblemDetails', () => {
    const { component } = setup(() => throwError(() => new HttpErrorResponse({
      status: 400,
      error: { detail: 'One or more validation errors occurred.', errors: { NewPassword: ['Şifre 8-128 karakter arasında olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.'] } },
    })));
    component.passwordForm.setValue({ currentPassword: 'eski', newPassword: 'Sifre-2026', confirmPassword: 'Sifre-2026' });

    component.updatePassword();

    expect(component.passwordError()).toContain('Şifre 8-128 karakter');
  });
});
