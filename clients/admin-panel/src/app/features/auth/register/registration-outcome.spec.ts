import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { Subject } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { LocationService } from '../../../core/services/location.service';
import { ConfigurationService } from '../../../core/services/settings/configuration.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { SiteAnalyticsService } from '../../../core/services/site-analytics.service';
import { InstitutionRegisterComponent } from './institution/institution-register.component';
import { ParentRegisterComponent } from './parent/parent-register.component';
import { StudentRegisterComponent } from './student/student-register.component';
import { TeacherRegisterComponent } from './teacher/teacher-register.component';
import { readVerificationEmailOutcome, registrationSuccessCopy } from './registration-outcome';
import { RegistrationSuccessComponent } from './registration-success.component';

// Paket A #2 (05.11.2026): the success screen follows the server's verificationEmail outcome.
type Voice = 'sen' | 'siz';
interface Form {
  isLoading(): boolean;
  verifiedNotice(): { title: string; message: string } | null;
  form: { setValue(value: unknown): void };
  legalReady: boolean;
  legalAcceptances: unknown[];
  onSubmit(): Promise<void> | void;
}

const password = 'Sifre-2026';
const forms: Array<{ name: string; voice: Voice; role: string; path: string; create: () => Form; value: Record<string, unknown> }> = [
  {
    name: 'student', voice: 'sen', role: 'student', path: '/auth/coaching/register/student',
    create: () => new StudentRegisterComponent() as unknown as Form,
    value: { firstName: 'Elif', lastName: 'Kaya', email: 'elif@example.test', password, confirmPassword: password },
  },
  {
    name: 'parent', voice: 'sen', role: 'parent', path: '/auth/coaching/register/parent',
    create: () => new ParentRegisterComponent() as unknown as Form,
    value: { firstName: 'Ayşe', lastName: 'Kaya', email: 'ayse@example.test', password, confirmPassword: password, phoneNumber: '5551234567' },
  },
  {
    name: 'teacher', voice: 'sen', role: 'teacher', path: '/auth/coaching/register/teacher',
    create: () => new TeacherRegisterComponent() as unknown as Form,
    value: { firstName: 'Ali', lastName: 'Demir', email: 'ali@example.test', password, confirmPassword: password, branch: 'Matematik' },
  },
  {
    name: 'institution', voice: 'sen', role: 'institution', path: '/auth/coaching/register/institution',
    create: () => new InstitutionRegisterComponent() as unknown as Form,
    value: { institutionName: 'Okul', institutionType: 1, provinceId: '34', districtId: '3401', firstName: 'Ali', lastName: 'Demir', email: 'ali@example.test', password, confirmPassword: password },
  },
];

function create(entry: (typeof forms)[number]) {
  const toaster = { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() };
  const router = { navigate: vi.fn().mockResolvedValue(true) };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(), provideHttpClientTesting(),
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
      { provide: ToasterService, useValue: toaster },
      { provide: ConfigurationService, useValue: {} },
      { provide: AuthService, useValue: {} },
      { provide: SocialAuthService, useValue: { authState: new Subject() } },
      { provide: LocationService, useValue: { getProvinces: () => new Subject(), getDistricts: () => new Subject() } },
    ],
  });
  const component = TestBed.runInInjectionContext(entry.create);
  component.legalReady = true;
  component.legalAcceptances = [{ slug: 'privacy', version: 1 }, { slug: 'kvkk', version: 1 }, { slug: 'coaching-terms', version: 1 }];
  component.form.setValue(entry.value);
  return { component, toaster, router };
}

async function submit(entry: (typeof forms)[number], body: Record<string, string>) {
  const created = create(entry);
  await created.component.onSubmit();
  const http = TestBed.inject(HttpTestingController);
  http.expectOne(candidate => candidate.url.endsWith(entry.path)).flush(body);
  http.verify();
  return created;
}

