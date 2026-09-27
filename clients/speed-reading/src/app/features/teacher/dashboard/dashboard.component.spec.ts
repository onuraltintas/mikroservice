import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { AuthService } from '../../../core/services/auth.service';
import { ReportsService } from '../../../core/services/reports.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { StudentsService } from '../../../core/services/students.service';
import { InstitutionsService } from '../../../core/services/institutions.service';
import { TeacherReportService } from '../../../core/services/teacher-report.service';

describe('DashboardComponent', () => {
  it('shows the school grade rather than the reading level', () => {
    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        { provide: AuthService, useValue: { currentUserValue: { id: 'teacher-1' }, hasRole: () => false } },
        { provide: ReportsService, useValue: {} },
        { provide: TeacherReportService, useValue: {} },
        { provide: TeachersService, useValue: {} },
        { provide: StudentsService, useValue: {} },
        { provide: InstitutionsService, useValue: {} }
      ]
    });

    const component = TestBed.createComponent(DashboardComponent).componentInstance;

    expect(component.getSchoolGradeLabel(8)).toBe('8. sınıf');
    expect(component.getSchoolGradeLabel(null)).toBe('Sınıf belirtilmedi');
  });

  it('bases speed distribution percentages only on students with measured reading speed', () => {
    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        { provide: AuthService, useValue: { currentUserValue: { id: 'teacher-1' }, hasRole: () => false } },
        { provide: ReportsService, useValue: {} },
        { provide: TeacherReportService, useValue: {} },
        { provide: TeachersService, useValue: {} },
        { provide: StudentsService, useValue: {} },
        { provide: InstitutionsService, useValue: {} }
      ]
    });

    const component = TestBed.createComponent(DashboardComponent).componentInstance;
    component.overview = {
      totalStudents: 10,
      studentsAboveAverage: 1,
      studentsAtAverage: 1,
      studentsBelowAverage: 2
    } as any;

    expect(component.getDistributionPct(1)).toBe(25);
  });

  it('loads the institution roster for institution viewers', () => {
    const auth = {
      currentUserValue: { id: 'institution-admin-1', institutionId: 'institution-1', roles: ['InstitutionAdmin'] },
      hasRole: (role: string) => role === 'InstitutionAdmin'
    };
    const institutionStudents = [{
      id: 'student-1',
      firstName: 'Ada',
      lastName: 'Yılmaz',
      email: 'ada@example.test',
      currentLevel: 5,
      learningStyle: 'balanced',
      isActive: true,
      createdAt: new Date()
    }];
    const institutionRoster = jasmine.createSpy('getInstitutionStudentsPage').and.returnValue(of({ items: institutionStudents, totalCount: 21 }));
    const teacherRoster = jasmine.createSpy('getMyStudentsPage').and.returnValue(of({ items: [], totalCount: 0 }));

    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: ReportsService, useValue: {
          getTeacherClassOverviewReport: jasmine.createSpy('getTeacherClassOverviewReport').and.returnValue(of(null)),
          getInstitutionClassOverviewReport: jasmine.createSpy('getInstitutionClassOverviewReport').and.returnValue(of(null))
        } },
        { provide: TeacherReportService, useValue: {
          getInstitutionTimeBasedProgressReport: jasmine.createSpy('getInstitutionTimeBasedProgressReport').and.returnValue(of(null))
        } },
        { provide: TeachersService, useValue: { getMyStudentsPage: teacherRoster } },
        { provide: StudentsService, useValue: { getInstitutionStudentsPage: institutionRoster } },
        { provide: InstitutionsService, useValue: { getInstitutionById: () => of({ code: 'INST-1' }) } }
      ]
    });

    const component = TestBed.createComponent(DashboardComponent).componentInstance;
    component.loadDashboard();

    expect(institutionRoster).toHaveBeenCalledWith(1, 10);
    expect(teacherRoster).not.toHaveBeenCalled();
    expect(component.students).toEqual(institutionStudents);
    expect(component.totalStudentCount).toBe(21);
    expect(TestBed.inject(ReportsService).getInstitutionClassOverviewReport).toHaveBeenCalledWith(
      'institution-1', jasmine.any(Date), jasmine.any(Date));
  });
});
