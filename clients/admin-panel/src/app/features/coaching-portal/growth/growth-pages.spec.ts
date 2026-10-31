import { Component, input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CoachingStudentExamOverviewService, StudentExamOverview } from '../../../core/services/coaching-student-exam-overview.service';
import { CoachingScoreEstimateService, ScoreEstimate } from '../../../core/services/coaching-score-estimate.service';
import { CoachingWeeklyReviewService } from '../../../core/services/coaching-weekly-review.service';
import { StudentExamResultsComponent } from '../pages/student-exam-results.component';
import { StudentGrowthExamsComponent } from './student-growth-exams.component';
import { StudentExamOverviewComponent } from './student-exam-overview.component';
import { StudentGoalSummaryComponent } from './student-goal-summary.component';
import { StudentWeekSummaryComponent } from './student-week-summary.component';
import { CoachingGrowthWeekService } from './coaching-growth-week.service';

@Component({ selector: 'app-student-exam-results', standalone: true, template: '<p>Deneme formu</p>' })
class FakeExamForm { readonly formOnly = input(false); readonly editId = input<string | null>(null); }

const overview: StudentExamOverview = {
  exams: [
    { examId: 'i', resultId: 'ri', source: 'Institution', title: 'Kurum TYT 1', examDate: '2026-10-28T09:00:00Z', examType: 'YKS', examCode: 'TYT', examName: 'TYT',
      net: 61.25, score: 300, maxScore: 500, sections: [], rank: 2, participants: 31, topPercent: 6.45 },
    { examId: 't', resultId: 'rt', source: 'Teacher', title: 'Öğretmen denemesi', examDate: '2026-10-21T09:00:00Z', examType: 'YKS', examCode: 'TYT', examName: 'TYT',
      net: 55.5, score: 280, maxScore: 500, sections: [], rank: null, participants: null, topPercent: null },
    { examId: 's', resultId: 'rs', source: 'Student', title: 'Kendi denemem', examDate: '2026-10-11T09:00:00Z', examType: 'Quiz', examCode: null, examName: null,
      net: null, score: 80, maxScore: 100, sections: [], rank: null, participants: null, topPercent: null }],
  trends: [{ examCode: 'TYT', examName: 'TYT', maxNet: 120, points: [
    { examId: 't', examDate: '2026-10-21T09:00:00Z', net: 55.5, source: 'Teacher' }, { examId: 'i', examDate: '2026-10-28T09:00:00Z', net: 61.25, source: 'Institution' }] }],
  subjects: [
    { examCode: 'TYT', sectionCode: 'TUR', name: 'Türkçe', questionCount: 40, averageNet: 30, successRatio: 0.75, examsUsed: 2, weakest: false },
    { examCode: 'TYT', sectionCode: 'TAR', name: 'Tarih', questionCount: 5, averageNet: 1.5, successRatio: 0.3, examsUsed: 2, weakest: true }]
};

