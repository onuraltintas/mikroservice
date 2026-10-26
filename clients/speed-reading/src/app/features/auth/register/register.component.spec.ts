import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { RegisterComponent } from './register.component';
import { AuthService } from '../../../core/services/auth.service';
import { GoogleIdentityService } from '../../../core/services/google-identity.service';
import { PlatformLegalPagesService } from '../../../core/services/platform-legal-pages.service';
import { of, throwError } from 'rxjs';

describe('RegisterComponent', () => {
  it('keeps manual registration gated but lets Google registration proceed without legal acceptances', () => {
    const auth = jasmine.createSpyObj('AuthService', ['googleAuth', 'register']);
    auth.googleAuth.and.returnValue(of({ roles: [] }));
    const router = jasmine.createSpyObj('Router', ['navigate']);
    TestBed.configureTestingModule({ providers: [
      { provide: AuthService, useValue: auth },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
      { provide: GoogleIdentityService, useValue: {} }
    ] });
    const component = TestBed.runInInjectionContext(() => new RegisterComponent());
    component.registerForm.setValue({
      firstName: 'Test',
      lastName: 'Student',
      email: 'student@example.com',
      password: 'Password1!',
      confirmPassword: 'Password1!'
    });
    component.onSubmit();
    (component as any).handleGoogleResponse({ credential: 'google-token' });
    expect(auth.register).not.toHaveBeenCalled();
    expect(auth.googleAuth).toHaveBeenCalledOnceWith('google-token');
  });
  it('requires all current central legal document acceptances before registration', () => {
    TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [
        { provide: AuthService, useValue: {} },
        { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) },
        { provide: ActivatedRoute, useValue: {} },
        { provide: PlatformLegalPagesService, useValue: { getPage: (slug: string) => of({
          slug,
          title: slug,
          content: 'Published legal text',
          isPublished: true,
          version: 2,
          createdAt: new Date().toISOString()
        }) } },
        { provide: GoogleIdentityService, useValue: {
          renderButton: jasmine.createSpy('renderButton').and.returnValue(Promise.resolve()),
          clearCallback: jasmine.createSpy('clearCallback')
        } }
      ]
    });
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.hasRequiredLegalAcceptances).toBe(false);
    fixture.componentInstance.legalReady = true;
    fixture.componentInstance.legalAcceptances = [
      { slug: 'privacy', version: 2 },
      { slug: 'kvkk', version: 2 },
      { slug: 'speed-reading-terms', version: 2 }
    ];
    expect(fixture.componentInstance.hasRequiredLegalAcceptances).toBe(true);
    fixture.componentInstance.legalAcceptances.pop();
    expect(fixture.componentInstance.hasRequiredLegalAcceptances).toBe(false);
  });

  it('clears the spinner and shows a useful message when registration times out', () => {
    const auth = jasmine.createSpyObj('AuthService', ['register', 'googleAuth']);
    auth.register.and.returnValue(throwError(() => ({ name: 'TimeoutError', message: 'Timeout has occurred' })));
    const router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    TestBed.configureTestingModule({ providers: [
      { provide: AuthService, useValue: auth },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
      { provide: GoogleIdentityService, useValue: {} }
    ] });
    const component = TestBed.runInInjectionContext(() => new RegisterComponent());
    component.registerForm.setValue({
      firstName: 'Ada',
      lastName: 'Yılmaz',
      email: 'ada@example.com',
      password: 'Password1!',
      confirmPassword: 'Password1!'
    });
    component.legalReady = true;
    component.legalAcceptances = ['privacy', 'kvkk', 'speed-reading-terms'].map(slug => ({ slug, version: 1 }));

    component.onSubmit();

    expect(component.loading).toBeFalse();
    expect(component.error).toContain('zaman aşımına uğradı');
  });

  it('clears the spinner after a successful registration navigation starts', () => {
    const auth = jasmine.createSpyObj('AuthService', ['register', 'googleAuth']);
    auth.register.and.returnValue(of({ userId: 'student-id' }));
    const router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    TestBed.configureTestingModule({ providers: [
      { provide: AuthService, useValue: auth },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
      { provide: GoogleIdentityService, useValue: {} }
    ] });
    const component = TestBed.runInInjectionContext(() => new RegisterComponent());
    component.registerForm.setValue({
      firstName: 'Ada',
      lastName: 'Yılmaz',
      email: 'ada@example.com',
      password: 'Password1!',
      confirmPassword: 'Password1!'
    });
    component.legalReady = true;
    component.legalAcceptances = ['privacy', 'kvkk', 'speed-reading-terms'].map(slug => ({ slug, version: 1 }));

    component.onSubmit();

    expect(router.navigate).toHaveBeenCalled();
    expect(component.loading).toBeFalse();
  });
});
