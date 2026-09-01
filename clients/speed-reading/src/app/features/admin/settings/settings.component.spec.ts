import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { SettingsComponent } from './settings.component';
import { AdminConfigurationService } from '../../../core/services/admin-configuration.service';
import { AuthService } from '../../../core/services/auth.service';
import { UsersService } from '../../../core/services/users.service';
import { ToasterService } from '../../../core/services/toaster.service';

describe('SettingsComponent MFA enrollment', () => {
  let component: SettingsComponent;
  let auth: jasmine.SpyObj<AuthService>;
  let users: jasmine.SpyObj<UsersService>;

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>('AuthService', [
      'startAuthenticatedMfaSetup',
      'enableMfa'
    ]);
    users = jasmine.createSpyObj<UsersService>('UsersService', ['getMyProfile']);

    TestBed.configureTestingModule({
      imports: [SettingsComponent],
      providers: [
        {
          provide: AdminConfigurationService,
          useValue: {
            getAll: () => of([]),
            refreshCache: () => of({ message: 'ok' }),
            update: () => of(void 0)
          }
        },
        { provide: AuthService, useValue: auth },
        { provide: UsersService, useValue: users },
        {
          provide: ToasterService,
          useValue: jasmine.createSpyObj<ToasterService>('ToasterService', [
            'success', 'error', 'warning', 'info'
          ])
        }
      ]
    });

    users.getMyProfile.and.returnValue(of({
      id: 'admin-1',
      email: 'admin@example.com',
      firstName: 'System',
      lastName: 'Admin',
      roles: ['SystemAdmin'],
      isActive: true,
      emailConfirmed: true,
      mfaEnabled: false
    }));
    component = TestBed.createComponent(SettingsComponent).componentInstance;
  });

  it('loads the current admin MFA status independently from system configurations', () => {
    component.ngOnInit();

    expect(users.getMyProfile).toHaveBeenCalled();
    expect(component.mfaEnabled).toBeFalse();
  });

  it('starts MFA setup only after a current password is supplied', async () => {
    component.mfaCurrentPassword = 'CurrentPassword1!';
    auth.startAuthenticatedMfaSetup.and.resolveTo({
      secret: 'BASE32SECRET',
      otpAuthUri: 'otpauth://totp/EduPlatform:admin@example.com',
      setupToken: 'setup-token',
      challengeToken: 'challenge-token'
    });

    await component.startMfaSetup();

    expect(auth.startAuthenticatedMfaSetup).toHaveBeenCalledWith('CurrentPassword1!');
    expect(component.mfaSetup?.challengeToken).toBe('challenge-token');
    expect(component.mfaCurrentPassword).toBe('');
  });

  it('enables MFA with a six-digit code and keeps recovery codes visible', async () => {
    component.mfaSetup = {
      secret: 'BASE32SECRET',
      otpAuthUri: 'otpauth://totp/EduPlatform:admin@example.com',
      setupToken: 'setup-token',
      challengeToken: 'challenge-token'
    };
    component.mfaCode = '123456';
    auth.enableMfa.and.resolveTo(['RECOVERY-ONE']);

    await component.enableMfa();

    expect(auth.enableMfa).toHaveBeenCalledWith('challenge-token', 'setup-token', '123456');
    expect(component.mfaEnabled).toBeTrue();
    expect(component.mfaRecoveryCodes).toEqual(['RECOVERY-ONE']);
    expect(component.mfaSetup).toBeNull();
    expect(component.mfaCode).toBe('');
  });

  it('does not enable MFA with an invalid code', async () => {
    component.mfaSetup = {
      secret: 'BASE32SECRET',
      otpAuthUri: 'otpauth://totp/EduPlatform:admin@example.com',
      setupToken: 'setup-token',
      challengeToken: 'challenge-token'
    };
    component.mfaCode = '12345';

    await component.enableMfa();

    expect(auth.enableMfa).not.toHaveBeenCalled();
    expect(component.mfaEnabled).toBeFalse();
  });

  it('reports profile loading failures without blocking system settings', () => {
    users.getMyProfile.and.returnValue(throwError(() => new Error('profile unavailable')));

    component.ngOnInit();

    expect(component.mfaEnabled).toBeFalse();
  });
});
