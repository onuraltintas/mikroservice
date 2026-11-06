import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { vi } from 'vitest';
import { OnboardingView } from '../../../core/services/coaching-onboarding.service';
import { StudentOnboardingComponent } from './student-onboarding.component';

describe('StudentOnboardingComponent (Paket C #3)', () => {
  const base = '/api/coaching/students/me/onboarding';
  const sessions = [{ id: 'lgs-27', examCode: 'LGS', examName: 'LGS', date: '2027-06-13', label: '2027', applicationStart: null, applicationEnd: null }];
  const view = (over: Partial<OnboardingView> = {}, answers: Partial<OnboardingView['answers']> = {}): OnboardingView => ({
    status: 'NotStarted', nextStep: 1, savedSteps: [], completedAt: null, academicYearStart: 2026, examSessions: sessions,
    answers: { birthDate: null, homeProvinceId: null, homeCity: null, homeDistrictId: null, homeDistrict: null, educationStatus: 'Unknown', gradeLevel: null,
      graduationYear: null, schoolName: null, purpose: null, primaryScoreType: null, foreignLanguage: null, targetExamSessionId: null, targetExamYear: null,
      diplomaGrade: null, diplomaGradeEstimated: false, target: null, ...answers }, ...over });
  const stepTwo = { birthDate: '2012-05-01', homeProvinceId: '06', homeCity: 'Ankara', homeDistrictId: '0601', homeDistrict: 'Çankaya',
    educationStatus: 'Student' as const, gradeLevel: 8, purpose: 'Lgs' as const, targetExamSessionId: 'lgs-27', targetExamYear: 2027 };

  async function render(first: OnboardingView, query: Record<string, string> = {}) {
    const auth = { userProfile: signal({ id: 'student-1' }), logout: vi.fn().mockResolvedValue(undefined) };
    TestBed.configureTestingModule({ imports: [StudentOnboardingComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: AuthService, useValue: auth },
      ...(Object.keys(query).length ? [{ provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(query) }, queryParamMap: of(convertToParamMap(query)) } }] : [])] });
    const router = TestBed.inject(Router); const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(StudentOnboardingComponent);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne(base).flush({ data: first });
    await settle();
    if (first.status !== 'Completed') http.expectOne('/api/locations/provinces').flush([{ id: '06', name: 'Ankara' }, { id: '34', name: 'İstanbul' }]);
    await settle();
    const el = fixture.nativeElement as HTMLElement;
    async function settle() { for (let i = 0; i < 4; i++) { await new Promise(r => setTimeout(r)); fixture.detectChanges(); await fixture.whenStable(); } }
    const button = (text: string) => Array.from(el.querySelectorAll('[data-current] button, .ob-summary button, .ob-summary a')).find(x => x.textContent?.trim() === text) as HTMLElement;
    const click = async (text: string) => { const target = button(text); expect(target, text).toBeTruthy(); target.click(); await settle(); };
    return { fixture, http, el, settle, click, button, navigate, auth };
  }

  it('is a chat: one question with its reason, a progress bar, a live log, and answers that stay as bubbles with "Değiştir"', async () => {
    const t = await render(view());
    expect(t.el.querySelector('[role="log"][aria-live="polite"]')).not.toBeNull();
    const bar = t.el.querySelector('[role="progressbar"]')!;
    expect(bar.getAttribute('aria-valuenow')).toBe('1'); expect(t.el.textContent).toContain('Adım 1/7');
    const current = t.el.querySelector('[data-current]')!;
    expect(current.textContent).toContain('Doğum tarihin ne?');
    expect(current.querySelector('.ob-why')!.textContent!.length).toBeGreaterThan(10);
    expect(t.el.querySelector('textarea')).toBeNull();

    const date = current.querySelector('input[type="date"]') as HTMLInputElement;
    date.value = '2012-05-01'; date.dispatchEvent(new Event('input')); await t.click('Devam');
    const bubble = t.el.querySelector('[data-question="birthDate"] .ob-me')!;
    expect(bubble.textContent).toContain('01.05.2012'); expect(bubble.textContent).toContain('Değiştir');

    // City: a searchable list of the 81 provinces from Identity.
    const box = t.el.querySelector('[data-current] input[role="combobox"]') as HTMLInputElement;
    box.value = 'ank'; box.dispatchEvent(new Event('input')); await t.settle();
    const options = Array.from(t.el.querySelectorAll('[role="option"]'));
    expect(options.map(x => x.textContent!.trim())).toEqual(['Ankara']);
    (options[0] as HTMLElement).click(); await t.settle();
    t.http.expectOne('/api/locations/provinces/06/districts').flush([{ id: '0601', provinceId: '06', name: 'Çankaya' }]);
    await t.settle();
    const district = t.el.querySelector('[data-current] input[role="combobox"]') as HTMLInputElement;
    district.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' })); district.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    await t.settle();
    await t.click('8. sınıf');
    const save = t.http.expectOne(`${base}/steps/personal`);
    expect(save.request.body).toEqual({ birthDate: '2012-05-01', provinceId: '06', districtId: '0601', educationStatus: 'Student', gradeLevel: 8, graduationYear: null, schoolId: null });
    save.flush({ data: view({ status: 'InProgress', nextStep: 2, savedSteps: [1] }, { ...stepTwo, purpose: null, targetExamSessionId: null, targetExamYear: null }) });
    await t.settle();
    expect(t.el.querySelector('[data-current]')!.textContent).toContain('Hangi sınava hazırlanıyorsun?');
    expect(t.el.textContent).toContain('Adım 2/7');
    t.http.verify();
  });

  it('saves each step on its own and ends with the summary and "Planını kur"', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 3, savedSteps: [1, 2] }, stepTwo));
    expect(t.el.querySelector('[data-current]')!.textContent).toContain('Hedefin ne?');
    await t.click('Henüz karar vermedim');
    const target = t.http.expectOne(`${base}/steps/target`);
    expect(target.request.body).toEqual({ kind: 'none', targetId: null, scores: null });
    target.flush({ data: view({ status: 'InProgress', nextStep: 4, savedSteps: [1, 2, 3] }, stepTwo) }); await t.settle();

    t.http.expectOne('/api/coaching/invitations/my-invitations').flush([]); await t.settle();
    expect(t.el.textContent).toContain('Şu an bekleyen davetin yok');
    await t.click('Davetim yok, devam');
    t.http.expectOne(`${base}/steps/4`).flush({ data: view({ status: 'InProgress', nextStep: 5, savedSteps: [1, 2, 3, 4] }, stepTwo) }); await t.settle();

    await t.click('Veli kodu oluştur');
    t.http.expectOne('/api/users/me/parent-link-codes').flush({ code: 'AB12CD', expiresAt: '2026-11-13T00:00:00Z' }); await t.settle();
    expect(t.el.textContent).toContain('AB12CD');
    await t.click('Devam');
    t.http.expectOne(`${base}/steps/5`).flush({ data: view({ status: 'InProgress', nextStep: 6, savedSteps: [1, 2, 3, 4, 5] }, stepTwo) }); await t.settle();

    t.http.expectOne('/api/notifications/preferences').flush({ data: [
      { category: 'StudyPlan', label: 'Çalışma planı hatırlatmaları', inApp: true, email: false },
      { category: 'Messages', label: 'Yeni mesajlar', inApp: true, email: true },
      { category: 'AssignmentReminders', label: 'Ödev teslim hatırlatması', inApp: true, email: true }] });
    await t.settle();
    expect(t.el.querySelector('[data-testid="ob-email-Messages"]')).toBeNull();
    (t.el.querySelector('[data-testid="ob-email-StudyPlan"]') as HTMLInputElement).click(); await t.settle();
    await t.click('Kaydet ve devam et');
    const prefs = t.http.expectOne('/api/notifications/preferences');
    expect(prefs.request.method).toBe('PUT');
    expect(prefs.request.body.preferences).toEqual([
      { category: 'StudyPlan', inApp: true, email: true, sms: false, whatsApp: false },
      { category: 'AssignmentReminders', inApp: true, email: true, sms: false, whatsApp: false }]);
    prefs.flush({ data: [] }); await t.settle();
    t.http.expectOne(`${base}/steps/6`).flush({ data: view({ status: 'InProgress', nextStep: 7, savedSteps: [1, 2, 3, 4, 5, 6] }, stepTwo) }); await t.settle();

    expect(t.el.textContent).toContain('Adım 7/7');
    const summary = t.el.querySelector('.ob-summary')!;
    expect(summary.textContent).toContain('Sınav: LGS'); expect(summary.textContent).toContain('Hedef: Henüz karar vermedim');
    expect(summary.querySelectorAll('button[aria-label^="Düzenle"]').length).toBe(6);
    await t.click('Kurulumu bitir');
    t.http.expectOne(`${base}/complete`).flush({ data: { needsSubscription: false, state: view({ status: 'Completed', savedSteps: [1, 2, 3, 4, 5, 6, 7] }, stepTwo) } });
    await t.settle();
    const plan = Array.from(t.el.querySelectorAll('a')).find(x => x.textContent?.trim() === 'Planını kur')!;
    expect(plan.getAttribute('href')).toBe('/coaching-portal/account/settings');
    expect(t.navigate).not.toHaveBeenCalled();
    t.http.verify();
  });

  it('sends a student who needs a subscription to the subscription page after the summary', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 7, savedSteps: [1, 2, 3, 4, 5, 6] }, stepTwo));
    await t.click('Kurulumu bitir');
    t.http.expectOne(`${base}/complete`).flush({ data: { needsSubscription: true, state: view({ status: 'Completed' }, stepTwo) } });
    await t.settle();
    expect(t.navigate).toHaveBeenCalledWith(['/coaching-portal/account/payments'], { queryParams: { required: 1, from: 'welcome' } });
  });

  it('"Değiştir" on the exam asks again only the questions that depend on it, then the target', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 7, savedSteps: [1, 2, 3, 4, 5, 6] }, stepTwo));
    (t.el.querySelector('[data-question="purpose"] .ob-me button') as HTMLElement).click(); await t.settle();
    expect(t.el.querySelector('[data-current]')!.textContent).toContain('Hangi sınava hazırlanıyorsun?');
    // Personal answers stay.
    expect(t.el.querySelector('[data-question="district"] .ob-me')!.textContent).toContain('Çankaya');
    await t.click('YKS');
    await t.click('Sayısal');
    await t.click('2028');
    const exam = t.http.expectOne(`${base}/steps/exam`);
    expect(exam.request.body).toEqual({ purpose: 'Yks', primaryScoreType: 'SAY', foreignLanguage: null, targetExamSessionId: null, targetExamYear: 2028,
      diplomaGrade: null, diplomaGradeEstimated: false });
    exam.flush({ data: view({ status: 'InProgress', nextStep: 3, savedSteps: [1, 2, 4, 5, 6] }, { ...stepTwo, purpose: 'Yks', primaryScoreType: 'SAY', targetExamSessionId: null, targetExamYear: 2028 }) });
    await t.settle();
    const current = t.el.querySelector('[data-current]')!;
    expect(current.textContent).toContain('Hedefin ne?'); expect(current.textContent).toContain('Bölüm seçeceğim');
    // The invite, parent and notification steps are not asked again.
    expect(t.el.querySelector('[data-question="invite"]')).toBeNull();
    t.http.verify();
  });

  it('shows the server message when a step cannot be saved and keeps the answers', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 3, savedSteps: [1, 2] }, stepTwo));
    await t.click('Henüz karar vermedim');
    t.http.expectOne(`${base}/steps/target`).flush({ message: 'Önce hazırlandığın sınavı seç.' }, { status: 400, statusText: 'Bad Request' });
    await t.settle();
    expect(t.el.querySelector('[role="alert"]')!.textContent).toContain('Önce hazırlandığın sınavı seç.');
    expect(t.el.querySelector('[data-question="targetMode"] .ob-me')!.textContent).toContain('Henüz karar vermedim');
  });

  it('"Hedefini belirle" after the wizard asks only the target, saves it and returns to Bugün', async () => {
    const t = await render(view({ status: 'Completed', nextStep: 7, savedSteps: [1, 2, 3, 4, 5, 6, 7], completedAt: '2026-11-06T00:00:00Z' }, stepTwo), { step: 'target' });
    expect(t.el.querySelector('h1')!.textContent).toContain('Hedefini belirle');
    expect(t.el.querySelector('[role="progressbar"]')).toBeNull();
    expect(t.el.querySelector('[data-current]')!.textContent).toContain('Hedefin ne?');
    expect(t.el.querySelector('[data-question="birthDate"]')).toBeNull();
    await t.click('Hedef puan gireceğim');
    const input = t.el.querySelector('[data-current] input[type="number"]') as HTMLInputElement;
    input.value = '0'; input.dispatchEvent(new Event('input')); await t.click('Devam');
    expect(t.el.querySelector('[role="alert"]')!.textContent).toContain("0'dan büyük, en çok 500");
    input.value = '450'; input.dispatchEvent(new Event('input')); await t.click('Devam');
    const save = t.http.expectOne(`${base}/steps/target`);
    expect(save.request.body).toEqual({ kind: 'score', targetId: null, scores: [{ scoreType: 'LGS', score: 450 }] });
    save.flush({ data: view({ status: 'Completed', savedSteps: [1, 2, 3, 4, 5, 6, 7] }, stepTwo) }); await t.settle();
    expect(t.navigate).toHaveBeenCalledWith(['/coaching-portal']);
  });

  it('a teacher\'s goal cannot be changed from "Hedefini belirle"', async () => {
    const t = await render(view({ status: 'Completed', savedSteps: [1, 2, 3, 4, 5, 6, 7] }, { ...stepTwo, targetLocked: true }), { step: 'target' });
    expect(t.el.querySelector('.ob-summary')!.textContent).toContain('Hedefini öğretmenin belirledi');
    t.http.expectNone(`${base}/steps/target`);
  });

  it('offers sign-out and Kişisel verilerim, and a retry when the e-mail choices cannot be loaded', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 6, savedSteps: [1, 2, 3, 4, 5] }, stepTwo));
    expect(t.el.querySelector('a[href="/coaching-portal/account/data"]')).not.toBeNull();
    (Array.from(t.el.querySelectorAll('button')).find(x => x.textContent?.trim() === 'Çıkış yap') as HTMLElement).click();
    expect(t.auth.logout).toHaveBeenCalled();
    t.http.expectOne('/api/notifications/preferences').flush({}, { status: 500, statusText: 'Error' }); await t.settle();
    expect(t.el.querySelector('[data-current]')!.textContent).toContain('E-posta tercihlerin yüklenemedi.');
    expect(t.button('Kaydet ve devam et')).toBeUndefined();
    await t.click('Yeniden dene');
    t.http.expectOne('/api/notifications/preferences').flush({ data: [{ category: 'StudyPlan', label: 'Çalışma planı hatırlatmaları', inApp: true, email: false }] });
    await t.settle();
    expect(t.button('Kaydet ve devam et')).toBeTruthy();
  });

  it('asks "Seni davet eden bir öğretmenin ya da kurumun var mı?" (no code)', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 4, savedSteps: [1, 2, 3] }, stepTwo));
    t.http.expectOne('/api/coaching/invitations/my-invitations').flush([]); await t.settle();
    expect(t.el.querySelector('[data-current]')!.textContent).toContain('Seni davet eden bir öğretmenin ya da kurumun var mı?');
    expect(t.el.textContent).not.toMatch(/kod(un)? var/i);
  });

  it('lists the schools of the student\'s district first (own school and the LGS school step in the home city)', async () => {
    const t = await render(view());
    const date = t.el.querySelector('[data-current] input[type="date"]') as HTMLInputElement;
    date.value = '2010-05-01'; date.dispatchEvent(new Event('input')); await t.click('Devam');
    const city = t.el.querySelector('[data-current] input[role="combobox"]') as HTMLInputElement;
    city.value = 'ank'; city.dispatchEvent(new Event('input')); await t.settle();
    (t.el.querySelector('[role="option"]') as HTMLElement).click(); await t.settle();
    t.http.expectOne('/api/locations/provinces/06/districts').flush([{ id: '0601', provinceId: '06', name: 'Çankaya' }]); await t.settle();
    (t.el.querySelector('[role="option"]') as HTMLElement).click(); await t.settle();
    await t.click('10. sınıf');
    await new Promise(r => setTimeout(r, 300)); await t.settle();
    const own = t.http.expectOne(r => r.url === `${base}/options/schools`);
    expect(own.request.params.get('city')).toBe('Ankara'); expect(own.request.params.get('district')).toBe('Çankaya');
    own.flush({ data: { items: [{ id: 's1', name: 'Çankaya Lisesi', district: 'ÇANKAYA', minimumScore: null, scoreYear: null, inHomeDistrict: true }], totalCount: 1, pageNumber: 1, pageSize: 20 } });
    await t.settle();
    expect(t.el.querySelector('[role="option"]')!.textContent).toContain('ilçende');
  });

  it('a student who already finished goes to the panel', async () => {
    const t = await render(view({ status: 'Completed' }));
    expect(t.navigate).toHaveBeenCalledWith(['/coaching-portal']);
  });
});
