import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { RegisterTeacherComponent } from './register-teacher.component';
import { AuthService } from '../../../core/services/auth.service';
import { GoogleIdentityService } from '../../../core/services/google-identity.service';
import { ToasterService } from '../../../core/services/toaster.service';

describe('Teacher registration legal consent', () => {
  it('blocks Google and manual registration without current legal acceptances', () => {
    const auth = jasmine.createSpyObj('AuthService', ['googleAuth', 'registerTeacher']);
    auth.googleAuth.and.returnValue(of({ roles: [] }));
    TestBed.configureTestingModule({ providers: [
      { provide: AuthService, useValue: auth },
      { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
      { provide: GoogleIdentityService, useValue: {} },
      { provide: ToasterService, useValue: {} }
    ] });
    const component = TestBed.runInInjectionContext(() => new RegisterTeacherComponent());
    component.onSubmit();
    (component as any).handleGoogleResponse({ credential: 'google-token' });
    expect(auth.registerTeacher).not.toHaveBeenCalled();
    expect(auth.googleAuth).not.toHaveBeenCalled();
  });
});
