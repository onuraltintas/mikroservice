import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { IdentityService } from '../../../core/services/identity.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { ResetPasswordComponent } from './reset-password.component';

function setup(response: () => unknown) {
  const resetPassword = vi.fn(response);
  TestBed.configureTestingModule({
    providers: [
      { provide: IdentityService, useValue: { resetPassword } },
      { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
      { provide: ActivatedRoute, useValue: { queryParams: of({ token: 't', email: 'a@example.test' }) } },
      { provide: Router, useValue: { navigate: vi.fn() } },
    ],
  });
  const component = TestBed.runInInjectionContext(() => new ResetPasswordComponent());
  component.ngOnInit();
  return { component, resetPassword };
}

const submit = (component: ResetPasswordComponent, password: string) => {
  component.newPassword = password;
  component.confirmPassword = password;
  component.onSubmit(new Event('submit'));
};

describe('ResetPasswordComponent', () => {
  it('uses the server policy: any non-alphanumeric character, 8 to 128 characters', () => {
    const { component } = setup(() => of({}));
    for (const [password, valid] of [['Sifre 2026', true], ['Sifre-2026', true], ['Sifre2026', false], ['Sifre-2026' + 'x'.repeat(119), false]] as const) {
      component.newPassword = password;
      expect(component.passwordStrong).toBe(valid);
    }
  });

  it('shows the Identity reset error description', () => {
    const { component } = setup(() => throwError(() => new HttpErrorResponse({
      status: 400,
      error: { code: 'ResetPassword.InvalidRequest', description: 'Sıfırlama bağlantısı geçersiz veya süresi dolmuş olabilir. Lütfen yeni bir bağlantı isteyin.' },
    })));

    submit(component, 'Sifre-2026');

    expect(component.errorMessage()).toBe('Sıfırlama bağlantısı geçersiz veya süresi dolmuş olabilir. Lütfen yeni bir bağlantı isteyin.');
    expect(component.isLoading()).toBe(false);
  });

  it('explains rate limits and network failures', () => {
    const limited = setup(() => throwError(() => new HttpErrorResponse({ status: 429, error: null })));
    submit(limited.component, 'Sifre-2026');
    expect(limited.component.errorMessage()).toContain('Çok fazla deneme');

    TestBed.resetTestingModule();
    const offline = setup(() => throwError(() => new HttpErrorResponse({ status: 0, error: null })));
    submit(offline.component, 'Sifre-2026');
    expect(offline.component.errorMessage()).toContain('Sunucuya ulaşılamadı');
    expect(offline.component.isLoading()).toBe(false);
  });
});
