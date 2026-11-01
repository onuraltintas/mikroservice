import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingManagementService } from '../../../core/services/coaching-management.service';
import { CoachingStudentExamOverviewService } from '../../../core/services/coaching-student-exam-overview.service';
import { CoachingScoreEstimateService } from '../../../core/services/coaching-score-estimate.service';
import { CoachingParentService } from './coaching-parent.service';
import { ParentChildComponent } from './parent-child.component';
import { ParentCoachingTabComponent } from './parent-coaching-tab.component';
import { ParentPaymentsComponent } from './parent-payments.component';
import { ParentSpeedReadingTabComponent } from './parent-speed-reading-tab.component';

const parentWeek = {
  weekStart: '2026-10-26', planned: 14, completed: 9,
  overdue: [{ title: 'Paragraf 40 soru', date: '2026-10-28', kind: 'Study' }],
  nextSession: { startsAt: '2026-11-03T15:00:00Z', title: 'Haftalık görüşme', teacherName: 'Mehmet Kaya' },
  examCountdown: { name: 'YKS 2027', date: '2027-06-19', daysLeft: 230 },
  attention: [{ kind: 'overdue', text: '2 görev gecikti' }]
};
const readingOverview = {
  studentId: 'child-1', lastActivityAt: '2026-10-31T18:00:00Z',
  program: { name: 'Hızlı okuma temel', level: 2, currentDay: 12, totalDays: 28, completedDays: 11, completedTasks: 33, totalTasks: 84, status: 'Active' },
  thisWeek: { minutes: 95, activeDays: 4, exercisesCompleted: 12 },
  weeks: ['2026-09-07', '2026-09-14', '2026-09-21', '2026-09-28', '2026-10-05', '2026-10-12', '2026-10-19', '2026-10-26']
    .map((weekStart, i) => ({ weekStart, minutes: 20 + i * 10, activeDays: 2 + (i % 4) })),
  reading: { latestWpm: 312, firstWpm: 210, latestComprehension: 80, trend: [
    { date: '2026-10-01', wpm: 210, comprehension: 70 }, { date: '2026-10-12', wpm: 260, comprehension: 75 }, { date: '2026-10-29', wpm: 312, comprehension: 80 }] },
  gamification: { level: 4, xp: 1240, streakDays: 5, badgesEarned: 3, recentBadges: [{ name: 'İlk hafta', earnedAt: '2026-10-08T10:00:00Z' }] },
  recentResults: [{ date: '2026-10-29', exerciseType: 'Reading', exerciseName: 'Anlama testi', score: 80, wpm: 312, comprehension: 80 }]
};

function parentServiceMock(overrides: Record<string, unknown> = {}) {
  return {
    getLinks: vi.fn(() => of([])),
    getCoachingWeek: vi.fn(() => of(parentWeek)),
    getAdherence: vi.fn(() => of([{ weekStart: '2026-10-26', planned: 14, completed: 9, minutes: 320 }])),
    getWeeklyReview: vi.fn(() => of(null)),
    getAssignments: vi.fn(() => of({ items: [], totalCount: 0 })),
    getSessions: vi.fn(() => of({ items: [], totalCount: 0 })),
    getFees: vi.fn(() => of({ balance: 0, overdueAmount: 0, agreements: [] })),
    getAccess: vi.fn(() => of({ hasAccess: true, enforcementEnabled: true, planName: 'Bireysel', status: 'Active', accessUntil: '2026-12-01T00:00:00Z',
      subscriptions: [{ id: 'sub-1', status: 'Active', endDate: '2026-12-01T00:00:00Z', plan: { name: 'Bireysel aylık' } }] })),
    getTransfers: vi.fn(() => of([])),
    sendTransfer: vi.fn(),
    getSpeedReadingOverview: vi.fn(() => of(readingOverview)),
    ...overrides
  };
}

