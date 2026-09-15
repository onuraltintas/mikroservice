import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { AuthService } from '../../../core/services/auth.service';
import { ReportsService } from '../../../core/services/reports.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { StudentsService } from '../../../core/services/students.service';
import { InstitutionsService } from '../../../core/services/institutions.service';

describe('DashboardComponent', () => {
  it('loads the institution roster for institution viewers', () => {
    const auth = {
      currentUserValue: { id: 'institution-admin-1', roles: ['InstitutionAdmin'] },
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
    const institutionRoster = jasmine.createSpy('getInstitutionStudents').and.returnValue(of(institutionStudents));
    const teacherRoster = jasmine.createSpy('getMyStudents').and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: ReportsService, useValue: {
          getTeacherClassOverviewReport: () => of(null),
          getTeacherTimeBasedProgressReport: () => of(null)
        } },
        { provide: TeachersService, useValue: { getMyStudents: teacherRoster } },
        { provide: StudentsService, useValue: { getInstitutionStudents: institutionRoster } },
        { provide: InstitutionsService, useValue: { getInstitutionById: () => of({ code: 'INST-1' }) } }
      ]
    });

    const component = TestBed.createComponent(DashboardComponent).componentInstance;
    component.loadDashboard();

    expect(institutionRoster).toHaveBeenCalled();
    expect(teacherRoster).not.toHaveBeenCalled();
    expect(component.students).toEqual(institutionStudents);
  });
});
