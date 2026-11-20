import { HttpRequest } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { maxkampusRecaptchaAction } from './interceptors';
import { MaxkampusSession } from './session.service';
import { isInternalLink, textBlocks } from './text';
import { needsPlan } from '../panel/panel-gate.component';
import { passwordLooksValid, phoneLooksValid } from '../account/register.component';

const post = (url: string) => new HttpRequest('POST', url, {});

function token(payload: object): string {
  const encode = (value: object) => btoa(JSON.stringify(value)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `${encode({ alg: 'none' })}.${encode(payload)}.x`;
}

describe('maxkampus.com core rules', () => {
  it('asks the robot check only on account requests, with Identity\'s action names', () => {
    expect(maxkampusRecaptchaAction(post('/api/auth/course-management/login'))).toBe('auth_login');
    expect(maxkampusRecaptchaAction(post('/api/auth/course-management/google'))).toBe('auth_login');
    expect(maxkampusRecaptchaAction(post('/api/auth/course-management/register'))).toBe('auth_register');
    expect(maxkampusRecaptchaAction(post('/api/auth/course-management/google-register-complete'))).toBe('auth_register');
    expect(maxkampusRecaptchaAction(post('/api/auth/forgot-password'))).toBe('auth_recovery');
    expect(maxkampusRecaptchaAction(post('/api/auth/course-management/refresh-token'))).toBeNull();
    expect(maxkampusRecaptchaAction(new HttpRequest('GET', '/api/auth/course-management/login'))).toBeNull();
    expect(maxkampusRecaptchaAction(post('/api/site/sites/maxkampus/contact'))).toBeNull();
  });

  it('renders long text as headings, paragraphs and lists, never as HTML', () => {
    expect(textBlocks('## Başlık\n\nBir satır\nikinci satır\n\n- bir\n- iki\n\n<script>x</script>')).toEqual([
      { kind: 'heading', text: 'Başlık' },
      { kind: 'paragraph', text: 'Bir satır ikinci satır' },
      { kind: 'list', items: ['bir', 'iki'] },
      { kind: 'paragraph', text: '<script>x</script>' }
    ]);
    expect(isInternalLink('/fiyatlar')).toBe(true);
    expect(isInternalLink('//kotu.example')).toBe(false);
    expect(isInternalLink('https://eduivme.com')).toBe(false);
  });

  it('checks the phone and password as the service does', () => {
    for (const phone of ['0532 123 45 67', '+90 (212) 555-12-34', '5321234567', '+49 30 1234567']) expect(phoneLooksValid(phone)).toBe(true);
    for (const phone of ['12345', '0123 456 78 90', '']) expect(phoneLooksValid(phone)).toBe(false);
    expect(passwordLooksValid('Guclu-Sifre1')).toBe(true);
    expect(passwordLooksValid('zayifsifre')).toBe(false);
  });

  it('sends an owner without a running plan to the plan page', () => {
    expect(needsPlan(null)).toBe(false);
    expect(needsPlan({ requireSubscription: false, salesOpen: true, subscriptions: [], pendingOrders: [] })).toBe(false);
    expect(needsPlan({ requireSubscription: true, salesOpen: true, subscriptions: [], pendingOrders: [] })).toBe(true);
    expect(needsPlan({ requireSubscription: true, salesOpen: true, pendingOrders: [],
      subscriptions: [{ id: 's', planName: 'Kurum', endsAt: '2027-01-01', isCurrent: true }] })).toBe(false);
  });

  it('reads the maxkampüs role from the session token', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const session = TestBed.inject(MaxkampusSession);
    const exp = Math.floor(Date.now() / 1000) + 600;
    expect(session.apply({ accessToken: token({ sub: 'u1', email: 'test@example.com', exp,
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'CourseInstitutionOwner' }) })).toBe(true);
    expect(session.role()).toBe('owner');
    expect(session.email()).toBe('test@example.com');
    expect(session.accessToken()).not.toBe('');
    session.apply({ accessToken: token({ sub: 'u2', exp, 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': ['CourseTeacher'] }) });
    expect(session.role()).toBe('teacher');
    expect(session.apply({ requiresMfa: true, mfaChallengeToken: 'c' })).toBe(false);
  });
});

describe('the way from sign-up to the panel', () => {
  it('shows plan, payment, approval and setup in turn', async () => {
    const { planStep, START_STEPS } = await import('../site/start-steps.component');
    expect(START_STEPS).toEqual(['Hesap', 'Plan', 'Ödeme', 'Onay', 'Kurulum']);
    expect(planStep({ subscriptions: [], pendingOrders: [] })).toBe(1);
    expect(planStep({ subscriptions: [], pendingOrders: [{ paidDeclaredAt: null }] })).toBe(2);
    expect(planStep({ subscriptions: [], pendingOrders: [{ paidDeclaredAt: '2026-11-21T10:00:00Z' }] })).toBe(3);
    expect(planStep({ subscriptions: [{ isCurrent: true }], pendingOrders: [] })).toBe(4);
  });
});