function parentProviders(service: object) {
  return [provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
    { provide: CoachingParentService, useValue: service },
    { provide: CoachingStudentExamOverviewService, useValue: { get: vi.fn(() => of({ trends: [], subjects: [], exams: [] })) } },
    { provide: CoachingScoreEstimateService, useValue: { estimate: vi.fn(() => of({ estimates: [], target: null, notice: null })) } },
    { provide: CoachingManagementService, useValue: {
      getPublicSubscriptionPlans: () => of([{ id: 'plan-1', name: 'Bireysel yıllık', audience: 'Individual', price: 1200, isContactOnly: false, isActive: true, isPublic: true, durationDays: 365, billingPeriod: 'Annual', features: [] }]),
      getPublicBankTransferSettings: () => of({ currency: 'TRY', accountHolder: 'Edu İvme', bankName: 'Banka', iban: 'TR…', paymentInstructions: null }) } },
    { provide: ToasterService, useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } }];
}

// V4 (01.11.2026): the child page shows a tab per product the child shares; one product → no tab strip.
describe('ParentChildComponent', () => {
  const both = [{ product: 'Coaching', studentHasAccess: true, visibleToParent: true }, { product: 'SpeedReading', studentHasAccess: true, visibleToParent: true }];
  const link = (products: unknown) => ({ id: 'link-1', studentUserId: 'child-1', studentName: 'Elif Yılmaz', status: 'Verified', relationship: 'Mother', products });

  function setup(service: object, query: Record<string, string> = {}, id = 'child-1') {
    TestBed.configureTestingModule({ imports: [ParentChildComponent], providers: [...parentProviders(service),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ studentId: id }), queryParamMap: convertToParamMap(query) } } }] });
    const fixture = TestBed.createComponent(ParentChildComponent); fixture.detectChanges(); return fixture;
  }

  it('shows tabs for both products and opens Koçluk first', () => {
    const fixture = setup(parentServiceMock({ getLinks: vi.fn(() => of([link(both)])) }));
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('h1')?.textContent).toContain('Elif Yılmaz');
    const tabs = [...root.querySelectorAll('[role=tab]')].map(t => t.textContent?.trim());
    expect(tabs).toEqual(['Koçluk', 'Hızlı okuma']);
    expect(root.querySelector('app-parent-coaching-tab')).toBeTruthy();
    expect(root.querySelector('app-parent-speed-reading-tab')).toBeNull();
  });

  it('opens the tab named in the address and writes the choice back', async () => {
    const fixture = setup(parentServiceMock({ getLinks: vi.fn(() => of([link(both)])) }), { urun: 'hizli-okuma' });
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('app-parent-speed-reading-tab')).toBeTruthy();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    ([...root.querySelectorAll('[role=tab]')][0] as HTMLButtonElement).click();
    fixture.detectChanges(); await fixture.whenStable();
    expect(root.querySelector('app-parent-coaching-tab')).toBeTruthy();
    expect(navigate).toHaveBeenCalledWith([], expect.objectContaining({ queryParams: { urun: 'kocluk' }, replaceUrl: true }));
  });

  it('shows a single product without a tab strip', () => {
    const fixture = setup(parentServiceMock({ getLinks: vi.fn(() => of([link([
      { product: 'Coaching', studentHasAccess: false, visibleToParent: true }, { product: 'SpeedReading', studentHasAccess: true, visibleToParent: true }])])) }),
      { urun: 'kocluk' });
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('[role=tablist]')).toBeNull();
    expect(root.querySelector('app-parent-speed-reading-tab')).toBeTruthy();
    expect(root.querySelector('app-parent-coaching-tab')).toBeNull();
    expect(root.querySelector('edu-coaching-checkout')).toBeNull();
  });

  it('explains a child who shares nothing and shows no payment form', () => {
    const fixture = setup(parentServiceMock({ getLinks: vi.fn(() => of([link([
      { product: 'Coaching', studentHasAccess: true, visibleToParent: false }, { product: 'SpeedReading', studentHasAccess: true, visibleToParent: false }])])) }));
    const root = fixture.nativeElement as HTMLElement;
    expect(root.textContent).toContain('paylaşmayı kapattı');
    expect(root.querySelector('app-parent-coaching-tab')).toBeNull();
    expect(root.querySelector('edu-coaching-checkout')).toBeNull();
  });

  it('does not show a child the parent is not linked to', () => {
    const fixture = setup(parentServiceMock({ getLinks: vi.fn(() => of([])) }), {}, 'child-9');
    expect(fixture.nativeElement.textContent).toContain('Bu öğrenciyle doğrulanmış bir veli bağlantın yok');
    expect(fixture.nativeElement.querySelector('app-parent-coaching-tab')).toBeNull();
  });

  it('prints the child page from the browser (O13)', () => {
    const fixture = setup(parentServiceMock({ getLinks: vi.fn(() => of([link(both)])) }));
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    (fixture.nativeElement.querySelector('[data-testid="print-child"]') as HTMLButtonElement).click();
    expect(print).toHaveBeenCalled(); print.mockRestore();
  });
});

