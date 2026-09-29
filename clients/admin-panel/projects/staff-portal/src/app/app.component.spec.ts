import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { StaffAuthService } from './auth/staff-auth.service';
import { StaffProductAccess } from './auth/staff-auth.models';
import { AppComponent } from './app.component';

describe('Staff portal MFA flow', () => {
  it('starts the enrollment setup when Identity requires first-time MFA enrollment', async () => {
    const auth = {
      login: vi.fn().mockResolvedValue({
        requiresMfa: true,
        mfaEnrollmentRequired: true,
        mfaChallengeToken: 'mfa-challenge'
      }),
      startMfaSetup: vi.fn().mockResolvedValue({
        secret: 'TOTP-SECRET',
        otpAuthUri: 'otpauth://example',
        setupToken: 'setup-token'
      })
    };
    const component = createComponent(auth);
    component.email = 'manager@example.test';
    component.password = 'password';

    await component.submitLogin();

    expect(auth.startMfaSetup).toHaveBeenCalledWith('mfa-challenge');
    expect((component as unknown as { mfaStage: string }).mfaStage).toBe('setup');
    expect((component as unknown as { mfaSecret: string }).mfaSecret).toBe('TOTP-SECRET');
  });

  it('shows one-time recovery codes after MFA enrollment succeeds', async () => {
    const auth = {
      enableMfa: vi.fn().mockResolvedValue(['ONE-TIME-01']),
      verifyMfa: vi.fn().mockResolvedValue(undefined)
    };
    const component = createComponent(auth);
    const state = component as unknown as {
      mfaStage: string;
      mfaChallengeToken: string;
      mfaSetupToken: string;
      mfaCode: string;
      recoveryCodes: string[];
    };
    state.mfaStage = 'setup';
    state.mfaChallengeToken = 'mfa-challenge';
    state.mfaSetupToken = 'setup-token';
    state.mfaCode = '123456';

    await component.submitMfa();

    expect(auth.enableMfa).toHaveBeenCalledWith('coaching', 'mfa-challenge', 'setup-token', '123456');
    expect(state.mfaStage).toBe('recovery');
    expect(state.recoveryCodes).toEqual(['ONE-TIME-01']);
  });

  it('allows an existing MFA user to authenticate with a recovery code', async () => {
    const auth = {
      verifyMfa: vi.fn().mockResolvedValue(undefined)
    };
    const component = createComponent(auth);
    const state = component as unknown as {
      mfaChallengeToken: string;
      mfaRecoveryCode: string;
      submitRecoveryCode: () => Promise<void>;
    };
    state.mfaChallengeToken = 'mfa-challenge';
    state.mfaRecoveryCode = 'RECOVERY-01';

    await state.submitRecoveryCode();

    expect(auth.verifyMfa).toHaveBeenCalledWith('coaching', 'mfa-challenge', null, 'RECOVERY-01');
  });
});

describe('Staff portal workspace role selection', () => {
  it('defaults dual-role coaching accounts to the institution workspace without rendering both panels', () => {
    const component = createWorkspaceComponent();

    expect(component.activeWorkspaceRole).toBe('institution');
    expect(component.canSwitchWorkspaceRole).toBe(true);
    expect(component.isInstitutionWorkspaceSelected).toBe(true);
    expect(component.isTeacherWorkspaceSelected).toBe(false);
  });

  it('switches to the teacher workspace when the user selects it', () => {
    const component = createWorkspaceComponent();

    component.selectWorkspaceRole('teacher');

    expect(component.activeWorkspaceRole).toBe('teacher');
    expect(component.isTeacherWorkspaceSelected).toBe(true);
    expect(component.isInstitutionWorkspaceSelected).toBe(false);
  });

  it('uses only roles granted for the currently active product', () => {
    const activeProduct = signal<'coaching' | 'speed-reading'>('coaching');
    const component = createComponent({
      products: signal<StaffProductAccess[]>([
        { product: 'coaching', roles: ['Teacher', 'InstitutionAdmin'] },
        { product: 'speed-reading', roles: ['Teacher'] }
      ]),
      activeProduct
    });

    component.selectWorkspaceRole('teacher');
    activeProduct.set('speed-reading');
    expect(component.activeWorkspaceRole).toBe('teacher');
    expect(component.isSpeedReadingTeacherWorkspaceSelected).toBe(true);
    expect(component.canSwitchWorkspaceRole).toBe(false);
    component.selectWorkspaceRole('institution');
    expect(component.activeWorkspaceRole).toBe('teacher');

    activeProduct.set('coaching');
    expect(component.activeWorkspaceRole).toBe('teacher');
  });

  it('selects the Speed Reading institution workspace for that product role only', () => {
    const component = createComponent({
      products: signal<StaffProductAccess[]>([
        { product: 'coaching', roles: ['Teacher'] },
        { product: 'speed-reading', roles: ['Teacher', 'InstitutionAdmin'] }
      ]),
      activeProduct: signal('speed-reading')
    });

    expect(component.activeWorkspaceRole).toBe('institution');
    expect(component.isSpeedReadingInstitutionWorkspaceSelected).toBe(true);
    component.selectWorkspaceRole('teacher');
    expect(component.isSpeedReadingTeacherWorkspaceSelected).toBe(true);
    expect(component.isSpeedReadingInstitutionWorkspaceSelected).toBe(false);
  });
});

function createComponent(auth: Partial<StaffAuthService>): AppComponent {
  TestBed.configureTestingModule({
    providers: [{ provide: StaffAuthService, useValue: auth }]
  });
  return TestBed.runInInjectionContext(() => new AppComponent());
}

function createWorkspaceComponent(): AppComponent {
  return createComponent({
    products: signal<StaffProductAccess[]>([{ product: 'coaching', roles: ['Teacher', 'InstitutionAdmin'] }]),
    activeProduct: signal('coaching')
  });
}
