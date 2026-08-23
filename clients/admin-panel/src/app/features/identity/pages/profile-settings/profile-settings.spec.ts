import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ProfileSettingsComponent } from './profile-settings';
import { IdentityService } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { SocialAuthService } from '@abacritt/angularx-social-login';

describe('ProfileSettingsComponent MFA enrollment', () => {
  let fixture: ComponentFixture<ProfileSettingsComponent>;
  let component: ProfileSettingsComponent;
  const identity = {
    getMyProfile: vi.fn(() => of({
      userId: 'admin-1',
      email: 'admin@example.test',
      fullName: 'Admin User',
      firstName: 'Admin',
      lastName: 'User',
      role: 'SystemAdmin',
      isActive: true,
      emailConfirmed: true,
      mfaEnabled: false,
      roles: ['SystemAdmin'],
      permissions: []
    }))
  };
  const auth = {
    startAuthenticatedMfaSetup: vi.fn(async () => ({
      secret: 'SECRET',
      otpAuthUri: 'otpauth://test',
      setupToken: 'setup',
      challengeToken: 'challenge'
    })),
    enableMfa: vi.fn(async () => ['RECOVERY-ONE'])
  };
  const toaster = { success: vi.fn(), error: vi.fn() };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProfileSettingsComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: IdentityService, useValue: identity },
        { provide: AuthService, useValue: auth },
        { provide: ToasterService, useValue: toaster },
        { provide: SocialAuthService, useValue: { signIn: vi.fn() } },
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProfileSettingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('keeps recovery codes visible after enabling MFA', async () => {
    component.mfaCurrentPassword = 'current-password';
    await component.startMfaSetup();
    component.mfaCode = '123456';
    await component.enableMfa();
    fixture.detectChanges();

    expect(auth.startAuthenticatedMfaSetup).toHaveBeenCalledWith('current-password');
    expect(auth.enableMfa).toHaveBeenCalledWith('challenge', 'setup', '123456');
    expect(fixture.nativeElement.textContent).toContain('RECOVERY-ONE');
  });
});
