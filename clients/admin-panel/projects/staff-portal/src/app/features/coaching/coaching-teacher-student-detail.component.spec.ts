import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingTeacherStudentDetailComponent } from './coaching-teacher-student-detail.component';
import { CoachingTeacherStudent } from './coaching-teacher-students.service';

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
