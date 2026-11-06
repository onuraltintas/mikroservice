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
import { ToasterService } from '../../../core/services/toaster.service';

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

  interface Resume { invitations?: object[]; code?: { expiresAt: string; createdAt: string; expired?: boolean } | null; links?: { parentName: string; status: string }[]; prefs?: { category: string; label: string; inApp: boolean; email: boolean }[] }
  async function render(first: OnboardingView, query: Record<string, string> = {}, resume: Resume = {}, options: { embedded?: boolean; confirm?: boolean } = {}) {
    const auth = { userProfile: signal({ id: 'student-1' }), logout: vi.fn().mockResolvedValue(undefined) };
    const toaster = { confirm: vi.fn().mockResolvedValue(options.confirm ?? true), success: vi.fn(), error: vi.fn() };
    TestBed.configureTestingModule({ imports: [StudentOnboardingComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: AuthService, useValue: auth }, { provide: ToasterService, useValue: toaster },
      ...(Object.keys(query).length ? [{ provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(query) }, queryParamMap: of(convertToParamMap(query)) } }] : [])] });
    const router = TestBed.inject(Router); const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(StudentOnboardingComponent);
    const http = TestBed.inject(HttpTestingController);
    if (options.embedded) fixture.componentRef.setInput('embedded', true);
    const saved = vi.fn(); fixture.componentInstance.targetSaved.subscribe(saved);
    fixture.detectChanges();
    http.expectOne(base).flush({ data: first });
    await settle();
    const wizard = first.status !== 'Completed' && !options.embedded;
    if (wizard) http.expectOne('/api/locations/provinces').flush([{ id: '06', name: 'Ankara' }, { id: '34', name: 'İstanbul' }]);
    // E4 #18: a resumed wizard rebuilds the answers of saved steps 4–6 from Coaching, Identity and Notification.
    if (wizard && first.savedSteps.includes(4) && !first.acceptedInvitations?.length)
      http.expectOne('/api/coaching/invitations/my-invitations').flush(resume.invitations ?? []);
    if (wizard && first.savedSteps.includes(5)) {
      http.expectOne('/api/users/me/parent-link-codes/active').flush(resume.code ?? null);
      http.expectOne('/api/users/me/parent-links').flush(resume.links ?? []);
    }
    if (wizard && first.savedSteps.includes(6)) http.expectOne('/api/notifications/preferences').flush({ data: resume.prefs ?? [] });
    await settle();
    const el = fixture.nativeElement as HTMLElement;
    async function settle() { for (let i = 0; i < 4; i++) { await new Promise(r => setTimeout(r)); fixture.detectChanges(); await fixture.whenStable(); } }
    const button = (text: string) => Array.from(el.querySelectorAll('[data-current] button, .ob-summary button, .ob-summary a')).find(x => x.textContent?.trim() === text) as HTMLElement;
    const click = async (text: string) => { const target = button(text); expect(target, text).toBeTruthy(); target.click(); await settle(); };
    return { fixture, http, el, settle, click, button, navigate, auth, saved, toaster };
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
    expect(t.el.querySelector('[data-current]')!.textContent).toContain('Şu an durumun ne?');
    await t.click('Ortaokul öğrencisiyim');
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

    t.http.expectOne('/api/users/me/parent-link-codes/active').flush(null); await t.settle();
    await t.click('Veli kodu oluştur');
    t.http.expectOne('/api/users/me/parent-link-codes').flush({ code: 'AB12CD', expiresAt: '2026-11-13T09:00:00Z' }); await t.settle();
    expect(t.el.textContent).toContain('AB12CD');
    expect(t.el.querySelector('[data-current]')!.textContent).toContain('13.11.2026 tarihine kadar, bir kez kullanılabilir.');
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
    expect(summary.textContent).toContain('Davet: Davetim yok');
    expect(summary.textContent).toContain('Veli bağlantısı: Veli kodu oluşturdum (13.11.2026 tarihine kadar geçerli)');
    expect(summary.textContent).toContain('E-postalar: Çalışma planı hatırlatmaları, Ödev teslim hatırlatması');
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
    // Paket D: YKS is offered from 9th grade on, so this student is in 10th grade (with an older LGS answer).
    const tenth = { ...stepTwo, gradeLevel: 10 };
    const t = await render(view({ status: 'InProgress', nextStep: 7, savedSteps: [1, 2, 3, 4, 5, 6] }, tenth));
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
    exam.flush({ data: view({ status: 'InProgress', nextStep: 3, savedSteps: [1, 2, 4, 5, 6] }, { ...tenth, purpose: 'Yks', primaryScoreType: 'SAY', targetExamSessionId: null, targetExamYear: 2028 }) });
    await t.settle();
    const current = t.el.querySelector('[data-current]')!;
    expect(current.textContent).toContain('Hedefin ne?'); expect(current.textContent).toContain('Bölüm seçeceğim');
    // The invite, parent and notification steps are not asked again.
    expect(t.el.querySelector('[data-question="invite"]')).toBeNull();
    t.http.verify();
  });

  it('asks the exam again instead of resending one the grade no longer fits (review M1)', async () => {
    // Step 2 re-opened by a grade change (e.g. by the institution) while the wizard was open: the saved LGS no longer fits 11th grade.
    const t = await render(view({ status: 'InProgress', nextStep: 2, savedSteps: [1], allowedPurposes: ['Yks', 'Yds', 'Yokdil'] }, { ...stepTwo, gradeLevel: 11 }));
    t.http.expectNone(`${base}/steps/exam`);
    const current = t.el.querySelector('[data-current]')!;
    expect(current.getAttribute('data-question')).toBe('purpose');
    expect(Array.from(current.querySelectorAll('button')).map(x => x.textContent?.trim())).not.toContain('LGS');
    expect(t.button('YKS')).toBeTruthy();
  });

  it('a new grade in the wizard drops an exam that no longer fits once the server answers (review M1)', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 3, savedSteps: [1, 2], allowedPurposes: ['Lgs'] }, stepTwo));
    (t.el.querySelector('[data-question="stage"] .ob-me button') as HTMLElement).click(); await t.settle();
    await t.click('Lise öğrencisiyim');
    await t.click('9. sınıf');
    await t.click('Şimdilik geçeyim');
    t.http.expectOne(`${base}/steps/personal`).flush({ data: view({ status: 'InProgress', nextStep: 2, savedSteps: [1], allowedPurposes: ['Yks', 'Yds', 'Yokdil'] },
      { ...stepTwo, gradeLevel: 9 }) });
    await t.settle();
    t.http.expectNone(`${base}/steps/exam`);
    expect(t.el.querySelector('[data-current]')!.getAttribute('data-question')).toBe('purpose');
  });

  it('shows the server message when a step cannot be saved and keeps the answers', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 3, savedSteps: [1, 2] }, stepTwo));
    await t.click('Henüz karar vermedim');
    t.http.expectOne(`${base}/steps/target`).flush({ message: 'Önce hazırlandığın sınavı seç.' }, { status: 500, statusText: 'Server Error' });
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
    await t.click('Lise öğrencisiyim');
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

  // ---------- E4 (live test 06.11.2026) ----------

  it('a reloaded wizard shows the real answers of the invite, parent and e-mail steps (#18)', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 7, savedSteps: [1, 2, 3, 4, 5, 6],
      acceptedInvitations: [{ inviterEmail: 'ogretmen@example.test', type: 'StudentToTeacher', acceptedAt: '2026-11-06T09:00:00Z' }] }, stepTwo), {}, {
      code: { expiresAt: '2026-11-13T09:00:00Z', createdAt: '2026-11-06T09:00:00Z' },
      prefs: [{ category: 'StudyPlan', label: 'Çalışma planı hatırlatmaları', inApp: true, email: true },
        { category: 'Sessions', label: 'Seans hatırlatmaları', inApp: true, email: false },
        { category: 'Messages', label: 'Yeni mesajlar', inApp: true, email: true }] });
    const summary = t.el.querySelector('.ob-summary')!.textContent!;
    expect(summary).toContain('Davet: Daveti kabul ettim: ogretmen@example.test');
    expect(summary).toContain('Veli bağlantısı: Veli kodu oluşturdum (13.11.2026 tarihine kadar geçerli)');
    expect(summary).toContain('E-postalar: Çalışma planı hatırlatmaları');
    expect(summary).not.toContain('Yeni mesajlar');
    expect(summary).not.toContain('Tamam'); expect(summary).not.toContain('Tercihlerim kaydedildi');
    t.http.verify();
  });

  it('a validation message has no "Yeniden dene"; a server error does (#18)', async () => {
    const twelfth = { ...stepTwo, educationStatus: 'Student' as const, gradeLevel: 12, purpose: null, targetExamSessionId: null, targetExamYear: null };
    const t = await render(view({ status: 'InProgress', nextStep: 2, savedSteps: [1], allowedPurposes: ['Yks', 'Yds', 'Yokdil'] }, twelfth));
    await t.click('YKS'); await t.click('Sayısal'); await t.click('2028');
    const grade = t.el.querySelector('[data-current] input[type="number"]') as HTMLInputElement;
    grade.value = '45'; grade.dispatchEvent(new Event('input')); await t.click('Kaydet');
    const alert = t.el.querySelector('[role="alert"]')!;
    expect(alert.textContent).toContain('50 ile 100 arasında bir sayı yaz.');
    expect(t.el.querySelector('[data-current]')!.contains(alert)).toBe(true);
    expect(Array.from(t.el.querySelectorAll('button')).map(x => x.textContent?.trim())).not.toContain('Yeniden dene');
    t.http.expectNone(`${base}/steps/exam`);

    grade.value = '85'; grade.dispatchEvent(new Event('input')); await t.click('Kaydet');
    expect(t.el.textContent).not.toContain('50 ile 100 arasında');
    t.http.expectOne(`${base}/steps/exam`).flush({ message: 'Sunucuya ulaşılamadı.' }, { status: 503, statusText: 'Unavailable' }); await t.settle();
    expect(t.el.querySelector('[role="alert"]')!.textContent).toContain('Sunucuya ulaşılamadı.');
    expect(Array.from(t.el.querySelectorAll('button')).map(x => x.textContent?.trim())).toContain('Yeniden dene');
  });

  it('the parent step shows an active code with its expiry, and a new code replaces it (#18, #21)', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 5, savedSteps: [1, 2, 3, 4] }, stepTwo));
    t.http.expectOne('/api/users/me/parent-link-codes/active').flush({ expiresAt: '2026-11-13T09:00:00Z', createdAt: '2026-11-06T09:00:00Z' }); await t.settle();
    const current = t.el.querySelector('[data-current]')!;
    expect(current.textContent).toContain('Aktif bir veli kodun var: 13.11.2026 tarihine kadar, bir kez kullanılabilir.');
    expect(current.textContent).toContain('Yeni kod oluşturursan eski kod hemen geçersiz olur.');
    expect(t.button('Veli kodu oluştur')).toBeUndefined();
    await t.click('Yeni kod oluştur');
    t.http.expectOne('/api/users/me/parent-link-codes').flush({ code: 'XY34-ZW56', expiresAt: '2026-11-13T10:00:00Z' }); await t.settle();
    expect(t.el.querySelector('[data-current]')!.textContent).toContain('XY34-ZW56');
    await t.click('Devam');
    t.http.expectOne(`${base}/steps/5`).flush({ data: view({ status: 'InProgress', nextStep: 6, savedSteps: [1, 2, 3, 4, 5] }, stepTwo) }); await t.settle();
    expect(t.el.querySelector('[data-question="parent"] .ob-me')!.textContent).toContain('Veli kodu oluşturdum (13.11.2026 tarihine kadar geçerli)');
  });

  it('an accepted invitation is the answer of the invite step (#18)', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 4, savedSteps: [1, 2, 3] }, stepTwo));
    t.http.expectOne('/api/coaching/invitations/my-invitations').flush([{ id: 'i1', inviterEmail: 'ogretmen@example.test', message: null }]); await t.settle();
    await t.click('Kabul et');
    t.http.expectOne('/api/coaching/invitations/i1/accept').flush(null); await t.settle();
    await t.click('Devam');
    t.http.expectOne(`${base}/steps/4`).flush({ data: view({ status: 'InProgress', nextStep: 5, savedSteps: [1, 2, 3, 4] }, stepTwo) }); await t.settle();
    expect(t.el.querySelector('[data-question="invite"] .ob-me')!.textContent).toContain('Daveti kabul ettim: ogretmen@example.test');
  });

  // ---------- E4 review ----------
  it('a step the server refuses (400) asks its last question again with the message and no "Yeniden dene"', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 3, savedSteps: [1, 2] }, stepTwo));
    await t.click('Henüz karar vermedim');
    t.http.expectOne(`${base}/steps/target`).flush({ message: 'Bu hedef sınavına uymuyor.' }, { status: 400, statusText: 'Bad Request' }); await t.settle();
    const current = t.el.querySelector('[data-current]')!;
    expect(current.getAttribute('data-question')).toBe('targetMode');
    expect(current.querySelector('[role="alert"]')!.textContent).toContain('Bu hedef sınavına uymuyor.');
    expect(Array.from(t.el.querySelectorAll('button')).map(x => x.textContent?.trim())).not.toContain('Yeniden dene');
    await t.click('Henüz karar vermedim');
    t.http.expectOne(`${base}/steps/target`);
    expect(t.el.textContent).not.toContain('Bu hedef sınavına uymuyor.');
  });

  // F2 (E4 a): the server names the refused question ("field"); the wizard asks that one, not the step's last question.
  it('a 400 that names its question asks that question again', async () => {
    const t = await render(view({ status: 'InProgress', nextStep: 3, savedSteps: [1, 2] }, stepTwo));
    await t.click('Henüz karar vermedim');
    t.http.expectOne(`${base}/steps/target`).flush({ message: 'Önce hazırlandığın sınavı seç.', field: 'purpose' }, { status: 400, statusText: 'Bad Request' }); await t.settle();
    const current = t.el.querySelector('[data-current]')!;
    expect(current.getAttribute('data-question')).toBe('purpose');
    expect(current.querySelector('[role="alert"]')!.textContent).toContain('Önce hazırlandığın sınavı seç.');
    // An unknown name falls back to the step's last question.
    TestBed.resetTestingModule();
    const u = await render(view({ status: 'InProgress', nextStep: 3, savedSteps: [1, 2] }, stepTwo));
    await u.click('Henüz karar vermedim');
    u.http.expectOne(`${base}/steps/target`).flush({ message: 'Olmadı.', field: 'nope' }, { status: 400, statusText: 'Bad Request' }); await u.settle();
    expect(u.el.querySelector('[data-current]')!.getAttribute('data-question')).toBe('targetMode');
  });

  it('a resumed wizard keeps "Sonra bakarım" when invitations are still waiting and says when the parent code expired', async () => {
    vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date('2026-11-03T12:00:00Z'));
    const t = await render(view({ status: 'InProgress', nextStep: 7, savedSteps: [1, 2, 3, 4, 5, 6], acceptedInvitations: [] }, stepTwo), {}, {
      invitations: [{ id: 'i1', inviterEmail: 'ogretmen@example.test', message: null }],
      code: { expiresAt: '2026-11-01T09:00:00Z', createdAt: '2026-10-25T09:00:00Z', expired: true } });
    const summary = t.el.querySelector('.ob-summary')!.textContent!;
    expect(summary).toContain('Davet: Sonra bakarım');
    expect(summary).toContain('Veli bağlantısı: Veli kodumun süresi doldu (01.11.2026)');
    t.http.verify(); vi.useRealTimers();
  });

  it('the parent step offers a new code when the earlier one expired', async () => {
    vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date('2026-11-03T12:00:00Z'));
    const t = await render(view({ status: 'InProgress', nextStep: 5, savedSteps: [1, 2, 3, 4] }, stepTwo));
    t.http.expectOne('/api/users/me/parent-link-codes/active').flush({ expiresAt: '2026-11-01T09:00:00Z', createdAt: '2026-10-25T09:00:00Z', expired: true }); await t.settle();
    const current = t.el.querySelector('[data-current]')!;
    expect(current.textContent).toContain('Veli kodunun süresi doldu (01.11.2026)');
    expect(current.textContent).not.toContain('Aktif bir veli kodun var');
    expect(t.button('Veli kodu oluştur')).toBeTruthy();
    vi.useRealTimers();
  });

  // F2 (E4 c): a code whose expiry is long past is not mentioned.
  it('the parent step does not mention a code whose expiry is long past', async () => {
    vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date('2026-11-20T12:00:00Z'));
    const t = await render(view({ status: 'InProgress', nextStep: 5, savedSteps: [1, 2, 3, 4] }, stepTwo));
    t.http.expectOne('/api/users/me/parent-link-codes/active').flush({ expiresAt: '2026-11-01T09:00:00Z', createdAt: '2026-10-25T09:00:00Z', expired: true }); await t.settle();
    const current = t.el.querySelector('[data-current]')!;
    expect(current.querySelector('[data-testid="ob-expired-code"]')).toBeNull();
    expect(current.textContent).not.toContain('01.11.2026');
    expect(t.button('Veli kodu oluştur')).toBeTruthy();
    vi.useRealTimers();
  });
  // E7 (#30, live test 06.11.2026): "Hedefini belirle" and Hedefim show the current target first; "Değiştir" opens the same steps.
  describe('E7: the current target first', () => {
    const completed = { status: 'Completed' as const, nextStep: 7, savedSteps: [1, 2, 3, 4, 5, 6, 7], completedAt: '2026-11-06T00:00:00Z' };
    const yks = { ...stepTwo, educationStatus: 'Student' as const, gradeLevel: 12, purpose: 'Yks' as const, primaryScoreType: 'EA', targetExamSessionId: null, targetExamYear: 2027 };
    const program = { kind: 'program' as const, targetId: 'p-1', name: 'Ankara Üniversitesi · Hukuk', detail: 'Hukuk Fakültesi', minimumScore: 475.14192,
      scoreYear: 2025, scoreType: 'EA', scores: [], canEdit: true };
    const scores = { kind: 'score' as const, targetId: null, name: 'TYT ve Eşit Ağırlık', detail: null, minimumScore: null, scoreYear: null, scoreType: 'EA',
      scores: [{ scoreType: 'EA', score: 450 }, { scoreType: 'TYT', score: 400 }], canEdit: true };

    it('"Hedefini belirle" shows the saved programme with its taban in tr-TR before asking anything', async () => {
      const t = await render(view(completed, { ...yks, target: program }), { step: 'target' });
      const bubble = t.el.querySelector('[data-question="targetSaved"] .ob-me')!;
      expect(bubble.textContent).toContain('Ankara Üniversitesi · Hukuk · taban 475,14');
      expect(bubble.textContent).not.toContain('475.14');
      expect(t.el.querySelector('[data-question="targetMode"]')).toBeNull();
      expect(bubble.querySelector('button')!.textContent).toContain('Değiştir');
      t.http.expectNone(`${base}/steps/target`);
    });

    it('"Değiştir" opens the steps and "Vazgeç" keeps the saved target without saving', async () => {
      const t = await render(view(completed, { ...yks, target: program }), { step: 'target' });
      (t.el.querySelector('[data-question="targetSaved"] .ob-me button') as HTMLElement).click(); await t.settle();
      expect(t.el.querySelector('[data-current]')!.getAttribute('data-question')).toBe('targetMode');
      expect(t.button('Bölüm seçeceğim')).toBeTruthy();
      await t.click('Vazgeç');
      expect(t.el.querySelector('[data-question="targetSaved"] .ob-me')!.textContent).toContain('Ankara Üniversitesi · Hukuk');
      t.http.expectNone(`${base}/steps/target`);
      expect(t.navigate).not.toHaveBeenCalled();
    });

    it('shows both YKS score targets and saves both again from the score steps', async () => {
      const t = await render(view(completed, { ...yks, target: scores }), { step: 'target' });
      const bubble = t.el.querySelector('[data-question="targetSaved"] .ob-me')!;
      expect(bubble.textContent).toContain('Eşit Ağırlık 450');
      expect(bubble.textContent).toContain('TYT 400');
      (bubble.querySelector('button') as HTMLElement).click(); await t.settle();
      await t.click('Yalnız puan gireceğim');
      const number = () => t.el.querySelector('[data-current] input[type="number"]') as HTMLInputElement;
      number().value = '410'; number().dispatchEvent(new Event('input')); await t.click('Devam');
      number().value = '455,5'.replace(',', '.'); number().dispatchEvent(new Event('input')); await t.click('Devam');
      const save = t.http.expectOne(`${base}/steps/target`);
      expect(save.request.body).toEqual({ kind: 'score', targetId: null, scores: [{ scoreType: 'TYT', score: 410 }, { scoreType: 'EA', score: 455.5 }] });
    });

    it('embedded in Hedefim: no page header, the new target is shown after saving and Bugün is not opened', async () => {
      const t = await render(view(completed, { ...stepTwo, target: { ...program, kind: 'school', name: 'Ankara Fen Lisesi', scoreType: 'LGS', minimumScore: 480.5 } }), {}, {}, { embedded: true });
      expect(t.el.querySelector('h1')).toBeNull();
      expect(t.el.textContent).not.toContain('Çıkış yap');
      expect(t.el.querySelector('[data-question="targetSaved"] .ob-me')!.textContent).toContain('Ankara Fen Lisesi · taban 480,5');
      (t.el.querySelector('[data-question="targetSaved"] .ob-me button') as HTMLElement).click(); await t.settle();
      await t.click('Hedef puan gireceğim');
      const input = t.el.querySelector('[data-current] input[type="number"]') as HTMLInputElement;
      input.value = '460'; input.dispatchEvent(new Event('input')); await t.click('Devam');
      const save = t.http.expectOne(`${base}/steps/target`);
      expect(save.request.body).toEqual({ kind: 'score', targetId: null, scores: [{ scoreType: 'LGS', score: 460 }] });
      save.flush({ data: view(completed, { ...stepTwo, target: { kind: 'score', targetId: null, name: 'LGS', detail: null, minimumScore: null, scoreYear: null,
        scoreType: 'LGS', scores: [{ scoreType: 'LGS', score: 460 }], canEdit: true } }) }); await t.settle();
      expect(t.navigate).not.toHaveBeenCalled();
      expect(t.saved).toHaveBeenCalledTimes(1);
      expect(t.el.querySelector('[data-question="targetSaved"] .ob-me')!.textContent).toContain('LGS 460');
      expect(t.el.querySelector('[role="status"]')!.textContent).toContain('Hedefin kaydedildi');
    });

    it('embedded: a teacher\'s goal is shown locked with the note and no "Değiştir"', async () => {
      const t = await render(view(completed, { ...stepTwo, targetLocked: true, target: { ...program, kind: 'school', name: 'Ankara Fen Lisesi', canEdit: false } }), {}, {}, { embedded: true });
      const bubble = t.el.querySelector('[data-question="targetSaved"] .ob-me')!;
      expect(bubble.textContent).toContain('Hedefini öğretmenin belirledi: Ankara Fen Lisesi');
      expect(bubble.querySelector('button')).toBeNull();
      expect(t.el.textContent).toContain('Değiştirmek istersen öğretmenine yaz.');
      expect(t.el.textContent).not.toContain("Bugün'e dön");
    });
    // Review of PR 120 (item 1): the target branch never says "Önce sınavını seç" to a student who has an exam.
    async function changeToLgsScore(t: Awaited<ReturnType<typeof render>>, score = '460') {
      (t.el.querySelector('[data-question="targetSaved"] .ob-me button') as HTMLElement).click(); await t.settle();
      await t.click('Hedef puan gireceğim');
      const input = t.el.querySelector('[data-current] input[type="number"]') as HTMLInputElement;
      input.value = score; input.dispatchEvent(new Event('input')); await t.click('Devam');
    }
    const school = { ...program, kind: 'school' as const, name: 'Ankara Fen Lisesi', scoreType: 'LGS', minimumScore: 480.5 };

    it('while the new target is being saved it says "Kaydediliyor…", not "Önce sınavını seç"', async () => {
      const t = await render(view(completed, { ...stepTwo, target: school }), {}, {}, { embedded: true });
      await changeToLgsScore(t);
      const save = t.http.expectOne(`${base}/steps/target`);
      expect(t.el.textContent).toContain('Kaydediliyor…');
      expect(t.el.textContent).not.toContain('Önce sınavını seç');
      save.flush({ data: view(completed, { ...stepTwo, target: school }) }); await t.settle();
    });

    it('a failed save shows the error with "Yeniden dene" and "Vazgeç" next to it; "Vazgeç" brings the saved target back', async () => {
      const t = await render(view(completed, { ...stepTwo, target: school }), {}, {}, { embedded: true });
      await changeToLgsScore(t);
      t.http.expectOne(`${base}/steps/target`).flush({ message: 'Sunucu hatası.' }, { status: 500, statusText: 'Server Error' }); await t.settle();
      expect(t.el.textContent).not.toContain('Önce sınavını seç');
      const alert = t.el.querySelector('.ob-error[role="alert"]') as HTMLElement;
      expect(alert.textContent).toContain('Sunucu hatası.');
      const buttons = Array.from(alert.querySelectorAll('button')).map(x => x.textContent?.trim());
      expect(buttons).toEqual(['Yeniden dene', 'Vazgeç']);
      (Array.from(alert.querySelectorAll('button')).find(x => x.textContent?.trim() === 'Vazgeç') as HTMLElement).click(); await t.settle();
      expect(t.el.querySelector('[data-question="targetSaved"] .ob-me')!.textContent).toContain('Ankara Fen Lisesi');
      expect(t.el.querySelector('.ob-error')).toBeNull();
      t.http.expectNone(`${base}/steps/target`);
    });

    it('a teacher who sets the goal meanwhile (Onboarding.TeacherGoal) shows the locked goal clearly', async () => {
      const t = await render(view(completed, { ...stepTwo, target: school }), {}, {}, { embedded: true });
      await changeToLgsScore(t);
      t.http.expectOne(`${base}/steps/target`).flush({ success: false, code: 'Onboarding.TeacherGoal', message: 'Hedefini öğretmenin belirledi. Değiştirmek istersen öğretmenine yaz.' },
        { status: 409, statusText: 'Conflict' }); await t.settle();
      t.http.expectOne(base).flush({ data: view(completed, { ...stepTwo, targetLocked: true, target: { ...school, name: 'Kabataş Erkek Lisesi', canEdit: false } }) }); await t.settle();
      const bubble = t.el.querySelector('[data-question="targetSaved"] .ob-me')!;
      expect(bubble.textContent).toContain('Hedefini öğretmenin belirledi: Kabataş Erkek Lisesi');
      expect(bubble.querySelector('button')).toBeNull();
      expect(t.el.querySelector('.ob-summary')!.textContent).toContain('Değiştirmek istersen öğretmenine yaz.');
      expect(t.el.textContent).not.toContain('Önce sınavını seç');
      expect(t.el.textContent).not.toContain('Vazgeç');
    });

    it('after a refused answer (400) the question comes back with the message and "Vazgeç" still works', async () => {
      const t = await render(view(completed, { ...stepTwo, target: school }), {}, {}, { embedded: true });
      await changeToLgsScore(t);
      t.http.expectOne(`${base}/steps/target`).flush({ message: 'Bu puan bu sınava uymuyor.' }, { status: 400, statusText: 'Bad Request' }); await t.settle();
      expect(t.el.querySelector('[data-current]')!.getAttribute('data-question')).toBe('targetScore');
      expect(t.el.querySelector('[data-current] [role="alert"]')!.textContent).toContain('Bu puan bu sınava uymuyor.');
      await t.click('Vazgeç');
      expect(t.el.querySelector('[data-question="targetSaved"] .ob-me')!.textContent).toContain('Ankara Fen Lisesi');
      expect(t.el.textContent).not.toContain('Bu puan bu sınava uymuyor.');
      t.http.expectNone(`${base}/steps/target`);
    });

    // F2 review (M1): in target mode a field of another step (here "purpose") cannot be asked; the target question comes back.
    it('a 400 naming a question of another step re-asks the target question in target mode', async () => {
      const t = await render(view(completed, { ...stepTwo, target: school }), {}, {}, { embedded: true });
      await changeToLgsScore(t);
      t.http.expectOne(`${base}/steps/target`).flush({ message: 'Önce hazırlandığın sınavı seç.', field: 'purpose' }, { status: 400, statusText: 'Bad Request' }); await t.settle();
      const current = t.el.querySelector('[data-current]')!;
      expect(current).not.toBeNull();
      expect(current.getAttribute('data-question')).toBe('targetScore');
      expect(current.querySelector('[role="alert"]')!.textContent).toContain('Önce hazırlandığın sınavı seç.');
      expect(t.el.querySelector('[data-question="purpose"]')).toBeNull();
    });

    // F2 review: the settle loop of this test can pass the default 5s under load (no product race).
    it('"Henüz karar vermedim" asks before the saved target is removed', { timeout: 15000 }, async () => {
      const t = await render(view(completed, { ...stepTwo, target: school }), {}, {}, { embedded: true, confirm: false });
      (t.el.querySelector('[data-question="targetSaved"] .ob-me button') as HTMLElement).click(); await t.settle();
      await t.click('Henüz karar vermedim');
      expect(t.toaster.confirm).toHaveBeenCalledWith(expect.stringContaining('Mevcut hedefin silinecek'), expect.anything());
      t.http.expectNone(`${base}/steps/target`);
      expect(t.el.querySelector('[data-current]')!.getAttribute('data-question')).toBe('targetMode');
      t.toaster.confirm.mockResolvedValue(true);
      await t.click('Henüz karar vermedim');
      const save = t.http.expectOne(`${base}/steps/target`);
      expect(save.request.body).toEqual({ kind: 'none', targetId: null, scores: null });
      save.flush({ data: view(completed, stepTwo) }); await t.settle();
      expect(t.el.querySelector('[data-question="targetSaved"] .ob-me')!.textContent).toContain('Henüz karar vermedim');
    });

    it('embedded: no target after a reload looks as right after saving ("Henüz karar vermedim" with "Değiştir")', async () => {
      const t = await render(view(completed, stepTwo), {}, {}, { embedded: true });
      const bubble = t.el.querySelector('[data-question="targetSaved"] .ob-me')!;
      expect(bubble.textContent).toContain('Henüz karar vermedim');
      expect(bubble.querySelector('button')!.textContent).toContain('Değiştir');
      expect(t.el.querySelector('[data-question="targetMode"]')).toBeNull();
    });

    it('embedded before the first-login setup is finished: only a link to it', async () => {
      const t = await render(view({ status: 'InProgress', nextStep: 3, savedSteps: [1, 2] }, stepTwo), {}, {}, { embedded: true });
      expect(t.el.querySelector('[data-question]')).toBeNull();
      const link = t.el.querySelector('a[href="/coaching-portal/welcome"]')!;
      expect(link).toBeTruthy();
      expect(t.el.textContent).toContain('Önce hesap kurulumunu bitir');
      t.http.expectNone('/api/locations/provinces');
    });

    it('a started change counts as unsaved (Hedefim asks before leaving)', async () => {
      const t = await render(view(completed, { ...stepTwo, target: school }), {}, {}, { embedded: true });
      expect(t.fixture.componentInstance.dirty()).toBe(false);
      (t.el.querySelector('[data-question="targetSaved"] .ob-me button') as HTMLElement).click(); await t.settle();
      expect(t.fixture.componentInstance.dirty()).toBe(false);
      await t.click('Hedef puan gireceğim');
      expect(t.fixture.componentInstance.dirty()).toBe(true);
      await t.click('Vazgeç');
      expect(t.fixture.componentInstance.dirty()).toBe(false);
    });
  });
});