// V4: Koçluk tab — this week, attention, 8-week adherence, goal and exams in the parent's voice, paged lists, review notes.
describe('ParentCoachingTabComponent', () => {
  function setup(service: object, exams?: object) {
    TestBed.configureTestingModule({ imports: [ParentCoachingTabComponent], providers: [...parentProviders(service),
      ...(exams ? [{ provide: CoachingStudentExamOverviewService, useValue: { get: vi.fn(() => of(exams)) } }] : [])] });
    const fixture = TestBed.createComponent(ParentCoachingTabComponent);
    fixture.componentRef.setInput('studentId', 'child-1'); fixture.componentRef.setInput('childName', 'Elif Yılmaz');
    fixture.detectChanges(); return fixture;
  }
  const assignment = (n: number) => ({ id: `a-${n}`, title: `Ödev ${n}`, dueDate: '2026-11-04T15:00:00Z', status: 'Graded', score: 85, maxScore: 100,
    teacherFeedback: n === 1 ? 'Çok iyi, paragrafa devam.' : undefined, isOverdue: false });

  it('shows this week, what needs attention and the next session', () => {
    const fixture = setup(parentServiceMock());
    const week = fixture.nativeElement.querySelector('[data-testid="parent-week"]') as HTMLElement;
    expect(week.querySelector('cs-progress-ring')).toBeTruthy();
    expect(week.textContent).toContain('9'); expect(week.textContent).toContain('14');
    expect(week.textContent).toContain('Paragraf 40 soru');
    expect(week.textContent).toContain('Haftalık görüşme'); expect(week.textContent).toContain('Mehmet Kaya');
    expect(week.textContent).toContain('YKS 2027'); expect(week.textContent).toContain('230 gün');
    expect(fixture.nativeElement.querySelector('[data-testid="parent-attention"]')?.textContent).toContain('2 görev gecikti');
  });

  it('draws the last weeks of plan adherence as labelled bars', () => {
    const weeks = ['2026-09-07', '2026-09-14', '2026-09-21', '2026-09-28', '2026-10-05', '2026-10-12', '2026-10-19', '2026-10-26'];
    const fixture = setup(parentServiceMock({ getAdherence: vi.fn(() => of(weeks.map((weekStart, i) => ({ weekStart, planned: 10, completed: i, minutes: 60 })))) }));
    const section = fixture.nativeElement.querySelector('[data-testid="adherence"]') as HTMLElement;
    expect(section.querySelectorAll('li').length).toBe(8);
    expect(section.textContent).toContain('7/10');
  });

  it('talks about the child, not to the child, in the goal and exam views', () => {
    const fixture = setup(parentServiceMock(), { trends: [{ examCode: 'TYT', examName: 'TYT', maxNet: 120, points: [
      { examId: 'e1', examDate: '2026-10-01T06:00:00Z', net: 60, source: 'Student' }, { examId: 'e2', examDate: '2026-10-20T06:00:00Z', net: 70, source: 'Teacher' }] }],
      subjects: [{ examCode: 'TYT', sectionCode: 'MAT', name: 'Matematik', averageNet: 20, questionCount: 40, successRatio: .5, examsUsed: 2, weakest: true }],
      exams: [{ examId: 'e1', resultId: 'r1', source: 'Student', title: 'TYT 1', examDate: '2026-10-01T06:00:00Z', examType: 'Mock', examCode: 'TYT', examName: 'TYT',
        net: 60, score: 0, maxScore: 0, sections: [], rank: null, participants: null, topPercent: null }] });
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Hedefin'); expect(text).not.toContain('Derslerin'); expect(text).not.toContain('Net gidişatın');
    expect(text).not.toMatch(/(^|\s)Ben(\s|$)/); expect(text).not.toContain('zorlandığın');
    expect(text).toContain("Elif'in");
    expect(text).not.toContain('kayıtlı sonuç');
  });

  it('lists assignments with grade and feedback and loads more on request', () => {
    const service = parentServiceMock({ getAssignments: vi.fn((_: string, page = 1) => of(page === 1
      ? { items: Array.from({ length: 10 }, (_, i) => assignment(i + 1)), totalCount: 12 }
      : { items: [assignment(11), assignment(12)], totalCount: 12 })) });
    const fixture = setup(service);
    const section = fixture.nativeElement.querySelector('[data-testid="parent-assignments"]') as HTMLElement;
    expect(section.textContent).toContain('85/100'); expect(section.textContent).toContain('Çok iyi, paragrafa devam.');
    const more = section.querySelector('[data-testid="more-assignments"]') as HTMLButtonElement;
    expect(more.textContent).toContain('Daha fazla');
    more.click(); fixture.detectChanges();
    expect(service.getAssignments).toHaveBeenLastCalledWith('child-1', 2);
    expect(section.textContent).toContain('Ödev 12');
    expect(section.querySelector('[data-testid="more-assignments"]')).toBeNull();
  });

  it('shows session attendance for the child', () => {
    const fixture = setup(parentServiceMock({ getSessions: vi.fn(() => of({ items: [{ id: 's-1', subject: 'Haftalık görüşme', startTime: '2026-10-28T15:00:00Z', status: 'Completed', type: 'OneOnOne',
      studentReflections: [{ studentId: 'child-1', note: null, attendanceStatus: 'Present' }] }], totalCount: 1 })) }));
    const section = fixture.nativeElement.querySelector('[data-testid="parent-sessions"]') as HTMLElement;
    expect(section.textContent).toContain('Haftalık görüşme'); expect(section.textContent).toContain('Katıldı');
    expect(section.querySelector('[data-testid="more-sessions"]')).toBeNull();
  });

  it('shows the teacher weekly review read-only', () => {
    const fixture = setup(parentServiceMock({ getWeeklyReview: vi.fn(() => of({ weekStart: '2026-10-26', reviews: [{ note: 'Matematikte düzenli, fizikte tekrar gerekli.', reviewedAt: '2026-10-31T10:00:00Z' }],
      decisions: [{ title: 'Elektrik konusunu tekrar et', note: '', dueOn: '2026-11-05', status: 'Open', isOverdue: false }] })) }));
    const section = fixture.nativeElement.querySelector('[data-testid="parent-review"]') as HTMLElement;
    expect(section.textContent).toContain('Matematikte düzenli'); expect(section.textContent).toContain('Elektrik konusunu tekrar et');
    expect(section.querySelector('textarea, input')).toBeNull();
  });

  it('keeps the other sections when one read fails', () => {
    const fixture = setup(parentServiceMock({ getCoachingWeek: vi.fn(() => throwError(() => ({ status: 500 }))) }));
    expect(fixture.nativeElement.textContent).toContain('Bu haftanın özeti şu an yüklenemedi');
    expect(fixture.nativeElement.querySelector('[data-testid="adherence"]')).toBeTruthy();
  });
});

