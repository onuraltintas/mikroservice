import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { RegisterComponent } from './register.component';
import { AuthService } from '../../../core/services/auth.service';
import { GoogleIdentityService } from '../../../core/services/google-identity.service';
import { PlatformLegalPagesService } from '../../../core/services/platform-legal-pages.service';
import { of } from 'rxjs';

describe('RegisterComponent', () => {
  it('blocks both manual and Google requests until legal acceptances are complete', () => {
    const auth = jasmine.createSpyObj('AuthService', ['googleAuth', 'register']);
    auth.googleAuth.and.returnValue(of({ roles: [] }));
    TestBed.configureTestingModule({ providers: [
      { provide: AuthService, useValue: auth },
      { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
      { provide: GoogleIdentityService, useValue: {} }
    ] });
    const component = TestBed.runInInjectionContext(() => new RegisterComponent());
    component.onSubmit();
    (component as any).handleGoogleResponse({ credential: 'google-token' });
    expect(auth.register).not.toHaveBeenCalled();
    expect(auth.googleAuth).not.toHaveBeenCalled();
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
});
