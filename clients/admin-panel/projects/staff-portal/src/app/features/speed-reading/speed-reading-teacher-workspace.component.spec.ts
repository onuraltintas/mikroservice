import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SpeedReadingTeacherWorkspaceComponent } from './speed-reading-teacher-workspace.component';

describe('SpeedReadingTeacherWorkspaceComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SpeedReadingTeacherWorkspaceComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shows the teacher roster and class overview from Speed Reading data', () => {
    const fixture = TestBed.createComponent(SpeedReadingTeacherWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/speed-reading/teachers/me/students?pageNumber=1&pageSize=25').flush({
      items: [
        {
          id: 'student-1',
          firstName: 'Ayşe',
          lastName: 'Yılmaz',
          email: 'ayse@example.test',
          currentLevel: 4,
          gradeLevel: 8,
          targetWpm: 300,
          targetComprehension: 85,
          dailyGoalMinutes: 20,
          learningStyle: 'Visual',
          isActive: true,
          createdAt: '2026-09-04T00:00:00Z',
        },
      ],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 25,
    });
    http
      .expectOne((request) => request.url === '/api/speed-reading/analytics/teacher/class-overview')
      .flush({
        dateFrom: '2026-09-01T00:00:00Z',
        dateTo: '2026-09-30T00:00:00Z',
        totalStudents: 1,
        activeStudents: 1,
        activeStudentsDataAvailable: true,
        classAverageWpmDataAvailable: true,
        classAverageComprehensionDataAvailable: true,
        classAverageWpm: 245,
        classAverageComprehension: 82,
        totalActivitiesCompleted: 19,
        studentsAboveAverage: 1,
        studentsAtAverage: 0,
        studentsBelowAverage: 0,
        topPerformers: [],
        studentsNeedingSupport: [],
      });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Hızlı Okuma · Öğretmen');
    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('245');
    expect(fixture.nativeElement.textContent).toContain('19');
  });

  it('applies search, grade and status filters to the teacher-scoped roster', () => {
    const fixture = TestBed.createComponent(SpeedReadingTeacherWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/speed-reading/teachers/me/students?pageNumber=1&pageSize=25').flush({
      items: [],
      totalCount: 0,
      pageNumber: 1,
      pageSize: 25,
    });
    http
      .expectOne((request) => request.url === '/api/speed-reading/analytics/teacher/class-overview')
      .flush({
        dateFrom: '',
        dateTo: '',
        totalStudents: 0,
        activeStudents: 0,
        activeStudentsDataAvailable: false,
        classAverageWpmDataAvailable: false,
        classAverageComprehensionDataAvailable: false,
        classAverageWpm: 0,
        classAverageComprehension: 0,
        totalActivitiesCompleted: 0,
        studentsAboveAverage: 0,
        studentsAtAverage: 0,
        studentsBelowAverage: 0,
        topPerformers: [],
        studentsNeedingSupport: [],
      });

    fixture.componentInstance.searchInput = '  Elif  ';
    fixture.componentInstance.selectedGradeLevel = '7';
    fixture.componentInstance.selectedStatus = 'inactive';
    fixture.componentInstance.applyFilters();

    const request = http.expectOne(
      (candidate) =>
        candidate.url === '/api/speed-reading/teachers/me/students' &&
        candidate.params.get('pageNumber') === '1' &&
        candidate.params.get('pageSize') === '25' &&
        candidate.params.get('searchTerm') === 'Elif' &&
        candidate.params.get('gradeLevel') === '7' &&
        candidate.params.get('isActive') === 'false',
    );
    request.flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci bulunamadı');
  });

  it('shows a retry action when the Speed Reading roster fails', () => {
    const fixture = TestBed.createComponent(SpeedReadingTeacherWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/speed-reading/teachers/me/students?pageNumber=1&pageSize=25').flush(
      {},
      {
        status: 500,
        statusText: 'Server Error',
      },
    );
    http
      .expectOne((request) => request.url === '/api/speed-reading/analytics/teacher/class-overview')
      .flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci listesi yüklenemedi');
    expect(fixture.nativeElement.textContent).toContain('Sınıf özeti yüklenemedi');
  });

  it('opens an individual student report using teacher-scoped Speed Reading APIs', () => {
    const fixture = TestBed.createComponent(SpeedReadingTeacherWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/speed-reading/teachers/me/students?pageNumber=1&pageSize=25').flush({
      items: [
        {
          id: 'student-1',
          firstName: 'Ayşe',
          lastName: 'Yılmaz',
          email: 'ayse@example.test',
          currentLevel: 4,
          gradeLevel: 8,
          isActive: true,
          createdAt: '2026-09-04T00:00:00Z',
        },
      ],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 25,
    });
    http
      .expectOne((request) => request.url === '/api/speed-reading/analytics/teacher/class-overview')
      .flush({
        dateFrom: '',
        dateTo: '',
        totalStudents: 1,
        activeStudents: 1,
        activeStudentsDataAvailable: true,
        classAverageWpmDataAvailable: false,
        classAverageComprehensionDataAvailable: false,
        classAverageWpm: 0,
        classAverageComprehension: 0,
        totalActivitiesCompleted: 0,
        studentsAboveAverage: 0,
        studentsAtAverage: 0,
        studentsBelowAverage: 1,
        topPerformers: [],
        studentsNeedingSupport: [],
      });
    fixture.detectChanges();

    const reportButton = fixture.nativeElement.querySelector(
      '[data-testid="teacher-student-report"]',
    ) as HTMLButtonElement | null;
    expect(reportButton).toBeTruthy();
    if (!reportButton) return;
    reportButton.click();
    fixture.detectChanges();

    const reportRequests = ['summary', 'reading-speed', 'comprehension', 'activity'].map((path) =>
      http.expectOne(
        (request) =>
          request.url === `/api/speed-reading/analytics/teacher/students/student-1/${path}`,
      ),
    );
    reportRequests[0].flush({
      readingSessions: 2,
      averageWpm: 245,
      averageComprehension: 80,
      totalReadingMinutes: 10,
      exercisesCompleted: 2,
      averageSuccessRate: 85,
      currentLevel: 4,
    });
    reportRequests[1].flush({
      averageWpm: 245,
      improvementRate: 5,
      benchmark: { institutionAverage: 220, performanceLevel: 'İyi' },
      recommendations: [],
    });
    reportRequests[2].flush({
      averageComprehension: 80,
      improvementRate: 3,
      totalQuestionsAttempted: 5,
      benchmark: { institutionAverage: 75, performanceLevel: 'İyi' },
      weakAreas: [],
      strongAreas: [],
    });
    reportRequests[3].flush({
      dataAvailable: true,
      unavailableReason: null,
      currentStreak: { days: 3, longestStreak: 4, lastActivityDate: '2026-09-29', isActive: true },
      recentActivities: [],
      studyTime: {
        totalMinutes: 10,
        totalSessions: 2,
        consistency: 80,
        mostActiveDay: 'Pazartesi',
      },
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci ayrıntılı raporu');
    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('245 WPM');
  });
});