// V4: payments — the subscription status, and the payment form only when the child needs one.
describe('ParentPaymentsComponent', () => {
  function setup(service: object) {
    TestBed.configureTestingModule({ imports: [ParentPaymentsComponent], providers: parentProviders(service) });
    const fixture = TestBed.createComponent(ParentPaymentsComponent);
    fixture.componentRef.setInput('studentId', 'child-1'); fixture.componentRef.setInput('childName', 'Elif Yılmaz');
    fixture.detectChanges(); return fixture;
  }
  const noSubscription = (enforcementEnabled: boolean) =>
    vi.fn(() => of({ hasAccess: !enforcementEnabled, enforcementEnabled, planName: null, status: null, accessUntil: null, subscriptions: [] }));

  it('shows an active subscription without a payment form', () => {
    const fixture = setup(parentServiceMock());
    expect(fixture.nativeElement.textContent).toContain('Bireysel aylık');
    expect(fixture.nativeElement.querySelector('edu-coaching-checkout')).toBeNull();
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
  });

  it('offers the payment form when the child needs a subscription', () => {
    const fixture = setup(parentServiceMock({ getAccess: noSubscription(true) }));
    expect(fixture.nativeElement.textContent).toContain('Elif');
    expect(fixture.nativeElement.querySelector('edu-coaching-checkout')).toBeTruthy();
  });

  it('never shows internal wording and no form when no subscription is needed', () => {
    const fixture = setup(parentServiceMock({ getAccess: noSubscription(false) }));
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('zorunluluğu'); expect(text).not.toContain('etkin değil');
    expect(fixture.nativeElement.querySelector('edu-coaching-checkout')).toBeNull();
  });

  it('waits for a notice under review instead of asking again', () => {
    const fixture = setup(parentServiceMock({ getAccess: noSubscription(true),
      getTransfers: vi.fn(() => of([{ id: 't-1', status: 'Pending', paymentReference: 'EFT-1', plan: { name: 'Bireysel yıllık' }, discountAmount: 0, currency: 'TRY' }])) }));
    expect(fixture.nativeElement.textContent).toContain('inceleniyor');
    expect(fixture.nativeElement.querySelector('edu-coaching-checkout')).toBeNull();
  });

  it('shows fees with the overdue amount', () => {
    const fixture = setup(parentServiceMock({ getFees: vi.fn(() => of({ balance: 6000, overdueAmount: 1500, agreements: [{ id: 'f1', title: 'YKS paketi', totalAmount: 18000 }] })) }));
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('YKS paketi'); expect(text).toContain('1.500');
  });

  it('sends a transfer notice only with the adult payer declaration and the contract', async () => {
    const service = parentServiceMock({ getAccess: noSubscription(true),
      sendTransfer: vi.fn(() => of({ id: 'transfer-1', status: 'Pending', paymentReference: 'EFT-VELI-1', plan: { name: 'Bireysel yıllık' } })) });
    const fixture = setup(service); const component = fixture.componentInstance;
    component.planId = 'plan-1'; component.paymentReference = ' EFT-VELI-1 '; component.payerName = 'Ayşe Veli';
    await component.sendTransfer();
    expect(service.sendTransfer).not.toHaveBeenCalled(); expect(component.error()).toContain('18 yaş');
    component.adultPayerDeclaration = true; await component.sendTransfer();
    expect(service.sendTransfer).not.toHaveBeenCalled(); expect(component.error()).toContain('mesafeli satış sözleşmesini');
    component.checkout = { couponCode: 'VELI-10', acceptSalesContract: true }; await component.sendTransfer();
    expect(service.sendTransfer).toHaveBeenCalledWith('child-1', { planId: 'plan-1', paymentReference: 'EFT-VELI-1', payerName: 'Ayşe Veli', note: null,
      adultPayerDeclaration: true, couponCode: 'VELI-10', acceptSalesContract: true });
    expect(component.transfers()[0].id).toBe('transfer-1');
  });

  it('lists what the parent paid (O10)', () => {
    const fixture = setup(parentServiceMock());
    expect(fixture.nativeElement.textContent).toContain('Veli olarak yaptığın ödemeler');
  });
});

