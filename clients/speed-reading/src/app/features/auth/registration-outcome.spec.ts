import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, of } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { GoogleIdentityService } from '../../core/services/google-identity.service';
import { LocationsService } from '../../core/services/locations.service';
import { ToasterService } from '../../core/services/toaster.service';
import { RegisterSchoolComponent } from './register-school/register-school.component';
import { RegisterTeacherComponent } from './register-teacher/register-teacher.component';
import { RegisterComponent } from './register/register.component';
import { readVerificationEmailOutcome, registrationSuccessCopy } from './registration-outcome';
import { RegistrationVerifiedComponent } from './registration-verified.component';
import { provideRouter } from '@angular/router';

// Paket A #2 (05.11.2026): the success screen follows the server's verificationEmail outcome.
type Voice = 'sen' | 'siz';
const acceptances = ['privacy', 'kvkk', 'speed-reading-terms'].map(slug => ({ slug, version: 1 }));
const password = 'Sifre-2026';

interface Harness { submit(): void; successMessage(): string; verifiedMessage(): string; alreadyVerified(): boolean; setError(v: string): void; error(): string; }

const forms: Array<{ name: string; voice: Voice; method: string; create(): Harness }> = [
  {
    name: 'student registration', voice: 'sen', method: 'register',
    create: () => {
      const c = TestBed.runInInjectionContext(() => new RegisterComponent());
      c.registerForm.setValue({ firstName: 'Elif', lastName: 'Kaya', email: 'elif@example.test', password, confirmPassword: password });
      c.legalReady = true; c.legalAcceptances = acceptances;
      return { submit: () => c.onSubmit(), successMessage: () => c.successMessage, verifiedMessage: () => c.verifiedMessage, alreadyVerified: () => c.alreadyVerified,
        setError: v => c.error = v, error: () => c.error };
    },
  },
  {
    name: 'teacher registration', voice: 'siz', method: 'registerTeacher',
    create: () => {
      const c = TestBed.runInInjectionContext(() => new RegisterTeacherComponent());
      c.registerForm.setValue({ firstName: 'Ali', lastName: 'Demir', email: 'ali@example.test', password, confirmPassword: password });
      c.legalReady = true; c.legalAcceptances = acceptances;
      return { submit: () => c.onSubmit(), successMessage: () => c.successMessage, verifiedMessage: () => c.verifiedMessage, alreadyVerified: () => c.alreadyVerified,
        setError: v => c.error = v, error: () => c.error };
    },
  },
  {
    name: 'school registration', voice: 'siz', method: 'registerInstitution',
    create: () => {
      const c = TestBed.runInInjectionContext(() => new RegisterSchoolComponent());
      c.registerForm.setValue({ schoolName: 'Okul', contactEmail: 'okul@example.test', phoneNumber: '5551234567', address: '',
        provinceId: '34', districtId: '3401', firstName: 'Ali', lastName: 'Demir', adminEmail: 'ali@example.test', password, confirmPassword: password });
      c.legalReady = true; c.legalAcceptances = acceptances;
      return { submit: () => c.onSubmit(), successMessage: () => c.successMessage, verifiedMessage: () => c.verifiedMessage, alreadyVerified: () => c.alreadyVerified,
        setError: v => c.error = v, error: () => c.error };
    },
  },
];

function configure(method: string, response: unknown) {
  const auth = jasmine.createSpyObj('AuthService', [method, 'googleAuth']);
  auth[method].and.returnValue(of(response));
  const router = jasmine.createSpyObj('Router', ['navigate']);
  TestBed.configureTestingModule({ providers: [
    { provide: AuthService, useValue: auth },
    { provide: Router, useValue: router },
    { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } }, queryParams: of({}) } },
    { provide: GoogleIdentityService, useValue: {} },
    { provide: ToasterService, useValue: jasmine.createSpyObj('ToasterService', ['error', 'success', 'info']) },
    { provide: LocationsService, useValue: { getProvinces: () => new Subject(), getDistricts: () => new Subject() } },
  ] });
  return router as jasmine.SpyObj<Router>;
}