describe('registration verification e-mail outcome copy', () => {
  it('reads only an explicit notRequired; anything else (older API) means the e-mail was sent', () => {
    expect(readVerificationEmailOutcome({ userId: 'u', verificationEmail: 'notRequired' })).toBe('notRequired');
    expect(readVerificationEmailOutcome({ userId: 'u', verificationEmail: 'sent' })).toBe('sent');
    expect(readVerificationEmailOutcome({ userId: 'u' })).toBe('sent');
    expect(readVerificationEmailOutcome(null)).toBe('sent');
  });

  it.each(['sen', 'siz'] as const)('never says "gönderildi" for an already verified address (%s)', voice => {
    const verified = registrationSuccessCopy('notRequired', voice);
    expect(`${verified.title} ${verified.message}`).not.toMatch(/gönder/i);
    expect(verified.message).toContain('zaten doğrulanmış');
    expect(verified.message).toContain('giriş yapabilir');
    const sent = registrationSuccessCopy('sent', voice);
    expect(`${sent.title} ${sent.message}`).toMatch(/gönder/i);
    expect(sent.message).not.toContain('zaten doğrulanmış');
  });

  // Paket F #12: every Coaching sign-up form uses sen; the siz copy stays for other callers.
  it('has a sen and a siz version', () => {
    expect(registrationSuccessCopy('notRequired', 'sen').message).toContain('yapabilirsin.');
    expect(registrationSuccessCopy('notRequired', 'siz').message).toContain('yapabilirsiniz.');
    expect(registrationSuccessCopy('sent', 'sen').message).toContain('yapabilirsin.');
    expect(registrationSuccessCopy('sent', 'siz').message).toContain('yapabilirsiniz.');
  });

  it('renders the already-verified screen with a sign-in button and no "gönderildi"', async () => {
    TestBed.configureTestingModule({ imports: [RegistrationSuccessComponent], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(RegistrationSuccessComponent);
    document.body.appendChild(fixture.nativeElement);
    const copy = registrationSuccessCopy('notRequired', 'sen');
    fixture.componentRef.setInput('title', copy.title);
    fixture.componentRef.setInput('message', copy.message);
    fixture.componentRef.setInput('loginQueryParams', { registered: 'true', role: 'student' });
    fixture.detectChanges();
    await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('[role="status"]')?.textContent).toContain('zaten doğrulanmış');
    expect(element.textContent).not.toMatch(/gönder/i);
    // Review #4: focus moves to the panel heading so it is not lost when the form is hidden.
    const heading = element.querySelector('h3') as HTMLElement;
    expect(heading.getAttribute('tabindex')).toBe('-1');
    expect(document.activeElement).toBe(heading);
    // Second review nit: the focused heading keeps a visible focus style.
    expect(heading.className).not.toContain('outline-none');
    expect(heading.className).toContain('focus-visible:outline-2');
    const link = element.querySelector('a') as HTMLAnchorElement;
    expect(link.textContent?.trim()).toBe('Giriş yap');
    expect(link.getAttribute('href')).toBe('/auth/login?registered=true&role=student');
  });
});

describe.each(forms)('$name registration success screen', entry => {
  it('says the verification e-mail was sent for a new or unverified account and goes to login', async () => {
    const { component, toaster, router } = await submit(entry, { userId: 'u', verificationEmail: 'sent' });
    const copy = registrationSuccessCopy('sent', entry.voice);
    expect(toaster.success).toHaveBeenCalledWith(copy.message, copy.title);
    expect(component.verifiedNotice()).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], { queryParams: { registered: 'true', role: entry.role } });
  });

  // Third review N3: a sign-up is counted only when the request granted the product (new account or first join).
  it.each([
    ['a retry of a verified account', { verificationEmail: 'notRequired', newProductAccess: false }, false],
    ['a retry of an unverified account', { verificationEmail: 'sent', newProductAccess: false }, false],
    ['a first join of a verified account', { verificationEmail: 'notRequired', newProductAccess: true }, true],
    ['a new account', { verificationEmail: 'sent', newProductAccess: true }, true],
    ['an older API response', { verificationEmail: 'sent' }, true],
  ] as const)('counts a sign-up only when something new was granted: %s', async (_, response, counted) => {
    const created = create(entry);
    const track = vi.spyOn(TestBed.inject(SiteAnalyticsService), 'track');
    await created.component.onSubmit();
    TestBed.inject(HttpTestingController).expectOne(candidate => candidate.url.endsWith(entry.path)).flush({ userId: 'u', ...response });
    if (counted) expect(track).toHaveBeenCalledWith('sign_up', { method: 'password', role: entry.role });
    else expect(track).not.toHaveBeenCalledWith('sign_up', expect.anything());
  });

  it('says the e-mail is already verified, shows sign in, and does not claim an e-mail was sent', async () => {
    const { component, toaster, router } = await submit(entry, { userId: 'u', verificationEmail: 'notRequired' });
    const copy = registrationSuccessCopy('notRequired', entry.voice);
    expect(component.verifiedNotice()).toEqual(copy);
    // Review #4: the panel is the message; no second copy in a toast.
    expect(toaster.success).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(component.isLoading()).toBe(false);
  });

  it('treats an older API response without the outcome as sent', async () => {
    const { toaster } = await submit(entry, { userId: 'u' });
    const copy = registrationSuccessCopy('sent', entry.voice);
    expect(toaster.success).toHaveBeenCalledWith(copy.message, copy.title);
  });
});