// V4: Hızlı okuma tab — program, reading speed and comprehension, this week, streak and recent results.
describe('ParentSpeedReadingTabComponent', () => {
  function setup(service: object) {
    TestBed.configureTestingModule({ imports: [ParentSpeedReadingTabComponent], providers: parentProviders(service) });
    const fixture = TestBed.createComponent(ParentSpeedReadingTabComponent);
    fixture.componentRef.setInput('studentId', 'child-1'); fixture.componentRef.setInput('childName', 'Elif Yılmaz');
    fixture.detectChanges(); return fixture;
  }

  it('shows the program, speed, comprehension, this week and achievements', () => {
    const fixture = setup(parentServiceMock());
    const root = fixture.nativeElement as HTMLElement;
    const text = root.textContent as string;
    expect(text).toContain('12/28'); expect(text).toContain('Hızlı okuma temel'); expect(text).toContain('33/84');
    expect(text).toContain('312'); expect(text).toContain('%80');
    expect(text).toContain('95 dk'); expect(text).toContain('4 gün');
    expect(text).toContain('1.240'); expect(text).toContain('İlk hafta'); expect(text).toContain('5 gün');
    expect(text).toContain('Anlama testi');
    expect(root.querySelector('[data-testid="reading-trend"] svg[role=img]')).toBeTruthy();
    expect(root.querySelectorAll('[data-testid="reading-trend"] table tbody tr').length).toBe(3);
    expect(root.querySelectorAll('[data-testid="reading-weeks"] li').length).toBe(8);
  });

  it('uses the parent voice when there is nothing yet', () => {
    const fixture = setup(parentServiceMock({ getSpeedReadingOverview: vi.fn(() => of({ ...readingOverview, program: null,
      reading: { latestWpm: null, firstWpm: null, latestComprehension: null, trend: [] }, recentResults: [],
      gamification: { level: 1, xp: 0, streakDays: 0, badgesEarned: 0, recentBadges: [] } })) }));
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Elif henüz bir hızlı okuma programına başlamadı');
    expect(text).toContain('ölçülen');
    expect(text).not.toContain('başlamadın'); expect(text).not.toContain('okuman ');
  });

  it('explains when the overview cannot be read', () => {
    const fixture = setup(parentServiceMock({ getSpeedReadingOverview: vi.fn(() => throwError(() => ({ status: 500 }))) }));
    expect(fixture.nativeElement.textContent).toContain('Hızlı okuma ilerlemesi şu an görüntülenemiyor');
  });

  // V2 final shape (PR #88): 403 ParentReadForbidden, nullable gamification, lowercase program status, 1-decimal comprehension.
  it('says the child stopped sharing when the read is forbidden', () => {
    const fixture = setup(parentServiceMock({ getSpeedReadingOverview: vi.fn(() => throwError(() => ({ status: 403, error: { code: 'ParentReadForbidden', message: 'x' } }))) }));
    expect(fixture.nativeElement.textContent).toContain('Elif hızlı okuma ilerlemesini şu an seninle paylaşmıyor');
  });

  it('shows no zeros when there is no XP or badge yet, and formats comprehension and status', () => {
    const fixture = setup(parentServiceMock({ getSpeedReadingOverview: vi.fn(() => of({ ...readingOverview, gamification: null,
      program: { ...readingOverview.program, status: 'completed' },
      reading: { ...readingOverview.reading, latestComprehension: 80.5 },
      recentResults: [{ date: '2026-10-29', exerciseType: 'Okuma', exerciseName: 'Kutup ayıları', score: null, wpm: 305, comprehension: 72.5 }] })) }));
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Henüz XP ya da rozet yok'); expect(text).not.toContain('Puan (XP)');
    expect(text).toContain('%80,5'); expect(text).toContain('%72,5'); expect(text).toContain('Tamamlandı');
    expect(text).toContain('Kutup ayıları'); expect(text).toContain('Okuma');
  });
});