describe('registration verification e-mail outcome copy', () => {
  it('reads only an explicit notRequired; anything else (older API) means the e-mail was sent', () => {
    expect(readVerificationEmailOutcome({ userId: 'u', verificationEmail: 'notRequired' })).toBe('notRequired');
    expect(readVerificationEmailOutcome({ userId: 'u', verificationEmail: 'sent' })).toBe('sent');
    expect(readVerificationEmailOutcome({ userId: 'u' })).toBe('sent');
    expect(readVerificationEmailOutcome(null)).toBe('sent');
  });

  (['sen', 'siz'] as const).forEach(voice => {
    it(`never says "gönderildi" for an already verified address (${voice})`, () => {
      const verified = registrationSuccessCopy('notRequired', voice);
      expect(verified.message).not.toMatch(/gönder/i);
      expect(verified.message).toContain('zaten doğrulanmış');
      expect(verified.message).toContain(voice === 'sen' ? 'giriş yapabilirsin.' : 'giriş yapabilirsiniz.');
      const sent = registrationSuccessCopy('sent', voice);
      expect(sent.message).toMatch(/gönderdik/);
      expect(sent.message).not.toContain('zaten doğrulanmış');
    });
  });
});

forms.forEach(entry => {
  describe(`${entry.name} success screen`, () => {
    it('says the verification e-mail was sent and continues to login', fakeAsync(() => {
      const router = configure(entry.method, { userId: 'u', verificationEmail: 'sent' });
      const form = entry.create();
      form.submit();
      tick(5000);
      expect(form.alreadyVerified()).toBeFalse();
      expect(form.verifiedMessage()).toBe('');
      expect(form.successMessage()).toBe(registrationSuccessCopy('sent', entry.voice).message);
      expect(router.navigate).toHaveBeenCalled();
      expect(router.navigate.calls.mostRecent().args[0]).toEqual(['/auth/login']);
    }));

    it('says the address is already verified, offers sign in and does not claim an e-mail was sent', fakeAsync(() => {
      const router = configure(entry.method, { userId: 'u', verificationEmail: 'notRequired' });
      const form = entry.create();
      form.setError('Önceki hata');
      form.submit();
      tick(5000);
      expect(form.alreadyVerified()).toBeTrue();
      expect(form.verifiedMessage()).toBe(registrationSuccessCopy('notRequired', entry.voice).message);
      expect(form.verifiedMessage()).not.toMatch(/gönder/i);
      // Review #4/#5: shown once (in the panel), no stale success or error text next to it.
      expect(form.successMessage()).toBe('');
      expect(form.error()).toBe('');
      expect(router.navigate).not.toHaveBeenCalled();
    }));

    it('treats an older API response without the outcome as sent', fakeAsync(() => {
      configure(entry.method, { userId: 'u' });
      const form = entry.create();
      form.submit();
      tick(5000);
      expect(form.alreadyVerified()).toBeFalse();
      expect(form.successMessage()).toBe(registrationSuccessCopy('sent', entry.voice).message);
    }));
  });
});

// Review #4: the already-verified panel takes focus (the form it replaces is hidden) and offers sign in.
describe('RegistrationVerifiedComponent', () => {
  it('focuses its heading and links to login', async () => {
    TestBed.configureTestingModule({ imports: [RegistrationVerifiedComponent], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(RegistrationVerifiedComponent);
    document.body.appendChild(fixture.nativeElement);
    fixture.componentRef.setInput('message', registrationSuccessCopy('notRequired', 'siz').message);
    fixture.componentRef.setInput('loginQueryParams', { returnUrl: '/student/dashboard' });
    fixture.detectChanges();
    await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement;
    const heading = element.querySelector('h3') as HTMLElement;
    expect(heading.getAttribute('tabindex')).toBe('-1');
    expect(document.activeElement).toBe(heading);
    // Second review nit: the focused heading keeps a visible focus outline.
    expect(getComputedStyle(heading).outlineStyle).not.toBe('none');
    expect(element.querySelector('[role="status"]')?.textContent).toContain('zaten doğrulanmış');
    expect(element.textContent).not.toMatch(/gönder/i);
    const link = element.querySelector('a') as HTMLAnchorElement;
    expect(link.textContent?.trim()).toBe('Giriş yap');
    expect(link.getAttribute('href')).toBe('/auth/login?returnUrl=%2Fstudent%2Fdashboard');
    fixture.nativeElement.remove();
  });
});