describe('T6 Gelişimim', () => {
  function examsPage(data: StudentExamOverview | Error = overview) {
    const api = { get: vi.fn(() => data instanceof Error ? throwError(() => data) : of(data)) };
    TestBed.configureTestingModule({ imports: [StudentGrowthExamsComponent], providers: [provideRouter([]), { provide: CoachingStudentExamOverviewService, useValue: api }] });
    TestBed.overrideComponent(StudentGrowthExamsComponent, { remove: { imports: [StudentExamResultsComponent] }, add: { imports: [FakeExamForm] } });
    const fixture = TestBed.createComponent(StudentGrowthExamsComponent); fixture.detectChanges();
    return { fixture, api, el: fixture.nativeElement as HTMLElement };
  }

  it('Denemeler shows the net trend, the subjects with the weakest one marked, and one list with sources', () => {
    const { el, api } = examsPage();
    expect(api.get).toHaveBeenCalledWith(null);
    const chart = el.querySelector('svg[role="img"]')!;
    expect(chart.getAttribute('aria-label')).toContain('TYT');
    expect(el.querySelectorAll('[data-testid="trend-dot"]')).toHaveLength(2);
    expect(el.querySelector('[data-weakest="true"]')?.textContent).toContain('Tarih');
    const rows = [...el.querySelectorAll('[data-testid="exam-row"]')].map(x => x.textContent!.replace(/\s+/g, ' '));
    expect(rows[0]).toContain('Kurum'); expect(rows[0]).toContain('31 kişide 2.'); expect(rows[0]).toContain('61,25 net');
    expect(rows[1]).toContain('Öğretmen'); expect(rows[2]).toContain('Ben'); expect(rows[2]).toContain('Kısa sınav');
    expect(el.textContent).not.toContain('Quiz'); expect(el.textContent).not.toContain('Institution');
  });

  it('opens the entry form in a sheet from "+ Deneme ekle" and reloads after saving', () => {
    const { el, fixture, api } = examsPage();
    const add = [...el.querySelectorAll('button')].find(x => x.textContent?.includes('Deneme ekle'))!;
    add.click(); fixture.detectChanges();
    const sheet = el.querySelector('[role="dialog"]')!;
    expect(sheet.textContent).toContain('Deneme formu');
    fixture.componentInstance.saved(); fixture.detectChanges();
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  it('invites a first exam when there is none and reports a failed load', () => {
    const empty = examsPage({ exams: [], trends: [], subjects: [] });
    expect(empty.el.textContent).toContain('İlk denemeni ekle');
    TestBed.resetTestingModule();
    const failed = examsPage(new Error('down'));
    expect(failed.el.querySelector('[role="alert"]')?.textContent).toContain('Denemelerin yüklenemedi');
  });

  it('the parent view reuses the list read-only', () => {
    TestBed.configureTestingModule({ imports: [StudentExamOverviewComponent], providers: [{ provide: CoachingStudentExamOverviewService, useValue: { get: vi.fn(() => of(overview)) } }] });
    const fixture = TestBed.createComponent(StudentExamOverviewComponent);
    fixture.componentRef.setInput('studentId', 'child'); fixture.componentRef.setInput('readonly', true); fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(TestBed.inject(CoachingStudentExamOverviewService).get).toHaveBeenCalledWith('child');
    expect(el.querySelectorAll('[data-testid="exam-row"]')).toHaveLength(3);
    expect(el.querySelector('button.exam-row')).toBeNull();
    expect(el.textContent).not.toContain('Deneme ekle');
  });

  const estimate: ScoreEstimate = {
    estimates: [{ scoreType: 'SAY', name: 'SAY', modelYear: 2025, score: 380, scoreLow: 360, scoreHigh: 400, rank: 90000, rankBest: 80000, rankWorst: 100000,
      percentile: 12, examsUsed: 3, lastExamDate: '2026-10-20', includesDiplomaGrade: false }],
    target: { targetName: 'Ege Üniversitesi · Bilgisayar Mühendisliği', scoreType: 'SAY', targetScore: 440, targetRank: 30000, targetPercentile: 4,
      scoreGap: 60, netGap: 12.5, rankGap: 60000, reached: false }, notice: null };

  function goal(data: ScoreEstimate) {
    TestBed.configureTestingModule({ imports: [StudentGoalSummaryComponent], providers: [provideRouter([]), { provide: CoachingScoreEstimateService, useValue: { estimate: vi.fn(() => of(data)) } }] });
    const fixture = TestBed.createComponent(StudentGoalSummaryComponent); fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('Hedefim shows the target, a now → target gauge and the estimate cards', () => {
    const el = goal(estimate);
    expect(el.textContent).toContain('Ege Üniversitesi · Bilgisayar Mühendisliği');
    expect(el.textContent).toContain('Hedefe 12,5 net kaldı');
    const meters = el.querySelectorAll('[role="meter"]');
    expect(meters).toHaveLength(2);
    expect(meters[0].getAttribute('aria-valuetext')).toBe('Şu an 380, hedef 440');
    expect(el.querySelectorAll('[data-testid="estimate-card"]')).toHaveLength(1);
  });

  // V4 (01.11.2026): on the parent page the same views talk about the child by name.
  it('the parent goal view names the child instead of speaking to them', () => {
    TestBed.configureTestingModule({ imports: [StudentGoalSummaryComponent], providers: [provideRouter([]),
      { provide: CoachingScoreEstimateService, useValue: { estimate: vi.fn(() => of({ ...estimate, target: { ...estimate.target!, reached: true } })) } }] });
    const fixture = TestBed.createComponent(StudentGoalSummaryComponent);
    fixture.componentRef.setInput('studentId', 'child'); fixture.componentRef.setInput('readonly', true);
    fixture.componentRef.setInput('audience', 'parent'); fixture.componentRef.setInput('childName', 'Elif'); fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain("Elif'in hedefi"); expect(text).toContain('Puan tahmini');
    expect(text).not.toContain('Hedefin'); expect(text).not.toContain('Puan tahminin'); expect(text).not.toContain('puanın');
  });

  it('the parent goal view explains a missing target', () => {
    TestBed.configureTestingModule({ imports: [StudentGoalSummaryComponent], providers: [provideRouter([]),
      { provide: CoachingScoreEstimateService, useValue: { estimate: vi.fn(() => of({ estimates: [], target: null, notice: null })) } }] });
    const fixture = TestBed.createComponent(StudentGoalSummaryComponent);
    fixture.componentRef.setInput('studentId', 'child'); fixture.componentRef.setInput('readonly', true);
    fixture.componentRef.setInput('audience', 'parent'); fixture.componentRef.setInput('childName', 'Can'); fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Can henüz bir hedef seçmedi');
  });

  it('the parent exam view names the child in the trend, subjects and sources', () => {
    TestBed.configureTestingModule({ imports: [StudentExamOverviewComponent], providers: [{ provide: CoachingStudentExamOverviewService, useValue: { get: vi.fn(() => of(overview)) } }] });
    const fixture = TestBed.createComponent(StudentExamOverviewComponent);
    fixture.componentRef.setInput('studentId', 'child'); fixture.componentRef.setInput('readonly', true);
    fixture.componentRef.setInput('audience', 'parent'); fixture.componentRef.setInput('childName', 'Elif'); fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const text = el.textContent!;
    expect(text).toContain("Elif'in net gidişatı"); expect(text).toContain('Dersleri');
    expect(text).not.toContain('Net gidişatın'); expect(text).not.toContain('Derslerin'); expect(text).not.toContain('zorlandığın');
    expect(text).not.toContain('İlk denemene');
    const rows = [...el.querySelectorAll('[data-testid="exam-row"]')].map(x => x.textContent!.replace(/\s+/g, ' '));
    expect(rows[2]).toContain('Elif'); expect(rows[2]).not.toMatch(/(^|\s)Ben(\s|$)/);
    expect(el.querySelector('svg[role="img"]')!.getAttribute('aria-label')).not.toMatch(/çıktın|indin/);
  });

  it('Hedefim explains a target without taban or rank (K4)', () => {
    const el = goal({ ...estimate, target: { ...estimate.target!, targetScore: null, targetRank: null, scoreGap: null, netGap: null, rankGap: null } });
    expect(el.querySelectorAll('[role="meter"]')).toHaveLength(0);
    expect(el.textContent).toContain('taban puanı ya da sıralaması yok');
  });

  it('Haftam shows weekly minutes and tasks, open decisions and badge progress', () => {
    const weeks = { weeks: vi.fn(() => of([{ label: '13 Eki', minutes: 240, tasks: 4, scheduled: 6 }, { label: 'Bu hafta', minutes: 90, tasks: 1, scheduled: 5 }])),
      motivation: vi.fn(() => of({ studentId: 'me', currentStreak: 2, longestStreak: 4, activeToday: true, lastActiveDay: null, timeZoneId: 'Europe/Istanbul', celebration: null,
        badges: [{ code: 'five-exams', label: '5 deneme', description: '', earnedOn: null, progress: 3, target: 5 }] })) };
    const review = { home: vi.fn(() => of({ weekStart: '2026-10-26', currentWeekStart: '2026-10-26', needsReflection: true, openDecisions: 1, overdueDecisions: 0,
      decisions: [{ id: 'd', weekStart: '2026-10-19', title: 'Paragraf sorularına 20 dk ayır', dueOn: '2026-11-02', isOverdue: false }] })) };
    TestBed.configureTestingModule({ imports: [StudentWeekSummaryComponent], providers: [provideRouter([]),
      { provide: CoachingGrowthWeekService, useValue: weeks }, { provide: CoachingWeeklyReviewService, useValue: review }] });
    const fixture = TestBed.createComponent(StudentWeekSummaryComponent); fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('[data-testid="week-bar"]')).toHaveLength(2);
    expect(el.textContent).toContain('4 sa');
    expect(el.textContent).toContain('Paragraf sorularına 20 dk ayır');
    expect(el.textContent).toContain('Bu haftayı değerlendir');
    expect(el.textContent).toContain('5 deneme: 3/5');
  });
});
