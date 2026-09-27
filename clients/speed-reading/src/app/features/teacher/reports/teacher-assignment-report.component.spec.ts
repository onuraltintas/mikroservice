import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { TeacherAssignmentReportComponent } from './teacher-assignment-report.component';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';

describe('TeacherAssignmentReportComponent', () => {
  const report = {
    metadata: {
      reportId: 'teacher-assignments',
      reportType: 'Institution',
      generatedAt: new Date('2026-01-31T23:59:59.999Z'),
      generatedBy: 'self',
      startDate: new Date('2026-01-01T00:00:00.000Z'),
      endDate: new Date('2026-01-31T23:59:59.999Z')
    },
    dataAvailable: true,
    assignmentCount: 4,
    assignmentInfo: null,
    completionStats: { totalStudents: 3, completed: 1, inProgress: 1, notStarted: 1, completionRate: 40 },
    performanceStats: { averageScore: 82, medianScore: 82, highestScore: 90, lowestScore: 74, standardDeviation: 8 },
    scoreDistribution: { data: [{ name: '80-89', series: [{ name: 'Ödev sayısı', value: 2 }] }] },
    studentBreakdown: [{
      studentId: 'student-1', studentName: 'Ayşe Yılmaz', status: 'completed' as const,
      score: 82, completionTime: 30, submittedAt: new Date('2026-01-20T10:00:00.000Z')
    }],
    timeStats: { averageCompletionTime: 30, medianCompletionTime: 30, fastestCompletion: 30, slowestCompletion: 30 }
  };

  function createComponent(queryParams: Record<string, string>, isInstitutionManager: boolean) {
    const reportsService = {
      getInstitutionAssignmentReport: jasmine.createSpy('getInstitutionAssignmentReport').and.returnValue(of(report)),
      getAdminTeacherAssignmentReport: jasmine.createSpy('getAdminTeacherAssignmentReport').and.returnValue(of(report)),
      getTeacherAssignmentReport: jasmine.createSpy('getTeacherAssignmentReport').and.returnValue(of(report))
    };
    TestBed.configureTestingModule({
      imports: [TeacherAssignmentReportComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParams: of(queryParams), snapshot: { queryParamMap: { get: (key: string) => queryParams[key] ?? null } } } },
        { provide: ReportsService, useValue: reportsService },
        { provide: AuthService, useValue: {
          currentUserValue: { id: 'manager-1', institutionId: 'institution-1' },
          hasRole: () => isInstitutionManager
        } }
      ]
    });

    const fixture = TestBed.createComponent(TeacherAssignmentReportComponent);
    fixture.detectChanges();
    return { fixture, reportsService };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('loads institution-wide assignments and renders only reported metrics', () => {
    const { fixture, reportsService } = createComponent({ mode: 'institution' }, true);

    expect(reportsService.getInstitutionAssignmentReport).toHaveBeenCalledWith(
      'institution-1', jasmine.any(Date), jasmine.any(Date));
    expect(fixture.nativeElement.textContent).toContain('Ödev tamamlama raporu');
    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('4');
  });

  it('loads only the selected teacher scope from the institution manager view', () => {
    const { reportsService } = createComponent({ mode: 'teacher', teacherId: 'teacher-1' }, true);

    expect(reportsService.getAdminTeacherAssignmentReport).toHaveBeenCalledWith(
      'teacher-1', jasmine.any(Date), jasmine.any(Date));
    expect(reportsService.getInstitutionAssignmentReport).not.toHaveBeenCalled();
  });

  it('applies the selected date range and rejects reversed filters', () => {
    const { fixture, reportsService } = createComponent({ mode: 'institution' }, true);
    const component = fixture.componentInstance;
    component.startDate.set('2026-01-01');
    component.endDate.set('2026-01-15');
    component.loadReport();

    expect(reportsService.getInstitutionAssignmentReport).toHaveBeenCalledWith(
      'institution-1',
      new Date('2026-01-01T00:00:00.000Z'),
      new Date('2026-01-15T23:59:59.999Z'));

    component.startDate.set('2026-02-01');
    component.endDate.set('2026-01-15');
    component.loadReport();
    fixture.detectChanges();

    expect(reportsService.getInstitutionAssignmentReport).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('Başlangıç ve bitiş tarihlerini kontrol edin.');
  });

  it('loads the authenticated teacher scope without accepting a client-selected teacher id', () => {
    const { reportsService } = createComponent({ mode: 'teacher', teacherId: 'other-teacher' }, false);

    expect(reportsService.getTeacherAssignmentReport).toHaveBeenCalledWith(
      'manager-1', jasmine.any(Date), jasmine.any(Date));
    expect(reportsService.getAdminTeacherAssignmentReport).not.toHaveBeenCalled();
  });
});
