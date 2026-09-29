import { TestBed } from '@angular/core/testing';
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

describe('CoachingInstitutionStudentReportComponent', () => {
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
      pageNumber: 1, pageSize: 25, fromDate: '2026-01-01T00:00:00.000Z',
      toDate: '2026-01-31T23:59:59.999Z', status: 'Submitted', search: 'matematik'
    });
    expect(component.historyTotalPages()).toBe(3);
    component.nextPage();
    expect(service.getStudentHistory).toHaveBeenLastCalledWith('student-1', 'Assignments', {
      pageNumber: 2, pageSize: 25, fromDate: '2026-01-01T00:00:00.000Z',
      toDate: '2026-01-31T23:59:59.999Z', status: 'Submitted', search: 'matematik'
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
