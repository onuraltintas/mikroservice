import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SpeedReadingInstitutionWorkspaceComponent } from './speed-reading-institution-workspace.component';

describe('SpeedReadingInstitutionWorkspaceComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SpeedReadingInstitutionWorkspaceComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function createLoadedWorkspace() {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({
      institutionId: 'institution-1',
      institutionName: 'Örnek Kurum',
    });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/institutions/institution-1/members' &&
          request.params.get('role') === 'Student',
      )
      .flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/analytics/institutions/institution-1/class-overview',
      )
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
    fixture.detectChanges();
    return fixture;
  }

  it('loads the authenticated institution, its own roster and Speed Reading analytics', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({
      institutionId: 'institution-1',
      institutionName: 'Örnek Kurum',
    });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/institutions/institution-1/members' &&
          request.params.get('role') === 'Student' &&
          request.params.get('pageNumber') === '1' &&
          request.params.get('pageSize') === '25',
      )
      .flush({
        items: [
          {
            userId: 'student-1',
            firstName: 'Ayşe',
            lastName: 'Yılmaz',
            email: 'ayse@example.test',
            role: 'Student',
            isMembershipActive: true,
            isActive: true,
            createdAt: '2026-09-04T00:00:00Z',
            gradeLevel: 8,
            currentLevel: 4,
            targetWpm: 300,
            targetComprehension: 85,
            dailyGoalMinutes: 20,
            teacherUserId: 'teacher-1',
            teacherName: 'Mehmet Kaya',
            studentCount: 0,
          },
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 25,
      });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/analytics/institutions/institution-1/class-overview',
      )
      .flush({
        dateFrom: '2026-09-01T00:00:00Z',
        dateTo: '2026-09-30T00:00:00Z',
        totalStudents: 12,
        activeStudents: 9,
        activeStudentsDataAvailable: true,
        classAverageWpmDataAvailable: true,
        classAverageComprehensionDataAvailable: true,
        classAverageWpm: 240,
        classAverageComprehension: 81,
        totalActivitiesCompleted: 64,
        studentsAboveAverage: 3,
        studentsAtAverage: 5,
        studentsBelowAverage: 4,
        topPerformers: [],
        studentsNeedingSupport: [],
      });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Örnek Kurum');
    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('Mehmet Kaya');
    expect(fixture.nativeElement.textContent).toContain('240');
    expect(fixture.nativeElement.textContent).toContain('64');
  });

  it('switches from students to teachers and excludes student-only grade filters', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({
      institutionId: 'institution-1',
      institutionName: 'Örnek Kurum',
    });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/institutions/institution-1/members' &&
          request.params.get('role') === 'Student',
      )
      .flush({
        items: [],
        totalCount: 0,
        pageNumber: 1,
        pageSize: 25,
      });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/analytics/institutions/institution-1/class-overview',
      )
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

    fixture.componentInstance.selectRole('Teacher');
    const teacherRequest = http.expectOne(
      (request) =>
        request.url === '/api/speed-reading/institutions/institution-1/members' &&
        request.params.get('role') === 'Teacher' &&
        request.params.get('pageNumber') === '1' &&
        request.params.get('pageSize') === '25',
    );
    expect(teacherRequest.request.params.has('gradeLevel')).toBe(false);
    teacherRequest.flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğretmen bulunamadı');
  });

  it('applies search, grade and status filters to institution-scoped students', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({
      institutionId: 'institution-1',
      institutionName: 'Örnek Kurum',
    });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/institutions/institution-1/members' &&
          request.params.get('role') === 'Student',
      )
      .flush({
        items: [],
        totalCount: 0,
        pageNumber: 1,
        pageSize: 25,
      });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/analytics/institutions/institution-1/class-overview',
      )
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
        candidate.url === '/api/speed-reading/institutions/institution-1/members' &&
        candidate.params.get('pageNumber') === '1' &&
        candidate.params.get('pageSize') === '25' &&
        candidate.params.get('role') === 'Student' &&
        candidate.params.get('searchTerm') === 'Elif' &&
        candidate.params.get('gradeLevel') === '7' &&
        candidate.params.get('isActive') === 'false',
    );
    request.flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci bulunamadı');
  });

  it('shows a retry action when the institution roster request fails', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({
      institutionId: 'institution-1',
      institutionName: 'Örnek Kurum',
    });
    http
      .expectOne(
        (request) => request.url === '/api/speed-reading/institutions/institution-1/members',
      )
      .flush({}, { status: 500, statusText: 'Server Error' });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/analytics/institutions/institution-1/class-overview',
      )
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
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Kurum üye listesi yüklenemedi');
    const retryButton = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Tekrar dene'));
    expect(retryButton).toBeDefined();
    if (!retryButton) return;
    retryButton.click();
    http
      .expectOne(
        (request) => request.url === '/api/speed-reading/institutions/institution-1/members',
      )
      .flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci bulunamadı');
  });

  it('edits a student using a teacher searched within the current institution', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({
      institutionId: 'institution-1',
      institutionName: 'Örnek Kurum',
    });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/institutions/institution-1/members' &&
          request.params.get('role') === 'Student',
      )
      .flush({
        items: [
          {
            userId: 'student-1',
            firstName: 'Ayşe',
            lastName: 'Yılmaz',
            role: 'Student',
            isMembershipActive: true,
            isActive: true,
            createdAt: '2026-09-04T00:00:00Z',
            gradeLevel: 8,
            teacherUserId: 'teacher-1',
            teacherName: 'Mehmet Kaya',
            studentCount: 0,
          },
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 25,
      });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/analytics/institutions/institution-1/class-overview',
      )
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

    const editButton = fixture.nativeElement.querySelector(
      '[data-testid="edit-student-profile"]',
    ) as HTMLButtonElement | null;
    expect(editButton).toBeTruthy();
    if (!editButton) return;
    editButton.click();
    fixture.detectChanges();

    fixture.componentInstance.studentGradeLevel = '9';
    fixture.componentInstance.teacherSearchInput = 'Zeynep';
    fixture.componentInstance.searchInstitutionTeachers();
    const teacherSearch = http.expectOne(
      (request) =>
        request.url === '/api/speed-reading/institutions/institution-1/members' &&
        request.params.get('role') === 'Teacher' &&
        request.params.get('searchTerm') === 'Zeynep',
    );
    teacherSearch.flush({
      items: [
        {
          userId: 'teacher-2',
          firstName: 'Zeynep',
          lastName: 'Demir',
          role: 'Teacher',
          isMembershipActive: true,
          isActive: true,
          createdAt: '2026-09-04T00:00:00Z',
          studentCount: 4,
        },
      ],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 25,
    });
    fixture.detectChanges();

    fixture.componentInstance.studentTeacherUserId = 'teacher-2';
    fixture.nativeElement.querySelector('[data-testid="save-student-profile"]').click();

    const update = http.expectOne(
      '/api/speed-reading/institutions/institution-1/members/student-1/student-profile',
    );
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ gradeLevel: 9, teacherUserId: 'teacher-2' });
    update.flush(null, { status: 204, statusText: 'No Content' });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/institutions/institution-1/members' &&
          request.params.get('role') === 'Student',
      )
      .flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
  });

  it('requires confirmation before changing a Speed Reading institution membership', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({
      institutionId: 'institution-1',
      institutionName: 'Örnek Kurum',
    });
    http
      .expectOne(
        (request) => request.url === '/api/speed-reading/institutions/institution-1/members',
      )
      .flush({
        items: [
          {
            userId: 'student-1',
            firstName: 'Ayşe',
            lastName: 'Yılmaz',
            role: 'Student',
            isMembershipActive: true,
            isActive: false,
            createdAt: '2026-09-04T00:00:00Z',
            studentCount: 0,
          },
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 25,
      });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/analytics/institutions/institution-1/class-overview',
      )
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

    const action = fixture.nativeElement.querySelector(
      '[data-testid="toggle-member-status"]',
    ) as HTMLButtonElement | null;
    expect(action).toBeTruthy();
    if (!action) return;
    expect(action.textContent).toContain('Üyeliği kapat');
    action.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(
      'Üyelik durumunu değiştirmek istiyor musunuz?',
    );

    fixture.nativeElement.querySelector('[data-testid="confirm-member-status"]').click();
    const update = http.expectOne(
      '/api/speed-reading/institutions/institution-1/members/student-1',
    );
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ role: 'Student', isActive: false });
    update.flush(null, { status: 204, statusText: 'No Content' });
    http
      .expectOne(
        (request) => request.url === '/api/speed-reading/institutions/institution-1/members',
      )
      .flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/analytics/institutions/institution-1/class-overview',
      )
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
  });

  it('opens the detailed student report from the institution roster', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({
      institutionId: 'institution-1',
      institutionName: 'Örnek Kurum',
    });
    http
      .expectOne(
        (request) => request.url === '/api/speed-reading/institutions/institution-1/members',
      )
      .flush({
        items: [
          {
            userId: 'student-1',
            firstName: 'Ayşe',
            lastName: 'Yılmaz',
            role: 'Student',
            isMembershipActive: true,
            isActive: true,
            createdAt: '2026-09-04T00:00:00Z',
            studentCount: 0,
          },
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 25,
      });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/analytics/institutions/institution-1/class-overview',
      )
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
      '[data-testid="student-report"]',
    ) as HTMLButtonElement | null;
    expect(reportButton).toBeTruthy();
    if (!reportButton) return;
    reportButton.click();
    fixture.detectChanges();

    const reportRequests = ['summary', 'reading-speed', 'comprehension', 'activity'].map((path) =>
      http.expectOne(
        (request) =>
          request.url ===
          `/api/speed-reading/analytics/institutions/institution-1/students/student-1/${path}`,
      ),
    );
    reportRequests[0].flush({
      readingSessions: 1,
      averageWpm: 200,
      totalReadingMinutes: 10,
      exercisesCompleted: 1,
      averageSuccessRate: 80,
      currentLevel: 2,
    });
    reportRequests[1].flush({
      averageWpm: 200,
      improvementRate: 0,
      benchmark: { institutionAverage: 190, performanceLevel: 'İyi' },
      recommendations: [],
    });
    reportRequests[2].flush({
      averageComprehension: 80,
      improvementRate: 0,
      totalQuestionsAttempted: 2,
      benchmark: { institutionAverage: 75, performanceLevel: 'İyi' },
      strongAreas: [],
      weakAreas: [],
    });
    reportRequests[3].flush({ dataAvailable: false, unavailableReason: 'Veri yok' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Öğrenci ayrıntılı raporu');
  });

  it('loads assignment analytics for the selected date range in the institution report area', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({
      institutionId: 'institution-1',
      institutionName: 'Örnek Kurum',
    });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/institutions/institution-1/members' &&
          request.params.get('role') === 'Student',
      )
      .flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/analytics/institutions/institution-1/class-overview',
      )
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

    fixture.componentInstance.reportDateFrom = '2026-09-01';
    fixture.componentInstance.reportDateTo = '2026-09-30';
    fixture.detectChanges();
    const assignmentsTab = fixture.nativeElement.querySelector(
      '[data-testid="institution-report-assignments"]',
    ) as HTMLButtonElement | null;
    expect(assignmentsTab).toBeTruthy();
    if (!assignmentsTab) return;
    assignmentsTab.click();

    const request = http.expectOne(
      (candidate) =>
        candidate.url === '/api/speed-reading/analytics/institutions/institution-1/assignments' &&
        candidate.params.get('dateFrom') === '2026-09-01T00:00:00.000Z' &&
        candidate.params.get('dateTo') === '2026-09-30T23:59:59.999Z',
    );
    expect(request.request.method).toBe('GET');
    request.flush({
      dataAvailable: true,
      assignmentCount: 2,
      completionStats: {
        totalStudents: 4,
        completed: 2,
        inProgress: 1,
        notStarted: 1,
        completionRate: 50,
      },
      performanceStats: null,
      studentBreakdown: [],
      scoreDistribution: [],
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ödev raporu');
    expect(fixture.nativeElement.textContent).toContain('Tamamlanma oranı');
    expect(fixture.nativeElement.textContent).toContain('50%');
  });

  it('shows content and time-progress reports from their institution-scoped endpoints', () => {
    const fixture = createLoadedWorkspace();
    const contentTab = fixture.nativeElement.querySelector(
      '[data-testid="institution-report-content"]',
    ) as HTMLButtonElement | null;
    expect(contentTab).toBeTruthy();
    if (!contentTab) return;
    contentTab.click();
    http
      .expectOne(
        (request) =>
          request.url ===
          '/api/speed-reading/analytics/institutions/institution-1/content-analysis',
      )
      .flush({
        exerciseAnalysis: [
          {
            exerciseTypeName: 'Anlama egzersizi',
            totalCompletions: 12,
            activeStudents: 4,
            averageScore: 82,
            performanceLevel: 'İyi',
          },
        ],
        exerciseFrequencyChart: [],
        readingAnalysis: [],
        readingPerformanceChart: [],
      });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('İçerik analizi');
    expect(fixture.nativeElement.textContent).toContain('Anlama egzersizi');

    const progressTab = fixture.nativeElement.querySelector(
      '[data-testid="institution-report-progress"]',
    ) as HTMLButtonElement | null;
    expect(progressTab).toBeTruthy();
    if (!progressTab) return;
    progressTab.click();
    http
      .expectOne(
        (request) =>
          request.url === '/api/speed-reading/analytics/institutions/institution-1/time-progress',
      )
      .flush({
        weeklyProgressChart: [],
        monthlyProgressChart: [],
        activityIntensityChart: [],
        improvingStudents: [
          {
            studentId: 'student-1',
            studentName: 'Ayşe Yılmaz',
            previousScore: 70,
            currentScore: 82,
            improvement: 12,
            trend: 'improving',
            metric: 'Anlama',
          },
        ],
        decliningStudents: [],
      });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Zaman ve ilerleme');
    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('İlerleme gösteren öğrenciler');
  });

  it('rejects an inverted institution report date range without requesting data', () => {
    const fixture = createLoadedWorkspace();
    fixture.componentInstance.reportDateFrom = '2026-09-30';
    fixture.componentInstance.reportDateTo = '2026-09-01';
    fixture.componentInstance.selectInstitutionReport('assignments');

    http.expectNone('/api/speed-reading/analytics/institutions/institution-1/assignments');
    expect(fixture.componentInstance.institutionReportErrorMessage()).toContain(
      'başlangıç tarihi bitiş tarihinden sonra',
    );
  });

  it('rejects an institution report date range longer than the API limit', () => {
    const fixture = createLoadedWorkspace();
    fixture.componentInstance.reportDateFrom = '2025-01-01';
    fixture.componentInstance.reportDateTo = '2026-01-02';
    fixture.componentInstance.selectInstitutionReport('content');

    http.expectNone('/api/speed-reading/analytics/institutions/institution-1/content-analysis');
    expect(fixture.componentInstance.institutionReportErrorMessage()).toBe(
      'Rapor tarih aralığı en fazla 366 gün olabilir.',
    );
  });

  it('ignores an older report response after the selected date range becomes invalid', () => {
    const fixture = createLoadedWorkspace();
    fixture.componentInstance.reportDateFrom = '2026-09-01';
    fixture.componentInstance.reportDateTo = '2026-09-30';
    fixture.componentInstance.selectInstitutionReport('assignments');
    const pendingReport = http.expectOne(
      (request) =>
        request.url === '/api/speed-reading/analytics/institutions/institution-1/assignments',
    );

    fixture.componentInstance.reportDateFrom = '2026-09-30';
    fixture.componentInstance.reportDateTo = '2026-09-01';
    fixture.componentInstance.applyInstitutionReportRange();
    pendingReport.flush({
      dataAvailable: true,
      assignmentCount: 4,
      completionStats: {
        totalStudents: 3,
        completed: 2,
        inProgress: 1,
        notStarted: 0,
        completionRate: 70,
      },
      studentBreakdown: [],
      scoreDistribution: [],
    });

    expect(fixture.componentInstance.assignmentReport()).toBeNull();
    expect(fixture.componentInstance.institutionReportErrorMessage()).toContain(
      'başlangıç tarihi bitiş tarihinden sonra',
    );
  });

  it('does not call the Speed Reading API when Identity cannot resolve an institution', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush(
      {},
      {
        status: 403,
        statusText: 'Forbidden',
      },
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Kurum bilgisi alınamadı');
  });
});
