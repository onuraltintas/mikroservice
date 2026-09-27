import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { RegisterComponent } from './register.component';
import { AuthService } from '../../../core/services/auth.service';
import { GoogleIdentityService } from '../../../core/services/google-identity.service';

describe('RegisterComponent', () => {
  it('explains which required consents are missing before registration', () => {
    TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [
        { provide: AuthService, useValue: {} },
        { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) },
        { provide: ActivatedRoute, useValue: {} },
        { provide: GoogleIdentityService, useValue: {
          renderButton: jasmine.createSpy('renderButton').and.returnValue(Promise.resolve()),
          clearCallback: jasmine.createSpy('clearCallback')
        } }
      ]
    });
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Kayıt için Kullanım Koşulları ve KVKK Metni onayları gereklidir.');

    fixture.componentInstance.registerForm.patchValue({ acceptTerms: true });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Kayıt için KVKK Metni onayı gereklidir.');

    fixture.componentInstance.registerForm.patchValue({ acceptKVKK: true });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('onayı gereklidir.');
  });
});
