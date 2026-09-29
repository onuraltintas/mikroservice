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
    expect(fixture.nativeElement.textContent).toContain('önceki kurum kayıtları da kapsanır');
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

    const search = fixture.nativeElement.querySelector('input[name="historySearch"]') as HTMLInputElement;
    search.value = '  Fen sınavı  ';
    search.dispatchEvent(new Event('input'));
    const status = fixture.nativeElement.querySelector('select[name="historyStatus"]') as HTMLSelectElement;
    status.value = 'Result';
    status.dispatchEvent(new Event('change'));
    const fromDate = fixture.nativeElement.querySelector('input[name="historyFromDate"]') as HTMLInputElement;
    fromDate.value = '2026-09-01';
    fromDate.dispatchEvent(new Event('input'));
    const filterButton = fixture.nativeElement.querySelector('button[data-testid="apply-history-filters"]') as HTMLButtonElement;
    filterButton.click();

    const filteredRequest = http.expectOne(request =>
      request.url === '/api/reports/student/student-1/history'
        && request.params.get('type') === 'Exams'
        && request.params.get('pageNumber') === '1'
        && request.params.get('search') === 'Fen sınavı'
        && request.params.get('status') === 'Result'
        && request.params.get('fromDate') === '2026-09-01T00:00:00.000Z');
    filteredRequest.flush({ items: [], pageNumber: 1, pageSize: 10, totalCount: 21, totalPages: 3 });
    fixture.detectChanges();

    const nextPage = fixture.nativeElement.querySelector('button[data-testid="next-history-page"]') as HTMLButtonElement;
    nextPage.click();
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

    expect(fixture.nativeElement.textContent).toContain('Kesirler çalışma kağıdı');
    expect(fixture.nativeElement.textContent).toContain('Geçmiş kayıtlar yüklenemedi');
    const retry = fixture.nativeElement.querySelector('button[data-testid="retry-history"]') as HTMLButtonElement;
    retry.click();
    http.expectOne('/api/reports/student/student-1/history?pageNumber=1&pageSize=10&type=Assignments').flush({
      items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 1
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Bu kategoride kayıt bulunmuyor');
  });

  it('emits a return action to the student roster', () => {
    const fixture = createDetail();
    let backEmitted = 0;
    fixture.componentInstance.back.subscribe(() => backEmitted++);
    fixture.detectChanges();

    const backButton = fixture.nativeElement.querySelector('button[data-testid="back-to-roster"]') as HTMLButtonElement;
    backButton.click();

    expect(backEmitted).toBe(1);
  });
});
