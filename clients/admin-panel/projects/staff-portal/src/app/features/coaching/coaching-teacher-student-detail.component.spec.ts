import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingTeacherStudentDetailComponent } from './coaching-teacher-student-detail.component';
import { CoachingTeacherStudent } from './coaching-teacher-students.service';
import * as reportExport from '../../../../../../src/app/features/coaching-portal/coaching-history-export';

describe('CoachingTeacherStudentDetailComponent', () => {
  let http: HttpTestingController;
  const student: CoachingTeacherStudent = {
    userId: 'student-1',
    firstName: 'Ayşe',
    lastName: 'Yılmaz',
    fullName: 'Ayşe Yılmaz',
    gradeLevel: 8,
    institutionName: 'Atatürk Ortaokulu',
    assignmentStartDate: '2026-09-01T00:00:00Z'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoachingTeacherStudentDetailComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());
  afterEach(() => vi.restoreAllMocks());

  function createDetail() {
    const fixture = TestBed.createComponent(CoachingTeacherStudentDetailComponent);
    fixture.componentRef.setInput('student', student);
    fixture.detectChanges();
    return fixture;
  }

  function flushInitialReport() {
    http.expectOne('/api/reports/student/student-1/progress').flush({
      studentId: 'student-1',
      totalAssignments: 8,
      submittedAssignments: 6,
      gradedAssignments: 4,
      averageAssignmentPercentage: 82,
      totalExams: 3,
      averageExamPercentage: 76,
      totalGoals: 2,
      completedGoals: 1,
      averageGoalProgress: 65,
      totalSessions: 5,
      upcomingSessions: 1,
      attendedSessions: 4,
      attendancePercentage: 80
    });
    http.expectOne('/api/reports/student/student-1/history?pageNumber=1&pageSize=10&type=Assignments').flush({
      items: [{
        id: 'assignment-1',
        type: 'Assignments',
        title: 'Kesirler çalışma kağıdı',
        eventDate: '2026-09-10T10:00:00Z',
        status: 'Graded',
        score: 82,
        maxScore: 100
      }],
      pageNumber: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1
    });
  }

  it('shows the student summary and coaching history, including earlier institution records', () => {
    const fixture = createDetail();
    flushInitialReport();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('6 / 8 teslim');
    expect(fixture.nativeElement.textContent).toContain('Kesirler çalışma kağıdı');
    expect(fixture.nativeElement.textContent).toContain('önceki kurumlarında oluşan Koçluk kayıtları da kapsanır');
  });

  it('filters a category and paginates its history', () => {
    const fixture = createDetail();
    flushInitialReport();
    fixture.detectChanges();

    const examTab = fixture.nativeElement.querySelector('button[role="tab"][data-history-type="Exams"]') as HTMLButtonElement;
    examTab.click();
    const examRequest = http.expectOne(request =>
      request.url === '/api/reports/student/student-1/history'
        && request.params.get('type') === 'Exams'
        && request.params.get('pageNumber') === '1');
    examRequest.flush({ items: [], pageNumber: 1, pageSize: 10, totalCount: 21, totalPages: 3 });
    fixture.detectChanges();

    fixture.componentInstance.historySearch = '  Fen sınavı  ';
    fixture.componentInstance.historyStatus = 'Result';
    fixture.componentInstance.historyFromDate = '2026-09-01';
    fixture.componentInstance.applyFilters();

    const filteredRequest = http.expectOne(request =>
      request.url === '/api/reports/student/student-1/history'
        && request.params.get('type') === 'Exams'
        && request.params.get('pageNumber') === '1'
        && request.params.get('search') === 'Fen sınavı'
        && request.params.get('status') === 'Result'
        && request.params.get('fromDate') === '2026-09-01T00:00:00.000Z');
    filteredRequest.flush({ items: [], pageNumber: 1, pageSize: 10, totalCount: 21, totalPages: 3 });
    fixture.detectChanges();

    fixture.componentInstance.nextPage();
    const pageRequest = http.expectOne(request =>
      request.url === '/api/reports/student/student-1/history'
        && request.params.get('type') === 'Exams'
        && request.params.get('pageNumber') === '2'
        && request.params.get('search') === 'Fen sınavı');
    expect(pageRequest.request.method).toBe('GET');
    pageRequest.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 21, totalPages: 3 });
  });

  it('allows retrying a failed history request without losing the student summary', () => {
    const fixture = createDetail();
    http.expectOne('/api/reports/student/student-1/progress').flush({
      studentId: 'student-1', totalAssignments: 0, submittedAssignments: 0, gradedAssignments: 0,
      totalExams: 0, totalGoals: 0, completedGoals: 0, averageGoalProgress: 0,
      totalSessions: 0, upcomingSessions: 0, attendedSessions: 0
    });
    http.expectOne('/api/reports/student/student-1/history?pageNumber=1&pageSize=10&type=Assignments').flush({}, {
      status: 500,
      statusText: 'Server Error'
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('0 / 0 teslim');
    expect(fixture.nativeElement.textContent).toContain('Ödevler geçmişi yüklenemedi');
    const retry = fixture.nativeElement.querySelector('button[data-testid="retry-history"]') as HTMLButtonElement;
    retry.click();
    http.expectOne('/api/reports/student/student-1/history?pageNumber=1&pageSize=10&type=Assignments').flush({
      items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 1
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Bu kategoride kayıt bulunmuyor');
  });

  it('distinguishes an expired session from a forbidden student report', () => {
    const fixture = createDetail();
    http.expectOne('/api/reports/student/student-1/progress').flush({}, {
      status: 401,
      statusText: 'Unauthorized'
    });
    http.expectOne('/api/reports/student/student-1/history?pageNumber=1&pageSize=10&type=Assignments').flush({}, {
      status: 403,
      statusText: 'Forbidden'
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Oturum süreniz sona erdi');
    expect(fixture.nativeElement.textContent).toContain('Bu öğrenci raporunu görüntüleme yetkiniz bulunmuyor');
  });

  it('exports all filtered history pages before offering a CSV download', async () => {
    const fixture = createDetail();
    flushInitialReport();
    fixture.componentInstance.historySearch = '  Fen  ';
    fixture.componentInstance.historyStatus = 'Result';
    fixture.componentInstance.historyFromDate = '2026-09-01';
    const download = vi.spyOn(reportExport, 'downloadCoachingHistoryCsv').mockImplementation(() => undefined);

    const exportPromise = fixture.componentInstance.exportHistory();
    const request = http.expectOne(candidate =>
      candidate.url === '/api/reports/student/student-1/history'
        && candidate.params.get('pageNumber') === '1'
        && candidate.params.get('pageSize') === '100'
        && candidate.params.get('type') === 'Assignments'
        && candidate.params.get('search') === 'Fen'
        && candidate.params.get('status') === 'Result'
        && candidate.params.get('fromDate') === '2026-09-01T00:00:00.000Z');
    request.flush({
      items: [
        { id: 'assignment-2', type: 'Assignments', title: 'Fen çalışma', eventDate: '2026-09-11T10:00:00Z', status: 'Graded', score: 90, maxScore: 100 },
        { id: 'assignment-3', type: 'Assignments', title: 'Fen tekrar', eventDate: '2026-09-12T10:00:00Z', status: 'Graded', score: 80, maxScore: 100 }
      ],
      pageNumber: 1, pageSize: 100, totalCount: 2, totalPages: 1
    });
    await exportPromise;

    expect(download).toHaveBeenCalledOnce();
    expect(fixture.componentInstance.historyError()).toBeNull();
    expect(fixture.componentInstance.isExporting()).toBe(false);
  });

  it('does not download a partial CSV when a later report page fails', async () => {
    const fixture = createDetail();
    flushInitialReport();
    const download = vi.spyOn(reportExport, 'downloadCoachingHistoryCsv').mockImplementation(() => undefined);

    const exportPromise = fixture.componentInstance.exportHistory();
    http.expectOne(request => request.url.endsWith('/history') && request.params.get('pageNumber') === '1')
      .flush({ items: [{ id: 'assignment-2', type: 'Assignments', title: 'Ödev', eventDate: '2026-09-11T10:00:00Z', status: 'Graded' }], pageNumber: 1, pageSize: 100, totalCount: 2, totalPages: 1 });
    http.expectOne(request => request.url.endsWith('/history') && request.params.get('pageNumber') === '2')
      .flush({}, { status: 500, statusText: 'Server Error' });
    await exportPromise;

    expect(download).not.toHaveBeenCalled();
    expect(fixture.componentInstance.historyError()).toContain('Tam rapor indirilemedi');
    expect(fixture.componentInstance.isExporting()).toBe(false);
  });

  it('prints the complete history into a safely rendered print document', async () => {
    const fixture = createDetail();
    flushInitialReport();
    const printDocument = window.document.implementation.createHTMLDocument('Student report');
    const printWindow = {
      document: printDocument,
      opener: null,
      focus: vi.fn(),
      print: vi.fn()
    } as unknown as Window;
    vi.spyOn(window, 'open').mockReturnValue(printWindow);

    const printPromise = fixture.componentInstance.printHistory();
    http.expectOne(request => request.url.endsWith('/history') && request.params.get('pageSize') === '100').flush({
      items: [{
        id: 'assignment-2', type: 'Assignments', title: '<img src=x onerror=alert(1)>',
        eventDate: '2026-09-11T10:00:00Z', status: 'Graded'
      }],
      pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1
    });
    await printPromise;

    expect(printDocument.querySelector('img')).toBeNull();
    expect(printDocument.body.textContent).toContain('<img src=x onerror=alert(1)>');
    expect(printWindow.print).toHaveBeenCalledOnce();
  });

  it('emits a return action to the student roster', () => {
    const fixture = createDetail();
    http.expectOne('/api/reports/student/student-1/progress').flush({
      studentId: 'student-1', totalAssignments: 0, submittedAssignments: 0, gradedAssignments: 0,
      totalExams: 0, totalGoals: 0, completedGoals: 0, averageGoalProgress: 0,
      totalSessions: 0, upcomingSessions: 0, attendedSessions: 0
    });
    http.expectOne('/api/reports/student/student-1/history?pageNumber=1&pageSize=10&type=Assignments').flush({
      items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 1
    });
    let backEmitted = 0;
    fixture.componentInstance.back.subscribe(() => backEmitted++);
    fixture.detectChanges();

    const backButton = fixture.nativeElement.querySelector('button[data-testid="back-to-roster"]') as HTMLButtonElement;
    backButton.click();

    expect(backEmitted).toBe(1);
  });
});
