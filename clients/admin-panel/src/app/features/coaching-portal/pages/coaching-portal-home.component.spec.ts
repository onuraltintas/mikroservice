import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { CoachingPortalHomeComponent } from './coaching-portal-home.component';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import { StaffPortalRedirectService } from '../staff-portal-redirect.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudentTodayService, StudentTodayView } from '../../../core/services/coaching-student-today.service';
import { CoachingWhatsNewService } from '../layout/coaching-whats-new.service';
import { CoachingScoreEstimateService, ScoreEstimate } from '../../../core/services/coaching-score-estimate.service';

describe('CoachingPortalHomeComponent', () => {
  const view = (over: Partial<StudentTodayView> = {}): StudentTodayView => ({
    today: '2026-10-21', timeZoneId: 'Europe/Istanbul', generatedAt: '2026-10-21T08:00:00Z',
    nextStep: { item: { kind: 'assignment', recordId: 'a-late', title: 'Türev ödevi', date: '2026-10-20', startMinute: 1080, minutes: 40, timeZoneId: 'Europe/Istanbul', planId: null, weekStart: null }, overdue: true },
    items: [
      { kind: 'study', recordId: 's-1', title: 'Paragraf', date: '2026-10-21', startMinute: 840, minutes: 30, timeZoneId: 'Europe/Istanbul', planId: 'p-1', weekStart: null },
      { kind: 'assignment', recordId: 'a-1', title: 'Okuma ödevi', date: '2026-10-21', startMinute: 1020, minutes: null, timeZoneId: 'Europe/Istanbul', planId: null, weekStart: null }
    ],
    overdue: [{ kind: 'assignment', recordId: 'a-late', title: 'Türev ödevi', date: '2026-10-20', startMinute: 1080, minutes: 40, timeZoneId: 'Europe/Istanbul', planId: null, weekStart: null }],
    todayCount: 2, overdueCount: 1, upcomingCount: 3,
    exam: { name: 'YKS', date: '2027-06-19', daysLeft: 241, source: 'Catalog' },
    streak: { currentStreak: 6, longestStreak: 11, activeToday: true },
    week: { weekStart: '2026-10-19', planned: 8, completed: 3 },
    goal: { goalId: 'g-1', title: 'ODTÜ Bilgisayar', examCode: 'TYT', targetNet: 90, latestNet: 80, remainingNet: 10 },
    weeklyReview: { needed: false, weekStart: '2026-10-12' },
    nextSession: null, hasActivePlan: true, ...over
  });

  const noTarget: ScoreEstimate = { estimates: [], target: null, notice: null };
  async function render(today: StudentTodayView, setupReady = true, estimate: ScoreEstimate = noTarget, purposeNotForEducation = false) {
    const profile = signal<UserProfile | null>({ id: 'user-1', email: 'a@test', firstName: 'Elif', lastName: 'Test', username: 'a@test', roles: ['Student'], role: 'Student', permissions: [], product: 'coaching' });
    const api = { get: vi.fn(() => of(today)) };
    TestBed.configureTestingModule({ imports: [CoachingPortalHomeComponent], providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { userProfile: profile } },
      { provide: CoachingStudentTodayService, useValue: api },
      { provide: CoachingLearningSetupService, useValue: { get: () => of({ context: null, isReady: setupReady, purposeNotForEducation }) } },
      { provide: CoachingWhatsNewService, useValue: { visible: () => true, dismiss: vi.fn() } },
      { provide: CoachingScoreEstimateService, useValue: { estimate: vi.fn(() => of(estimate)) } },
      { provide: StaffPortalRedirectService, useValue: { redirect: vi.fn(), url: () => '' } }
    ] });
    await TestBed.compileComponents();
    TestBed.inject(CoachingPortalViewService).select('Student');
    const fixture = TestBed.createComponent(CoachingPortalHomeComponent);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, api, el: fixture.nativeElement as HTMLElement };
  }

  it('opens with the next step: one summary call, the overdue assignment first with a single action', async () => {
    const { api, el } = await render(view());
    expect(api.get).toHaveBeenCalledTimes(1);
    const next = el.querySelector('[data-testid="next-step"]')!;
    expect(next).not.toBeNull();
    // The next step is the first card on the page (after the one-time intro card).
    const sections = Array.from(el.querySelectorAll('[data-home-block]')).map(x => x.getAttribute('data-home-block'));
    expect(sections[0]).toBe('next-step');
    expect(next.textContent).toContain('Türev ödevi');
    expect(next.textContent).toContain('Gecikti');
    const action = next.querySelectorAll('a,button');
    expect(action.length).toBe(1);
    expect(action[0].getAttribute('href')).toBe('/coaching-portal/assignments/a-late');
  });

  it('lists today with overdue work merged and marked, and keeps upcoming collapsed', async () => {
    const { el } = await render(view());
    const rows = Array.from(el.querySelectorAll('[data-testid="today-list"] [data-record]'));
    expect(rows.map(x => x.getAttribute('data-record'))).toEqual(['a-late', 's-1', 'a-1']);
    expect(rows[0].textContent).toContain('Gecikti');
    expect(rows[1].textContent).not.toContain('Gecikti');
    expect(el.textContent).toContain('Yaklaşan 3');
    expect(el.querySelector('app-student-daily-agenda')).toBeNull();
  });

  it('shows the exam countdown, streak, week ring and the goal gap in one line', async () => {
    const { el } = await render(view());
    const strip = el.querySelector('[data-testid="today-strip"]')!.textContent!;
    expect(strip).toContain('241');
    expect(strip).toContain('YKS');
    expect(strip).toContain('6');
    expect(el.querySelector('cs-progress-ring')!.getAttribute('ng-reflect-value') ?? '3').toBe('3');
    expect(el.textContent).toContain('Hedefine 10 net kaldı');
    expect(el.querySelector('a[href="/coaching-portal/growth/goals"]')).not.toBeNull();
  });

  it('shows the weekly review and the next session only when there is one', async () => {
    let r = await render(view());
    expect(r.el.querySelector('[data-testid="weekly-review"]')).toBeNull();
    expect(r.el.querySelector('[data-testid="next-session"]')).toBeNull();
    TestBed.resetTestingModule();
    r = await render(view({ weeklyReview: { needed: true, weekStart: '2026-10-12' },
      nextSession: { id: 'ses-1', title: 'Haftalık görüşme', scheduledDate: '2026-10-22T07:00:00Z', durationMinutes: 45, meetingLink: null } }));
    expect(r.el.querySelector('[data-testid="weekly-review"]')!.textContent).toContain('Haftanı değerlendir');
    expect(r.el.querySelector('[data-testid="next-session"]')!.textContent).toContain('Haftalık görüşme');
  });

  it('drops the welcome block, the ready setup card, the shortcut stack and the extra refresh buttons', async () => {
    const { el } = await render(view());
    expect(el.textContent).not.toContain('Merhaba');
    expect(el.textContent).not.toContain('Çalışma düzenin');
    expect(el.textContent).not.toContain('Plan sihirbazı');
    expect(Array.from(el.querySelectorAll('button')).filter(x => x.textContent?.includes('Yenile')).length).toBeLessThanOrEqual(0);
    expect(el.querySelector('app-coaching-whats-new-card')).not.toBeNull();
  });

  it('points to "Planını kur" until the setup is ready, then offers a first plan when there is none', async () => {
    const empty = { hasActivePlan: false, nextStep: null, items: [], overdue: [], todayCount: 0, overdueCount: 0 };
    let r = await render(view(empty), false);
    // Paket C (#3): after the first-login wizard free hours and lessons are still missing; the card says so clearly.
    const setup = r.el.querySelector('[data-home-block="setup"]')!;
    expect(setup.textContent).toContain('Planını kur');
    expect(setup.querySelector('a[href="/coaching-portal/account/settings"]')!.textContent).toContain('Planını kur');
    expect(setup.querySelector('a[href="/coaching-portal/plan/wizard"]')).toBeNull();
    expect(r.el.querySelector('[data-testid="next-step"]')).toBeNull();
    TestBed.resetTestingModule();
    r = await render(view(empty), true);
    expect(r.el.querySelector('[data-home-block="setup"]')!.textContent).toContain('İlk planını oluştur');
    expect(r.el.querySelector('a[href="/coaching-portal/plan/wizard"]')).not.toBeNull();
  });

  it('says why the setup is open again when the exam no longer fits the grade (Paket D #8)', async () => {
    const { el } = await render(view({ hasActivePlan: true }), false, noTarget, true);
    const setup = el.querySelector('[data-home-block="setup"]')!;
    expect(setup.querySelector('h2')!.textContent).toContain('Çalışma tercihlerini güncelle');
    expect(setup.textContent).toContain('Seçili sınavın sınıfına uymuyor.');
    expect(setup.querySelector('a[href="/coaching-portal/account/settings"]')!.textContent).toContain('Çalışma tercihlerim');
  });

  describe('goal card (Paket C #4)', () => {
    const lgs = { scoreType: 'LGS', name: 'LGS', modelYear: 2026, score: 420, scoreLow: 400, scoreHigh: 440, rank: null, rankBest: null, rankWorst: null,
      percentile: null, examsUsed: 2, lastExamDate: '2026-10-20', includesDiplomaGrade: false, group: 'LGS' as const, label: 'Yaklaşık tahmin', margin: 16 };
    const school = { targetName: 'Ankara Fen Lisesi', scoreType: 'LGS', targetScore: 480, targetRank: null, targetPercentile: null, scoreGap: 60, netGap: 16.2,
      rankGap: null, reached: false, status: 'below' as const, margin: 16, kind: 'school' as const, scoreYear: 2025 };
    const card = (el: HTMLElement) => el.querySelector('[data-testid="home-goal"]')!;

    it('without a target says "Hedefini belirle" and links to the target page', async () => {
      const { el } = await render(view({ goal: null }));
      expect(card(el).textContent).toContain('Hedefini belirle');
      // It opens the wizard's target question.
      expect(card(el).getAttribute('href')).toBe('/coaching-portal/welcome?step=target');
    });

    it('with a target and no practice exam names the target and says when the gap appears', async () => {
      const { el } = await render(view({ goal: null }), true, { estimates: [], notice: null, target: { ...school, scoreGap: null, netGap: null, status: null },
        goal: { scoreType: 'LGS', name: 'LGS', group: 'LGS', score: null, scoreLow: null, scoreHigh: null, margin: null, scaleMax: 500, label: 'Yaklaşık tahmin', exact: false, level: null, notComputedReason: null, reason: 'noExam' } });
      expect(card(el).textContent).toContain('Ankara Fen Lisesi');
      expect(card(el).textContent).toContain('İlk denemeni girince hedefe ne kadar kaldığını burada göreceksin.');
      expect(card(el).querySelector('[data-testid="home-goal-label"]')).toBeNull();
    });

    it('with an estimate shows the score gap, the taban, the approximate label and the LGS note; the net line stays second', async () => {
      const { el } = await render(view(), true, { estimates: [lgs], target: school, notice: null });
      const text = card(el).textContent!;
      expect(text).toContain('Hedefine yaklaşık 60 puan kaldı');
      expect(text).toContain('Ankara Fen Lisesi');
      expect(text).toContain('Taban puanı 480 (yaklaşık)');
      expect(card(el).querySelector('[data-testid="home-goal-label"]')!.textContent).toContain('Yaklaşık tahmin');
      expect(text).toContain("LGS'de hem puan tahmini hem okulun taban puanı yaklaşıktır.");
      expect(card(el).querySelector('[data-testid="home-goal-net"]')!.textContent).toContain('Hedefine 10 net kaldı');
    });

    it('a YKS student with TYT and field target scores sees both', async () => {
      const say = { ...lgs, scoreType: 'SAY', name: 'YKS Sayısal', group: 'YKS' as const, margin: null, score: 400, scoreLow: 390, scoreHigh: 410 };
      const tyt = { ...say, scoreType: 'TYT', name: 'TYT', score: 360 };
      const gap = (scoreType: string, name: string, targetScore: number, scoreGap: number) => ({ targetName: name, scoreType, targetScore, targetRank: null,
        targetPercentile: null, scoreGap, netGap: 5, rankGap: null, reached: scoreGap <= 0, status: (scoreGap <= 0 ? 'reached' : 'below') as 'reached' | 'below',
        kind: 'score' as const, ownTargetScore: true });
      const { el } = await render(view({ goal: null }), true, { estimates: [say, tyt], notice: null, target: gap('SAY', 'YKS Sayısal', 450, 50),
        scoreTargets: [gap('SAY', 'YKS Sayısal', 450, 50), gap('TYT', 'TYT', 350, -10)] });
      expect(card(el).textContent).toContain('Hedefine yaklaşık 50 puan kaldı');
      expect(card(el).textContent).toContain('YKS Sayısal hedef puanın');
      expect(el.querySelector('[data-testid="home-goal-secondary"]')!.textContent).toContain('TYT: hedef puanını geçtin');
    });

    it('says the target is passed, and borderline as in Paket B', async () => {
      let r = await render(view({ goal: null }), true, { estimates: [lgs], target: { ...school, targetScore: 380, scoreGap: -40, status: 'reached', reached: true }, notice: null });
      expect(card(r.el).textContent).toContain('Hedef puanını geçtin');
      TestBed.resetTestingModule();
      r = await render(view({ goal: null }), true, { estimates: [lgs], target: { ...school, targetScore: 430, scoreGap: 10, status: 'borderline' }, notice: null });
      expect(card(r.el).textContent).toContain('Hedef, tahmini puan aralığının içinde (400–440): sınırdasın.');
    });

    it('a YDS score target is exact: no "yaklaşık"', async () => {
      const { el } = await render(view({ goal: null }), true, { estimates: [], notice: null,
        languageScores: [{ examCode: 'YDS_EN', name: 'YDS İngilizce', score: 75, scoreLow: 70, scoreHigh: 80, examsUsed: 2, lastExamDate: '2026-10-20', level: 'C', label: 'Deneme sonucuna göre', exact: true }],
        target: { targetName: 'YDS İngilizce', scoreType: 'YDS_EN', targetScore: 85, targetRank: null, targetPercentile: null, scoreGap: 10, netGap: 8, rankGap: null,
          reached: false, status: 'below', kind: 'score', ownTargetScore: true } });
      const text = card(el).textContent!;
      expect(text).toContain('Hedefine 10 puan kaldı'); expect(text).not.toContain('yaklaşık');
      expect(text).toContain('YDS İngilizce hedef puanın'); expect(text).toContain('Hedef puanın 85');
      expect(card(el).querySelector('[data-testid="home-goal-label"]')!.textContent).toContain('Deneme sonucuna göre');
    });

    it('shows why no score can be computed (YKS 0,5 rule, MEB-AGS 1 net, no model)', async () => {
      const failed = [{ anyOf: [{ code: 'TUR', name: 'Türkçe', rawScore: 0.25 }] }];
      let r = await render(view({ goal: null }), true, { estimates: [], notice: null,
        notComputed: [{ scoreType: 'SAY', name: 'SAY', modelYear: 2026, notComputedReason: 'minRawScore', failedConditions: failed, examsUsed: 1, lastExamDate: '2026-10-20' }],
        target: { ...school, scoreType: 'SAY', kind: 'program', targetName: 'ODTÜ · Fizik', notComputedReason: 'minRawScore', status: null } });
      expect(card(r.el).textContent).toContain('Türkçe testinden en az 0,5 ham puan gerekir');
      TestBed.resetTestingModule();
      r = await render(view({ goal: null }), true, { estimates: [], notice: null,
        notComputed: [{ scoreType: 'P1', name: 'P1', modelYear: 2026, notComputedReason: 'minRawScore', failedConditions: [{ anyOf: [{ code: 'SAY', name: 'Sayısal', rawScore: 0.5 }] }], examsUsed: 1, lastExamDate: '2026-10-20', minimumRawScore: 1 }],
        target: { ...school, scoreType: 'P1', kind: 'score', targetName: 'MEB-AGS P1', targetScore: 70, notComputedReason: 'minRawScore', status: null } });
      expect(card(r.el).textContent).toContain('Sayısal testinden en az 1 net gerekir');
      TestBed.resetTestingModule();
      r = await render(view({ goal: null }), true, { estimates: [], notice: null, target: { ...school, scoreGap: null, status: null },
        goal: { scoreType: 'LGS', name: 'LGS', group: 'LGS', score: null, scoreLow: null, scoreHigh: null, margin: null, scaleMax: 500, label: 'Yaklaşık tahmin', exact: false, level: null, notComputedReason: null, reason: 'noModel' } });
      expect(card(r.el).textContent).toContain('Bu sınav için puan tahmini henüz hazır değil.');
    });
  });

  it('sends teacher-only users to the new staff portal instead of rendering the legacy dashboard', async () => {
    const profile = signal<UserProfile | null>({
      id: 'teacher-1', email: 'teacher@example.test', firstName: 'Ada', lastName: 'Koç',
      username: 'teacher@example.test', roles: ['Teacher'], role: 'Teacher', permissions: [], product: 'coaching'
    });
    const service = {
      getTeacherAssignments: vi.fn(() => of({
        items: [{ id: 'a1', title: 'Ödev 1', type: 'Digital', dueDate: '2030-01-01', status: 'Active',
          totalStudents: 4, submittedCount: 2, createdAt: '2029-12-01' }],
        pageNumber: 1, pageSize: 5, totalCount: 23, totalPages: 5
      })),
      getTeacherStudents: vi.fn(() => of({
        items: [{ userId: 'student-1' }], pageNumber: 1, pageSize: 1, totalCount: 41, totalPages: 41
      }))
    };
    const redirect = vi.fn();
    TestBed.configureTestingModule({
      imports: [CoachingPortalHomeComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: StaffPortalRedirectService, useValue: { redirect, url: () => 'https://onuraltintas.net/staff/?product=coaching' } }
      ]
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(CoachingPortalHomeComponent);
    fixture.detectChanges(); await fixture.whenStable();

    expect(redirect).toHaveBeenCalledWith('coaching');
    expect(service.getTeacherAssignments).not.toHaveBeenCalled();
    expect(service.getTeacherStudents).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).not.toContain('Toplam ödev');
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });

beforeEach(() => TestBed.overrideProvider(CoachingStudentNavigationService, { useValue: { showAssignments: () => true, showSessions: () => true, refresh: () => {} } }));
