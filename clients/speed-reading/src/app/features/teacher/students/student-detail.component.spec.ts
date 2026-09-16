import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { StudentDetailComponent } from './student-detail.component';
import { AuthService } from '../../../core/services/auth.service';
import { ReportsService } from '../../../core/services/reports.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { StudentsService } from '../../../core/services/students.service';
import { ToasterService } from '../../../core/services/toaster.service';

describe('StudentDetailComponent', () => {
  it('uses the service trend points when preparing detail charts', () => {
    TestBed.configureTestingModule({
      imports: [StudentDetailComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' }, queryParamMap: { get: () => null } } } },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
        { provide: AuthService, useValue: { currentUserValue: { id: 'teacher-1' }, hasRole: () => false } },
        { provide: ReportsService, useValue: {} },
        { provide: TeachersService, useValue: { getMyStudents: () => of([]) } },
        { provide: StudentsService, useValue: { getInstitutionStudents: () => of([]) } },
        { provide: ToasterService, useValue: { error: jasmine.createSpy('error'), success: jasmine.createSpy('success') } }
      ]
    });

    const component = TestBed.createComponent(StudentDetailComponent).componentInstance;
    component.student = {
      id: 'student-1',
      firstName: 'Ada',
      lastName: 'Yılmaz',
      email: 'ada@example.test',
      currentLevel: 5,
      learningStyle: 'balanced',
      isActive: true,
      createdAt: new Date()
    };

    (component as any).applyReport({
      studentReports: {
        dashboard: { totalActivities: 4, exercisesCompleted: 2, goalCompletionRate: 50 },
        readingSpeed: { statistics: { averageWPM: 260 }, wpmTrendChart: { data: [{ name: '2026-09-01', value: 240 }, { name: '2026-09-02', value: 280 }] } },
        comprehension: { overallComprehension: 80, comprehensionTrend: { data: [{ name: '2026-09-01', value: 75 }] } },
        activity: { studyTime: { totalMinutes: 20 }, currentStreak: { days: 2 }, recentActivities: [] },
        series: { activeSeries: [] }
      }
    });

    expect(component.kdpChartData).toEqual([{ name: 'KDP', series: [
      { name: '2026-09-01', value: 240 },
      { name: '2026-09-02', value: 280 }
    ] }]);
  });

  it('uses institution scope for an institution administrator detail report', () => {
    const reportRequest = jasmine.createSpy('getTeacherStudentDetailReport').and.returnValue(of(null));
    const student = {
      id: 'student-1',
      firstName: 'Ada',
      lastName: 'Yılmaz',
      email: 'ada@example.test',
      currentLevel: 5,
      learningStyle: 'balanced',
      isActive: true,
      createdAt: new Date()
    };

    TestBed.configureTestingModule({
      imports: [StudentDetailComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' }, queryParamMap: { get: () => null } } } },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
        { provide: AuthService, useValue: { currentUserValue: { id: 'institution-admin-1' }, hasRole: (role: string) => role === 'InstitutionAdmin' } },
        { provide: ReportsService, useValue: { getTeacherStudentDetailReport: reportRequest } },
        { provide: TeachersService, useValue: { getMyStudents: () => of([]) } },
        { provide: StudentsService, useValue: { getInstitutionStudents: () => of([student]) } },
        { provide: ToasterService, useValue: { error: jasmine.createSpy('error'), success: jasmine.createSpy('success') } }
      ]
    });

    const component = TestBed.createComponent(StudentDetailComponent).componentInstance;
    component.loadStudentData('student-1');

    expect(reportRequest).toHaveBeenCalled();
    expect(reportRequest.calls.mostRecent().args[0]).toBe('');
  });
});
