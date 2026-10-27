import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ChangeDetectorRef } from '@angular/core';
import { ExistingProductJoinComponent } from './existing-product-join.component';
import { environment } from '../../../environments/environment';

describe('ExistingProductJoinComponent', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [{ provide: ChangeDetectorRef, useValue: { markForCheck: () => {} } }] }));
  it('renders the required institution choices after a delayed HTTP response', async () => {
    TestBed.configureTestingModule({ imports: [ExistingProductJoinComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    TestBed.overrideComponent(ExistingProductJoinComponent, { set: { template: '<button (click)="submit()" [disabled]="loading">Katıl</button><p role="alert">{{error}}</p>@for (institution of institutions; track institution.id) {<option>{{institution.name}}</option>}' } });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(ExistingProductJoinComponent);
    const c = fixture.componentInstance;
    c.account = 'institution'; c.legalReady = true;
    c.form.setValue({ email: 'existing@example.test', password: 'legacy-password', mfaCode: '' });
    c.legalAcceptances = ['privacy','kvkk','coaching-terms'].map(slug => ({ slug, version: 1 }));
    fixture.detectChanges(); fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();
    TestBed.inject(HttpTestingController).expectOne('/api/auth/coaching/register/join').flush({
      code: 'Auth.InstitutionSelectionRequired', institutions: [{ id: 'id', name: 'İkinci kurum', role: 'Manager' }]
    }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('button').disabled).toBe(false);
    expect(fixture.nativeElement.querySelector('option').textContent).toBe('İkinci kurum');
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('kurumu listeden seçin');
    TestBed.inject(HttpTestingController).verify();
  });
  it('uses the dedicated Speed Reading origin for student login', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const c = TestBed.runInInjectionContext(() => new ExistingProductJoinComponent());
    c.product = 'speed-reading'; c.account = 'student';
    expect(c.loginUrl).toBe('https://masterhizliokuma.com/auth/login');
  });
  function create(account: 'student' | 'teacher' | 'institution' = 'student') {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const c = TestBed.runInInjectionContext(() => new ExistingProductJoinComponent());
    c.account = account;
    c.form.setValue({ email: 'existing@example.test', password: 'legacy-password', mfaCode: '' });
    c.legalReady = true;
    c.legalAcceptances = ['privacy','kvkk','coaching-terms'].map(slug => ({ slug, version: 1 }));
    return c;
  }
  it('requires an explicit institution choice and sends only the selected id', () => {
    const c = create('institution'); c.submit();
    const http = TestBed.inject(HttpTestingController);
    const first = http.expectOne('/api/auth/coaching/register/join');
    expect(first.request.body.institutionId).toBeUndefined();
    first.flush({ code: 'Auth.InstitutionSelectionRequired', description: 'Kurum seçin.', institutions: [
      { id: '11111111-1111-1111-1111-111111111111', name: 'First', role: 'Manager' },
      { id: '22222222-2222-2222-2222-222222222222', name: 'Second', role: 'Admin' }
    ] }, { status: 400, statusText: 'Bad Request' });
    expect(c.institutions.length).toBe(2); expect(c.joined).toBe(false);
    expect(c.error).toBe('Katılacağınız kurumu listeden seçin.');
    c.submit(); http.expectNone('/api/auth/coaching/register/join');
    c.selectedInstitutionId = '22222222-2222-2222-2222-222222222222'; c.submit();
    const selected = http.expectOne('/api/auth/coaching/register/join');
    expect(selected.request.body.institutionId).toBe(c.selectedInstitutionId);
    selected.flush({ userId: 'preserved' }); expect(c.joined).toBe(true); http.verify();
  });
  it('clears institution choices when the account credentials change', () => {
    const c = create('institution');
    c.institutions = [{ id: 'old', name: 'Old institution', role: 'Owner' }];
    c.selectedInstitutionId = 'old';
    c.form.controls.email.setValue('other@example.test');
    expect(c.institutions.length).toBe(0); expect(c.selectedInstitutionId).toBe('');
  });
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
      expect(c.loginUrl).toContain(account === 'student' ? '/auth/login' : environment.staffPortalUrl);
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
