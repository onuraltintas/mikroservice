import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingTeacherStudentsComponent } from './coaching-teacher-students.component';

describe('CoachingTeacherStudentsComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoachingTeacherStudentsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lets a Coaching teacher invite a student and reports the product-specific result', () => {
    const fixture = TestBed.createComponent(CoachingTeacherStudentsComponent);
    fixture.detectChanges();
    http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=25').flush({
      items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="teacher-student-invite-email"]')).toBeTruthy();

    fixture.componentInstance.studentInviteEmail = 'student@example.test';
    fixture.componentInstance.sendStudentInvitation();
    http.expectOne('/api/teachers/invite-student').flush({ invitationId: 'coaching-invitation-1' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Koçluk öğrenci daveti gönderildi');
  });

  it('shows the teacher-scoped roster with grade and institution context', () => {
    const fixture = TestBed.createComponent(CoachingTeacherStudentsComponent);
    fixture.detectChanges();
    http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=25').flush({
      items: [{
        userId: 'student-1',
        firstName: 'Ayşe',
        lastName: 'Yılmaz',
        fullName: 'Ayşe Yılmaz',
        gradeLevel: 8,
        institutionName: 'Atatürk Ortaokulu',
        subject: 'Matematik',
        assignmentStartDate: '2026-09-01T00:00:00Z'
      }],
      pageNumber: 1,
      pageSize: 25,
      totalCount: 1,
      totalPages: 1
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('Atatürk Ortaokulu');
    expect(fixture.nativeElement.textContent).toContain('8. sınıf');
    expect(fixture.nativeElement.textContent).toContain('1 Eyl 2026');
  });

  it('retries a failed roster request from the visible error state', () => {
    const fixture = TestBed.createComponent(CoachingTeacherStudentsComponent);
    fixture.detectChanges();
    http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=25').flush({}, {
      status: 500,
      statusText: 'Server Error'
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci listesi yüklenemedi');
    const retryButton = fixture.nativeElement.querySelector('button[data-testid="retry-roster"]') as HTMLButtonElement;
    retryButton.click();
    fixture.detectChanges();
    http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=25').flush({
      items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci bulunamadı');
  });

  it('pages through the roster and resets to the first page when searching', () => {
    const fixture = TestBed.createComponent(CoachingTeacherStudentsComponent);
    fixture.detectChanges();
    http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=25').flush({
      items: [], pageNumber: 1, pageSize: 25, totalCount: 30, totalPages: 2
    });

    fixture.componentInstance.nextPage();
    http.expectOne('/api/teachers/me/students?pageNumber=2&pageSize=25').flush({
      items: [], pageNumber: 2, pageSize: 25, totalCount: 30, totalPages: 2
    });
    fixture.componentInstance.setSearchTerm('  Ayşe  ');
    const searchRequest = http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=25&searchTerm=Ay%C5%9Fe');
    searchRequest.flush({
      items: [{
        userId: 'student-2',
        firstName: 'Ayşe',
        lastName: 'Demir',
        fullName: 'Ayşe Demir',
        assignmentStartDate: '2026-09-02T00:00:00Z'
      }],
      pageNumber: 1,
      pageSize: 25,
      totalCount: 1,
      totalPages: 1
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.pageNumber()).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Ayşe Demir');
  });

  it('ignores an older request when a newer search response arrives first', () => {
    const fixture = TestBed.createComponent(CoachingTeacherStudentsComponent);
    fixture.detectChanges();
    const initialRequest = http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=25');
    fixture.componentInstance.setSearchTerm('Ayşe');
    const searchRequest = http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=25&searchTerm=Ay%C5%9Fe');
    searchRequest.flush({
      items: [{
        userId: 'new-result',
        firstName: 'Ayşe',
        lastName: 'Demir',
        fullName: 'Ayşe Demir',
        assignmentStartDate: '2026-09-02T00:00:00Z'
      }],
      pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1
    });
    initialRequest.flush({
      items: [{
        userId: 'old-result',
        firstName: 'Mehmet',
        lastName: 'Kaya',
        fullName: 'Mehmet Kaya',
        assignmentStartDate: '2026-09-01T00:00:00Z'
      }],
      pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ayşe Demir');
    expect(fixture.nativeElement.textContent).not.toContain('Mehmet Kaya');
  });

  it('opens the selected student report and returns to the same roster', () => {
    const fixture = TestBed.createComponent(CoachingTeacherStudentsComponent);
    fixture.detectChanges();
    http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=25').flush({
      items: [{
        userId: 'student-1',
        firstName: 'Ayşe',
        lastName: 'Yılmaz',
        fullName: 'Ayşe Yılmaz',
        assignmentStartDate: '2026-09-01T00:00:00Z'
      }],
      pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1
    });
    fixture.detectChanges();

    const openButton = fixture.nativeElement.querySelector('button[data-testid="open-student-report"]') as HTMLButtonElement;
    openButton.click();
    fixture.detectChanges();
    http.expectOne('/api/reports/student/student-1/progress').flush({
      studentId: 'student-1', totalAssignments: 0, submittedAssignments: 0, gradedAssignments: 0,
      totalExams: 0, totalGoals: 0, completedGoals: 0, averageGoalProgress: 0,
      totalSessions: 0, upcomingSessions: 0, attendedSessions: 0
    });
    http.expectOne('/api/reports/student/student-1/history?pageNumber=1&pageSize=10&type=Assignments').flush({
      items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 1
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('ÖĞRENCİ RAPORU');

    const backButton = fixture.nativeElement.querySelector('button[data-testid="back-to-roster"]') as HTMLButtonElement;
    backButton.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Öğrencilerim');
    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
  });
});
