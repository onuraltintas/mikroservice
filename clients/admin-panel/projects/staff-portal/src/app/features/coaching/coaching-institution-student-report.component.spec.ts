import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import {
  CoachingInstitutionHistoryFilter,
  CoachingInstitutionHistoryType,
  CoachingInstitutionStudent,
  CoachingInstitutionStudentDetail,
  CoachingInstitutionStudentHistoryItem,
  CoachingInstitutionService
} from './coaching-institution.service';
import { CoachingInstitutionStudentReportComponent } from './coaching-institution-student-report.component';
import { CoachingScoreEstimateService } from '../../../../../../src/app/core/services/coaching-score-estimate.service';

describe('CoachingInstitutionStudentReportComponent', () => {
  it('opens the same scoped session briefing read-only with a refreshed institution summary', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = reportService(); const fixture = createFixture(service); fixture.detectChanges();
    (await fixture.componentInstance.openSessionBrief()); fixture.detectChanges();
    // The brief opens on the weekly work tab, whose weekly review is a lazily loaded `@defer` block: wait for it so the
    // test does not depend on whether an earlier spec on the same Vitest worker already loaded that chunk.
    await fixture.whenStable(); fixture.detectChanges();
    expect(service.getStudentDetail).toHaveBeenCalledTimes(2);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url === '/api/coaching/staff/students/student-1/study/weekly-review').flush({ data: { weekStart: '2026-10-19', today: '2026-10-20',
      reflection: null, summary: { scheduledTasks: 0, completedTasks: 0, plannedMinutes: 0, actualMinutes: null }, decisions: [], decisionPage: 1, totalDecisions: 0,
      canReflect: false, canDecide: false } });
    http.match(r => r.url.endsWith('/study/practice-progress')).forEach(r=>r.flush({data:{fromDate:'2026-10-01',toDate:'2026-10-20',trends:[],goals:[],recovery:[]}}));
    const reports = http.match(r => r.url === '/api/coaching/staff/students/student-1/study/report'); expect(reports.length).toBe(2);
    for (const report of reports) report.flush({ success: true, data: { fromDate: report.request.params.get('fromDate'), toDate: report.request.params.get('toDate'),
      source: 'StudentReported', reason: 'NoScheduledTasks', scheduledTasks: 0, completedTasks: 0, plannedMinutes: 0, actualMinutes: null, completionPercentage: null, topics: [] } });
    fixture.detectChanges(); http.expectNone(r => r.url.includes('/study/plans') || r.url.includes('/study/availability')); http.verify();
    expect(fixture.nativeElement.textContent).toContain('Seans hazırlık özeti'); expect(fixture.nativeElement.textContent).not.toContain('Yeni taslak');
    (await fixture.componentInstance.openSessionBrief()); fixture.detectChanges();
    expect(service.getStudentDetail).toHaveBeenCalledTimes(3);
    const refreshed = http.match(r => r.url === '/api/coaching/staff/students/student-1/study/report');
    expect(refreshed.length).toBe(2);
    for (const report of refreshed) report.flush({ success: true, data: { fromDate: report.request.params.get('fromDate'), toDate: report.request.params.get('toDate'),
      source: 'StudentReported', reason: 'NoScheduledTasks', scheduledTasks: 0, completedTasks: 0, plannedMinutes: 0, actualMinutes: null, completionPercentage: null, topics: [] } });
    http.verify();
  });
  it('Roadmap F1: shows the student score and rank estimate on request', () => {
    const estimate = vi.fn(() => of({ estimates: [{ scoreType: 'TYT', name: 'TYT', modelYear: 2026, score: 301.25, scoreLow: 290, scoreHigh: 310, rank: 410000,
      rankBest: 380000, rankWorst: 440000, percentile: null, examsUsed: 2, lastExamDate: '2026-10-20T00:00:00Z', includesDiplomaGrade: false }], target: null, notice: null }));
    TestBed.configureTestingModule({
      imports: [CoachingInstitutionStudentReportComponent],
      providers: [{ provide: CoachingInstitutionService, useValue: reportService() }, { provide: CoachingScoreEstimateService, useValue: { estimate } }]
    });
    const fixture = TestBed.createComponent(CoachingInstitutionStudentReportComponent);
    fixture.componentRef.setInput('student', student());
    fixture.detectChanges();
    expect(estimate).not.toHaveBeenCalled();
    (fixture.nativeElement.querySelector('[data-testid="toggle-score-estimate"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(estimate).toHaveBeenCalledWith('student-1', null);
    expect((fixture.nativeElement.querySelector('[data-testid="score-estimate"]') as HTMLElement).textContent).toContain('301,25');
  });

  it('loads the student summary and the first page of assignment history', () => {
    const service = reportService();
    const fixture = createFixture(service);
    fixture.detectChanges();

    expect(service.getStudentDetail).toHaveBeenCalledWith('student-1');
    expect(service.getStudentHistory).toHaveBeenCalledWith('student-1', 'Assignments', { pageNumber: 1, pageSize: 25 });
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('4');
    expect(fixture.nativeElement.textContent).toContain('Geçmiş kayıtlar');
  });

  it('Roadmap O13: prints the report from the browser', () => {
    const fixture = createFixture(reportService()); fixture.detectChanges();
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    (fixture.nativeElement.querySelector('[data-testid="print-student-report"]') as HTMLButtonElement).click();
    expect(print).toHaveBeenCalled(); print.mockRestore();
  });

  it('switches record types and requests full history for the selected student', () => {
    const service = reportService({ history: [historyItem('goal-1', 'Goals')] });
    const fixture = createFixture(service);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-history-type="Goals"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(service.getStudentHistory).toHaveBeenLastCalledWith('student-1', 'Goals', { pageNumber: 1, pageSize: 25 });
    expect(fixture.nativeElement.textContent).toContain('Haftalık çalışma');
  });

  it('applies date, status, and search filters and pages through results', () => {
    const service = reportService();
    service.getStudentHistory.mockReturnValue(of({ items: [historyItem('assignment-2', 'Assignments')], totalCount: 51 }));
    const fixture = createFixture(service);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.historyFromDate = '2026-01-01';
    component.historyToDate = '2026-01-31';
    component.historyStatus = 'Submitted';
    component.historySearch = '  matematik  ';
    component.applyFilters();

    expect(service.getStudentHistory).toHaveBeenLastCalledWith('student-1', 'Assignments', {
      pageNumber: 1, pageSize: 25, fromDate: new Date(2026, 0, 1).toISOString(),
      toDate: new Date(new Date(2026, 1, 1).getTime() - 1).toISOString(), status: 'Submitted', search: 'matematik'
    });
    expect(component.historyTotalPages()).toBe(3);
    component.nextPage();
    expect(service.getStudentHistory).toHaveBeenLastCalledWith('student-1', 'Assignments', {
      pageNumber: 2, pageSize: 25, fromDate: new Date(2026, 0, 1).toISOString(),
      toDate: new Date(new Date(2026, 1, 1).getTime() - 1).toISOString(), status: 'Submitted', search: 'matematik'
    });
  });

  it('rejects an invalid date range without making a history request', () => {
    const service = reportService();
    const fixture = createFixture(service);
    fixture.detectChanges();
    const initialCalls = service.getStudentHistory.mock.calls.length;
    fixture.componentInstance.historyFromDate = '2026-02-01';
    fixture.componentInstance.historyToDate = '2026-01-31';
    fixture.componentInstance.applyFilters();

    expect(service.getStudentHistory).toHaveBeenCalledTimes(initialCalls);
    expect(fixture.componentInstance.historyError()).toContain('Başlangıç tarihi');
  });

  it('keeps summary and history failures recoverable independently', () => {
    const service = reportService();
    service.getStudentDetail.mockReturnValue(throwError(() => new Error('offline')));
    service.getStudentHistory.mockReturnValue(throwError(() => new Error('offline')));
    const fixture = createFixture(service);
    fixture.detectChanges();
    expect(fixture.componentInstance.detailError()).toContain('özet');
    expect(fixture.componentInstance.historyError()).toContain('geçmişi');

    service.getStudentDetail.mockReturnValue(of(detail()));
    service.getStudentHistory.mockReturnValue(of({ items: [], totalCount: 0 }));
    fixture.componentInstance.retryDetail();
    fixture.componentInstance.retryHistory();
    expect(fixture.componentInstance.detailError()).toBeNull();
    expect(fixture.componentInstance.historyError()).toBeNull();
  });

  it('emits a request to return to the institution roster', () => {
    const fixture = createFixture(reportService());
    fixture.detectChanges();
    const back = vi.fn();
    fixture.componentInstance.back.subscribe(back);
    (fixture.nativeElement.querySelector('[data-testid="back-to-roster"]') as HTMLButtonElement).click();
    expect(back).toHaveBeenCalledOnce();
  });
});

