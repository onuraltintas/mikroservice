import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SpeedReadingInstitutionWorkspaceComponent } from './speed-reading-institution-workspace.component';

describe('SpeedReadingInstitutionWorkspaceComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SpeedReadingInstitutionWorkspaceComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the authenticated institution, its own roster and Speed Reading analytics', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({
      institutionId: 'institution-1', institutionName: 'Örnek Kurum'
    });
    http.expectOne(request =>
      request.url === '/api/speed-reading/institutions/institution-1/members'
        && request.params.get('role') === 'Student'
        && request.params.get('pageNumber') === '1'
        && request.params.get('pageSize') === '25').flush({
      items: [{
        userId: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'ayse@example.test',
        role: 'Student', isActive: true, createdAt: '2026-09-04T00:00:00Z', gradeLevel: 8,
        currentLevel: 4, targetWpm: 300, targetComprehension: 85, dailyGoalMinutes: 20,
        teacherUserId: 'teacher-1', teacherName: 'Mehmet Kaya', studentCount: 0
      }],
      totalCount: 1, pageNumber: 1, pageSize: 25
    });
    http.expectOne(request =>
      request.url === '/api/speed-reading/analytics/institutions/institution-1/class-overview').flush({
      dateFrom: '2026-09-01T00:00:00Z', dateTo: '2026-09-30T00:00:00Z',
      totalStudents: 12, activeStudents: 9, activeStudentsDataAvailable: true,
      classAverageWpmDataAvailable: true, classAverageComprehensionDataAvailable: true,
      classAverageWpm: 240, classAverageComprehension: 81, totalActivitiesCompleted: 64,
      studentsAboveAverage: 3, studentsAtAverage: 5, studentsBelowAverage: 4,
      topPerformers: [], studentsNeedingSupport: []
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
      institutionId: 'institution-1', institutionName: 'Örnek Kurum'
    });
    http.expectOne(request => request.url === '/api/speed-reading/institutions/institution-1/members'
      && request.params.get('role') === 'Student').flush({
      items: [], totalCount: 0, pageNumber: 1, pageSize: 25
    });
    http.expectOne(request => request.url === '/api/speed-reading/analytics/institutions/institution-1/class-overview')
      .flush({ dateFrom: '', dateTo: '', totalStudents: 0, activeStudents: 0,
        activeStudentsDataAvailable: false, classAverageWpmDataAvailable: false,
        classAverageComprehensionDataAvailable: false, classAverageWpm: 0,
        classAverageComprehension: 0, totalActivitiesCompleted: 0, studentsAboveAverage: 0,
        studentsAtAverage: 0, studentsBelowAverage: 0, topPerformers: [], studentsNeedingSupport: [] });

    fixture.componentInstance.selectRole('Teacher');
    const teacherRequest = http.expectOne(request =>
      request.url === '/api/speed-reading/institutions/institution-1/members'
        && request.params.get('role') === 'Teacher'
        && request.params.get('pageNumber') === '1'
        && request.params.get('pageSize') === '25');
    expect(teacherRequest.request.params.has('gradeLevel')).toBe(false);
    teacherRequest.flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğretmen bulunamadı');
  });

  it('does not call the Speed Reading API when Identity cannot resolve an institution', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionWorkspaceComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({}, {
      status: 403, statusText: 'Forbidden'
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Kurum bilgisi alınamadı');
  });
});
