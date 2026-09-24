import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAdminService } from '../../../core/services/coaching-admin.service';
import { CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { InstitutionService } from '../../../core/services/institution.service';
import { CoachingOverviewComponent } from './coaching-overview';

describe('CoachingOverviewComponent', () => {
  it('opens authorized coaching progress and results for a named student', () => {
    const portal = {
      getStudentProgress: vi.fn(() => of({ studentId: 'student-1', totalAssignments: 2 })),
      getStudentAssignments: vi.fn(() => of({ items: [] })),
      getStudentExamResults: vi.fn(() => of({ items: [] })),
      getStudentGoals: vi.fn(() => of({ items: [] }))
    };
    TestBed.configureTestingModule({
      imports: [CoachingOverviewComponent],
      providers: [
        { provide: CoachingAdminService, useValue: { getOverview: () => of(null) } },
        { provide: InstitutionService, useValue: { getAll: () => of({ items: [] }) } },
        { provide: CoachingPortalService, useValue: portal }
      ]
    });
    const component = TestBed.createComponent(CoachingOverviewComponent).componentInstance;

    component.openStudent({ studentId: 'student-1', studentName: 'Ayşe Yılmaz' } as never);

    expect(portal.getStudentProgress).toHaveBeenCalledWith('student-1');
    expect(component.selectedStudent()?.studentName).toBe('Ayşe Yılmaz');
  });

  it('shows scoped student and teacher names instead of a raw student id', () => {
    const service = {
      getOverview: vi.fn(() => of({
        totalAssignments: 0, activeAssignments: 0, completedAssignments: 0,
        cancelledAssignments: 0, totalAssignmentStudents: 0, submittedAssignmentStudents: 0,
        totalExams: 0, totalExamResults: 0, totalSessions: 0, upcomingSessions: 0,
        totalGoals: 0, completedGoals: 0, recentAssignments: []
      }))
    };
    TestBed.configureTestingModule({
      imports: [CoachingOverviewComponent],
      providers: [
        { provide: CoachingAdminService, useValue: service },
        { provide: InstitutionService, useValue: { getAll: () => of({ items: [] }) } }
      ]
    });
    const fixture = TestBed.createComponent(CoachingOverviewComponent);
    fixture.componentInstance.earlyWarnings.set({
      institutionId: 'institution-1', fromDate: '', toDate: '', pageNumber: 1,
      pageSize: 25, totalCount: 1, totalPages: 1,
      items: [{ studentId: 'student-123456789', studentName: 'Ayşe Yılmaz',
        studentEmail: 'ayse@example.test', teacherName: 'Öğretmen A',
        riskLevel: 'Low', riskScore: 0, reasonCodes: [], assignmentCount: 2,
        submittedAssignmentCount: 1, recordedAttendanceCount: 0,
        attendedSessionCount: 0, goalCount: 0, averageGoalProgress: 0 }]
    });

    fixture.detectChanges();

    const table = fixture.nativeElement.textContent as string;
    expect(table).toContain('Ayşe Yılmaz');
    expect(table).toContain('Öğretmen A');
    expect(table).not.toContain('student-123456789');
  });

  it('loads a bounded institution comparison for the selected grade and dates', () => {
    const service = {
      getOverview: vi.fn(() => of(null)),
      getInstitutionComparison: vi.fn(() => of({}))
    };
    const institutions = {
      getAll: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 100 }))
    };

    TestBed.configureTestingModule({
      imports: [CoachingOverviewComponent],
      providers: [
        { provide: CoachingAdminService, useValue: service },
        { provide: InstitutionService, useValue: institutions }
      ]
    });
    const component = TestBed.createComponent(CoachingOverviewComponent).componentInstance;
    component.selectedInstitutionId = 'institution-1';
    component.selectedGradeLevel = 8;
    component.fromDate = '2030-01-01';
    component.toDate = '2030-02-01';

    component.loadComparison();

    expect(service.getInstitutionComparison).toHaveBeenCalledWith('institution-1', {
      gradeLevel: 8,
      fromDate: '2030-01-01T00:00:00.000Z',
      toDate: '2030-02-01T23:59:59.999Z'
    });
  });

  it('loads a paged early-warning report with the same institution scope', () => {
    const service = {
      getOverview: vi.fn(() => of(null)),
      getInstitutionComparison: vi.fn(() => of({})),
      getInstitutionEarlyWarnings: vi.fn(() => of({
        items: [],
        pageNumber: 1,
        pageSize: 25,
        totalCount: 0,
        totalPages: 0
      }))
    };
    const institutions = {
      getAll: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 100 }))
    };

    TestBed.configureTestingModule({
      imports: [CoachingOverviewComponent],
      providers: [
        { provide: CoachingAdminService, useValue: service },
        { provide: InstitutionService, useValue: institutions }
      ]
    });
    const component = TestBed.createComponent(CoachingOverviewComponent).componentInstance;
    component.selectedInstitutionId = 'institution-1';
    component.selectedGradeLevel = 8;
    component.fromDate = '2030-01-01';
    component.toDate = '2030-02-01';

    component.loadEarlyWarnings();

    expect(service.getInstitutionEarlyWarnings).toHaveBeenCalledWith('institution-1', {
      pageNumber: 1,
      pageSize: 25,
      gradeLevel: 8,
      fromDate: '2030-01-01T00:00:00.000Z',
      toDate: '2030-02-01T23:59:59.999Z'
    });
  });
});