function createFixture(service = reportService()) {
  TestBed.configureTestingModule({
    imports: [CoachingInstitutionStudentReportComponent],
    providers: [{ provide: CoachingInstitutionService, useValue: service }]
  });
  const fixture = TestBed.createComponent(CoachingInstitutionStudentReportComponent);
  fixture.componentRef.setInput('student', student());
  return fixture;
}

function reportService(options: { history?: CoachingInstitutionStudentHistoryItem[] } = {}) {
  return {
    getStudentDetail: vi.fn(() => of(detail())),
    getStudentHistory: vi.fn((_studentId: string, _type: CoachingInstitutionHistoryType, _filter: CoachingInstitutionHistoryFilter) =>
      of({ items: options.history ?? [historyItem('assignment-1', 'Assignments')], totalCount: options.history?.length ?? 1 }))
  };
}

function student(): CoachingInstitutionStudent {
  return { userId: 'student-1', firstName: 'Ada', lastName: 'Yılmaz', email: 'ada@example.test', gradeLevel: 8, teacherName: 'Ayşe Demir' };
}

function detail(): CoachingInstitutionStudentDetail {
  return {
    studentId: 'student-1', totalAssignments: 4, submittedAssignments: 3, totalExams: 2,
    totalSessions: 5, totalGoals: 1, assignments: [], exams: []
  };
}

function historyItem(id: string, type: CoachingInstitutionHistoryType): CoachingInstitutionStudentHistoryItem {
  return {
    id, type, title: type === 'Goals' ? 'Haftalık çalışma' : 'Matematik tekrar ödevi',
    eventDate: '2026-01-20T10:00:00Z', status: type === 'Goals' ? 'InProgress' : 'Submitted', progress: 50
  };
}

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