// Second review M2b: an existing account with MFA can become a parent from the parent form.
describe('parent registration with an MFA-protected existing account', () => {
  it('asks for the authenticator code and sends it on the next attempt', async () => {
    const entry = forms.find(candidate => candidate.name === 'parent')!;
    const { component } = create(entry);
    const parent = component as unknown as ParentRegisterComponent;
    expect(parent.mfaRequired()).toBe(false);
    await parent.onSubmit();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(candidate => candidate.url.endsWith(entry.path)).flush(
      { code: 'Auth.ExistingAccountMfaRequired', description: 'Mevcut hesabınızın doğrulama uygulamasındaki altı haneli kodu girin.' },
      { status: 400, statusText: 'Bad Request' });
    expect(parent.mfaRequired()).toBe(true);

    parent.mfaCode.setValue('123456');
    await parent.onSubmit();
    const retry = http.expectOne(candidate => candidate.url.endsWith(entry.path));
    expect(retry.request.body.mfaCode).toBe('123456');
    retry.flush({ userId: 'u', verificationEmail: 'notRequired' });
    expect(parent.verifiedNotice()).not.toBeNull();
    http.verify();
  });

  it('does not send a code field for a normal registration', async () => {
    const entry = forms.find(candidate => candidate.name === 'parent')!;
    const { component } = create(entry);
    await component.onSubmit();
    const request = TestBed.inject(HttpTestingController).expectOne(candidate => candidate.url.endsWith(entry.path));
    expect('mfaCode' in request.request.body).toBe(false);
    request.flush({ userId: 'u', verificationEmail: 'sent' });
  });
});

// Third review item 5: an empty or invalid code is explained, not ignored.
describe('parent registration MFA code validation', () => {
  it.each(['', '12ab'])('shows a Turkish error for the code %j and sends nothing', async code => {
    const entry = forms.find(candidate => candidate.name === 'parent')!;
    const { component } = create(entry);
    const parent = component as unknown as ParentRegisterComponent;
    parent.mfaRequired.set(true);
    parent.mfaCode.setValue(code);
    await parent.onSubmit();
    TestBed.inject(HttpTestingController).expectNone(candidate => candidate.url.endsWith(entry.path));
    expect(parent.errorMessage()).toBe('Doğrulama uygulamandaki altı haneli kodu gir.');
    expect(parent.mfaCode.touched).toBe(true);
  });
});
