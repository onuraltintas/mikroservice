import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthService } from '../../../core/services/auth.service';
import { GoogleIdentityService } from '../../../core/services/google-identity.service';
import { SubscriptionService } from '../../../core/services/subscription.service';
import { ToasterService } from '../../../core/services/toaster.service';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['login']);
    authService.login.and.returnValue(of({
      id: 'student-id',
      token: '',
      refreshToken: '',
      email: 'student@example.com',
      firstName: 'Test',
      lastName: 'Student',
      roles: ['Student']
    }));

    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
        {
          provide: ActivatedRoute,
          useValue: {
            queryParams: of({}),
            snapshot: { queryParamMap: { get: () => null } }
          }
        },
        {
          provide: GoogleIdentityService,
          useValue: {
            renderButton: jasmine.createSpy('renderButton').and.returnValue(Promise.resolve()),
            clearCallback: jasmine.createSpy('clearCallback')
          }
        },
        {
          provide: SubscriptionService,
          useValue: { getMyModules: jasmine.createSpy('getMyModules').and.returnValue(of({ hasSpeedReading: true })) }
        },
        {
          provide: ToasterService,
          useValue: {
            error: jasmine.createSpy('error'),
            success: jasmine.createSpy('success')
          }
        }
      ]
    });

    component = TestBed.createComponent(LoginComponent).componentInstance;
    spyOn<any>(component, 'handleAuthenticatedResponse').and.stub();
  });

  it('sends the selected remember-me preference to the authentication API', () => {
    component.loginForm.setValue({
      email: 'student@example.com',
      password: 'Password1!',
      rememberMe: true
    });

    component.onSubmit();

    expect(authService.login).toHaveBeenCalledWith({
      email: 'student@example.com',
      password: 'Password1!',
      rememberMe: true
    });
  });

  it('sends a session-only preference when remember-me is cleared', () => {
    component.loginForm.setValue({
      email: 'student@example.com',
      password: 'Password1!',
      rememberMe: false
    });

    component.onSubmit();

    expect(authService.login).toHaveBeenCalledWith({
      email: 'student@example.com',
      password: 'Password1!',
      rememberMe: false
    });
  });
});
