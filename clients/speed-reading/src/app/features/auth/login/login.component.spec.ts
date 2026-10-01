import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthService } from '../../../core/services/auth.service';
import { GoogleIdentityService } from '../../../core/services/google-identity.service';
import { SubscriptionService } from '../../../core/services/subscription.service';
import { ToasterService } from '../../../core/services/toaster.service';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;
  let route: any;

  beforeEach(() => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['login', 'googleAuth']);
    router = jasmine.createSpyObj<Router>('Router', ['navigate', 'navigateByUrl']);
    route = {
      queryParams: of({}),
      snapshot: { queryParamMap: { get: jasmine.createSpy('get').and.returnValue(null) } }
    };
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
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: route },
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

  it('opens legal acceptance on the same screen without repeating Google authentication', () => {
    authService.googleAuth.and.returnValue(of({
      requiresLegalAcceptance: true, registrationToken: 'pending-ticket'
    } as any));
    (component as any).handleGoogleResponse({ credential: 'secret-google-token' });
    expect(router.navigate).not.toHaveBeenCalled();
    expect((component as any).googleRegistrationToken).toBe('pending-ticket');
    expect((component as any).handleAuthenticatedResponse).not.toHaveBeenCalled();
    expect(component.loading).toBe(false);
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

  it('shows the verification warning when the API error is exposed at the top level', () => {
    authService.login.and.returnValue(throwError(() => ({
      message: 'Please verify your email before signing in.'
    })));
    component.loginForm.setValue({
      email: 'student@example.com',
      password: 'Password1!',
      rememberMe: false
    });

    component.onSubmit();

    expect(component.showEmailVerificationWarning).toBeTrue();
    expect(component.unverifiedEmail).toBe('student@example.com');
  });

  it('shows an invalid password error only in the toast', () => {
    const toaster = TestBed.inject(ToasterService) as jasmine.SpyObj<ToasterService>;
    authService.login.and.returnValue(throwError(() => ({ error: { message: 'E-posta veya şifre hatalı.' } })));
    component.loginForm.setValue({ email: 'student@example.com', password: 'Wrong1!', rememberMe: false });

    component.onSubmit();

    expect(toaster.error).toHaveBeenCalledWith('E-posta veya şifre hatalı.', jasmine.any(Number));
    expect(component.error).toBe('');
  });

  it('keeps admin preview users on Master when a legacy admin return URL is present', () => {
    (component as any).handleAuthenticatedResponse.and.callThrough();
    route.snapshot.queryParamMap.get.and.returnValue('/admin');

    (component as any).handleAuthenticatedResponse({ roles: ['Admin'] });

    expect(router.navigate).toHaveBeenCalledWith(['/student/exercises']);
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });
});
