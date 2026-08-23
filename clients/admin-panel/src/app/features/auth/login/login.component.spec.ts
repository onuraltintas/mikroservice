import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { Subject } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { LoginComponent } from './login.component';

describe('LoginComponent MFA flow', () => {
  it('starts MFA enrollment only when the backend explicitly requires it', async () => {
    const authService = {
      loginWithPassword: vi.fn().mockResolvedValue({
        authenticated: false,
        requiresMfa: true,
        mfaEnrollmentRequired: true,
        mfaChallengeToken: 'challenge'
      }),
      startMfaSetup: vi.fn().mockResolvedValue({
        secret: 'ABCDEFGHIJKLMNOP234567',
        otpAuthUri: 'otpauth://totp/test',
        setupToken: 'setup'
      })
    };
    const component = createComponent(authService);
    component.email = 'admin@example.test';
    component.password = 'password';

    await component.onSubmit(new Event('submit'));

    expect(component.mfaStage()).toBe('setup');
    expect(component.mfaSecret()).toBe('ABCDEFGHIJKLMNOP234567');
    expect(authService.startMfaSetup).toHaveBeenCalledWith('challenge');
  });

  it('shows one-time recovery codes after successful enrollment', async () => {
    const authService = {
      enableMfa: vi.fn().mockResolvedValue(['CODE01-CODE001', 'CODE02-CODE002'])
    };
    const component = createComponent(authService);
    component.mfaChallengeToken = 'challenge';
    component.mfaSetupToken = 'setup';
    component.mfaStage.set('setup');
    component.mfaCode = '123456';

    await component.submitMfa();

    expect(component.mfaStage()).toBe('recovery');
    expect(component.recoveryCodes()).toEqual(['CODE01-CODE001', 'CODE02-CODE002']);
  });

  function createComponent(authService: Record<string, unknown>): LoginComponent {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true) } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() } },
        { provide: SocialAuthService, useValue: { authState: new Subject() } }
      ]
    });
    return TestBed.runInInjectionContext(() => new LoginComponent());
  }
});
