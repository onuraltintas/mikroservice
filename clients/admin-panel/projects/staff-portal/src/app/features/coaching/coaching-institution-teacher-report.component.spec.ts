import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import {
  CoachingInstitutionService,
  CoachingInstitutionStudent,
  CoachingInstitutionStudentPage,
  CoachingInstitutionTeacher,
  CoachingInstitutionTeacherAnalytics,
  CoachingInstitutionTeacherOverview
} from './coaching-institution.service';
import { CoachingInstitutionTeacherReportComponent } from './coaching-institution-teacher-report.component';

describe('CoachingInstitutionTeacherReportComponent', () => {
  it('loads teacher operations, current-period analytics, and students in the current institution', async () => {
    const service = teacherService({ students: [student('student-1')] });
    const fixture = await createFixture(service);
    fixture.detectChanges();

    expect(service.getTeacherOverview).toHaveBeenCalledWith('teacher-1');
    expect(service.getTeacherAnalytics).toHaveBeenCalledWith('teacher-1');
    expect(service.getStudentRoster).toHaveBeenCalledWith('institution-1', 1, '', 'teacher-1', null);
    expect(fixture.nativeElement.textContent).toContain('Zeynep Koç');
    expect(fixture.nativeElement.textContent).toContain('Son 30 gün');
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');
  });

  it('searches and pages only this teacher’s current-institution students', async () => {
    const service = teacherService();
    service.getStudentRoster.mockReturnValue(of(studentPage([student('student-1')], 26)));
    const fixture = await createFixture(service);
    fixture.detectChanges();
    fixture.componentInstance.searchStudents('  Ada  ');

    expect(service.getStudentRoster).toHaveBeenLastCalledWith('institution-1', 1, 'Ada', 'teacher-1', null);
    expect(fixture.componentInstance.studentTotalPages()).toBe(2);
    fixture.componentInstance.nextStudentsPage();
    expect(service.getStudentRoster).toHaveBeenLastCalledWith('institution-1', 2, 'Ada', 'teacher-1', null);
  });

  it('reports summary and roster failures independently and supports retry', async () => {
    const service = teacherService();
    service.getTeacherOverview.mockReturnValue(throwError(() => new Error('offline')));
    service.getTeacherAnalytics.mockReturnValue(throwError(() => new Error('offline')));
    service.getStudentRoster.mockReturnValue(throwError(() => new Error('offline')));
    const fixture = await createFixture(service);
    fixture.detectChanges();

    expect(fixture.componentInstance.overviewError()).toContain('özeti');
    expect(fixture.componentInstance.analyticsError()).toContain('analizi');
    expect(fixture.componentInstance.studentsError()).toContain('öğrencileri');

    service.getTeacherOverview.mockReturnValue(of(overview()));
    service.getTeacherAnalytics.mockReturnValue(of(analytics()));
    service.getStudentRoster.mockReturnValue(of(studentPage([], 0)));
    fixture.componentInstance.retryOverview();
    fixture.componentInstance.retryAnalytics();
    fixture.componentInstance.retryStudents();
    expect(fixture.componentInstance.overviewError()).toBeNull();
    expect(fixture.componentInstance.analyticsError()).toBeNull();
    expect(fixture.componentInstance.studentsError()).toBeNull();
  });

  it('requests opening a student report and supports returning to the teacher roster', async () => {
    const fixture = await createFixture(teacherService({ students: [student('student-1')] }));
    fixture.detectChanges();
    const studentSelected = vi.fn();
    const back = vi.fn();
    fixture.componentInstance.studentSelected.subscribe(studentSelected);
    fixture.componentInstance.back.subscribe(back);

    (fixture.nativeElement.querySelector('[data-testid="open-student-report-student-1"]') as HTMLButtonElement).click();
    (fixture.nativeElement.querySelector('[data-testid="back-to-teachers"]') as HTMLButtonElement).click();

    expect(studentSelected).toHaveBeenCalledWith(expect.objectContaining({ userId: 'student-1' }));
    expect(back).toHaveBeenCalledOnce();
  });
});

async function createFixture(service = teacherService()) {
  TestBed.configureTestingModule({
    imports: [CoachingInstitutionTeacherReportComponent],
    providers: [{ provide: CoachingInstitutionService, useValue: service }]
  });
  await TestBed.compileComponents();
  const fixture = TestBed.createComponent(CoachingInstitutionTeacherReportComponent);
  fixture.componentRef.setInput('teacher', { userId: 'teacher-1', firstName: 'Zeynep', lastName: 'Koç', email: 'zeynep@example.test' });
  fixture.componentRef.setInput('institutionId', 'institution-1');
  return fixture;
}

function teacherService(options: { students?: CoachingInstitutionStudent[] } = {}) {
  return {
    getTeacherOverview: vi.fn(() => of(overview())),
    getTeacherAnalytics: vi.fn(() => of(analytics())),
    getStudentRoster: vi.fn(() => of(studentPage(options.students ?? [], options.students?.length ?? 0)))
  };
}

function overview(): CoachingInstitutionTeacherOverview {
  return {
    teacherId: 'teacher-1', totalAssignments: 8, totalAssignmentStudents: 24,
    submittedAssignmentStudents: 18, totalExams: 4, totalSessions: 7
  };
}

function analytics(): CoachingInstitutionTeacherAnalytics {
  return {
    teacherId: 'teacher-1', currentPeriod: { assignments: 3, exams: 2, sessions: 4 },
    previousPeriod: { assignments: 2, exams: 1, sessions: 3 }, lowResults: 1, mediumResults: 2, highResults: 4
  };
}

function student(id: string): CoachingInstitutionStudent {
  return { userId: id, firstName: id === 'student-1' ? 'Ada' : 'Ece', lastName: 'Yılmaz', email: `${id}@example.test`, gradeLevel: 8, teacherName: 'Ayşe Demir', teacherUserId: 'teacher-1' };
}

function studentPage(students: CoachingInstitutionStudent[], totalCount: number): CoachingInstitutionStudentPage {
  return { students, totalCount };
}
