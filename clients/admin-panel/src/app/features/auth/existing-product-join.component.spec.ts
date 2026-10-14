import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ExistingProductJoinComponent } from './existing-product-join.component';

describe('ExistingProductJoinComponent', () => {
  function create(account: 'student' | 'teacher' | 'institution' = 'student') {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const c = TestBed.runInInjectionContext(() => new ExistingProductJoinComponent());
    c.account = account;
    c.form.setValue({ email: 'existing@example.test', password: 'legacy-password', mfaCode: '' });
    c.legalReady = true;
    c.legalAcceptances = ['privacy','kvkk','coaching-terms'].map(slug => ({ slug, version: 1 }));
    return c;
  }
  for (const account of ['student','teacher','institution'] as const) {
    it('joins an existing ' + account + ' without replacing identity or institution fields', () => {
      const c = create(account); c.submit();
      const http = TestBed.inject(HttpTestingController);
      const req = http.expectOne('/api/auth/coaching/register/join');
      expect(req.request.body.account).toBe(account);
      expect(req.request.body.password).toBe('legacy-password');
      expect(req.request.body.firstName).toBeUndefined();
      expect(req.request.body.institutionName).toBeUndefined();
      expect(req.request.body.legalAcceptances.length).toBe(3);
      req.flush({ userId: 'preserved-user-id' });
      expect(c.joined).toBe(true); expect(c.loading).toBe(false);
      expect(c.loginUrl).toContain(account === 'student' ? '/auth/login' : '/staff/');
      http.verify();
    });
  }
  it('does not submit without current legal acceptances', () => {
    const c = create(); c.legalAcceptances = []; c.submit();
    TestBed.inject(HttpTestingController).expectNone('/api/auth/coaching/register/join');
    expect(c.joined).toBe(false);
  });
  it('shows the MFA response and retries with the current code', () => {
    const c = create('teacher'); c.submit();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/auth/coaching/register/join').flush(
      { code: 'Auth.ExistingAccountMfaRequired', description: 'Yeni doğrulama kodunu girin.' },
      { status: 400, statusText: 'Bad Request' });
    expect(c.error).toBe('Yeni doğrulama kodunu girin.'); expect(c.joined).toBe(false);
    c.form.controls.mfaCode.setValue('123456'); c.submit();
    const req = http.expectOne('/api/auth/coaching/register/join');
    expect(req.request.body.mfaCode).toBe('123456'); expect(c.error).toBe('');
    req.flush({ userId: 'existing' }); expect(c.joined).toBe(true); http.verify();
  });
});
