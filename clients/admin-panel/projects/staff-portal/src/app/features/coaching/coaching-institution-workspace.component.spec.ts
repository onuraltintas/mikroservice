import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingInstitutionService, CoachingInstitutionStudent, CoachingInstitutionTeacher } from './coaching-institution.service';
import { CoachingInstitutionWorkspaceComponent } from './coaching-institution-workspace.component';

describe('CoachingInstitutionWorkspaceComponent', () => {
  it('uses the authenticated institution scope and loads an operational overview', async () => {
    const service = institutionService();
    const fixture = await createFixture(service);
    fixture.detectChanges();

    expect(service.getReadScope).toHaveBeenCalledOnce();
    expect(service.getOverview).toHaveBeenCalledWith(10);
    expect(fixture.nativeElement.textContent).toContain('Kurumunuzun Koçluk özeti');
    expect(fixture.nativeElement.textContent).toContain('12');
  });

  it('loads institution-scoped student data only after selecting the student roster', async () => {
    const students = [student('student-1')];
    const service = institutionService({ students });
    const fixture = await createFixture(service);
    fixture.detectChanges();
    expect(service.getStudentRoster).not.toHaveBeenCalled();

    (fixture.nativeElement.querySelector('[data-testid="institution-students-tab"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(service.getStudentRoster).toHaveBeenCalledWith('institution-1', 1, '', undefined, null);
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('Ayşe Demir');
  });

  it('opens a student report from the institution roster without changing the server authority scope', async () => {
    const service = institutionService({ students: [student('student-1')] });
    const fixture = await createFixture(service);
    fixture.detectChanges();
    fixture.componentInstance.selectSection('students');
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="student-report-student-1"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.selectedStudent()?.userId).toBe('student-1');
    expect(service.getStudentDetail).toHaveBeenCalledWith('student-1');
  });

  it('applies search, teacher, and grade filters on the server and supports paging', async () => {
    const service = institutionService();
    service.getStudentRoster.mockReturnValueOnce(of(studentPage([student('student-1')], 1, 2)))
      .mockReturnValueOnce(of(studentPage([student('student-1')], 1, 2)))
      .mockReturnValueOnce(of(studentPage([student('student-1')], 1, 2)))
      .mockReturnValueOnce(of(studentPage([student('student-1')], 1, 2)))
      .mockReturnValueOnce(of(studentPage([student('student-2')], 2, 2)));
    const fixture = await createFixture(service);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.selectSection('students');
    component.searchStudents('  Ada  ');
    component.setTeacherFilter('teacher-1');
    component.setGradeFilter('8');

    expect(service.getStudentRoster).toHaveBeenLastCalledWith('institution-1', 1, 'Ada', 'teacher-1', 8);
    component.loadMoreStudents();
    expect(service.getStudentRoster).toHaveBeenLastCalledWith('institution-1', 2, 'Ada', 'teacher-1', 8);
    expect(component.students().map(item => item.userId)).toEqual(['student-1', 'student-2']);
  });

  it('searches and pages teachers within the active institution', async () => {
    const service = institutionService({ teachers: [teacher('teacher-1')] });
    const fixture = await createFixture(service);
    fixture.detectChanges();
    fixture.componentInstance.selectSection('teachers');
    fixture.componentInstance.searchTeachers('Zeynep');
    fixture.detectChanges();

    expect(service.getTeacherRoster).toHaveBeenLastCalledWith('institution-1', 1, 'Zeynep');
    expect(fixture.nativeElement.textContent).toContain('Zeynep Koç');
  });

  it('pages the institution teacher filter options for large teacher rosters', async () => {
    const service = institutionService();
    service.getTeacherRoster.mockReturnValue(of(teacherPage([], 101)));
    const fixture = await createFixture(service);
    fixture.detectChanges();
    fixture.componentInstance.selectSection('students');
    expect(fixture.componentInstance.teacherFilterTotalPages()).toBe(2);

    fixture.componentInstance.loadMoreTeacherFilterOptions();

    expect(service.getTeacherRoster).toHaveBeenLastCalledWith('institution-1', 2, '', 100);
  });

  it('does not request institutional data when the server returns no institution scope', async () => {
    const service = institutionService();
    service.getReadScope.mockReturnValue(of({ isGlobal: false, institutionId: null as string | null }));
    const fixture = await createFixture(service);
    fixture.detectChanges();

    expect(service.getOverview).not.toHaveBeenCalled();
    expect(service.getStudentRoster).not.toHaveBeenCalled();
    expect(fixture.componentInstance.errorMessage()).toContain('aktif kurum kapsamı');
  });

  it('shows a recoverable message when overview loading fails', async () => {
    const service = institutionService();
    service.getOverview.mockReturnValue(throwError(() => new Error('offline')));
    const fixture = await createFixture(service);
    fixture.detectChanges();

    expect(fixture.componentInstance.errorMessage()).toContain('özet');
  });
});

async function createFixture(service = institutionService()) {
  TestBed.configureTestingModule({
    imports: [CoachingInstitutionWorkspaceComponent],
    providers: [{ provide: CoachingInstitutionService, useValue: service }]
  });
  await TestBed.compileComponents();
  return TestBed.createComponent(CoachingInstitutionWorkspaceComponent);
}

function institutionService(options: { students?: CoachingInstitutionStudent[]; teachers?: CoachingInstitutionTeacher[] } = {}) {
  return {
    getReadScope: vi.fn(() => of({ isGlobal: false, institutionId: 'institution-1' as string | null })),
    getOverview: vi.fn(() => of({
      totalAssignments: 12, activeAssignments: 4, completedAssignments: 8, cancelledAssignments: 0,
      totalAssignmentStudents: 30, submittedAssignmentStudents: 24, totalExams: 5, totalExamResults: 21,
      totalSessions: 18, upcomingSessions: 3, totalGoals: 9, completedGoals: 4,
      recentAssignments: [{ id: 'assignment-1', teacherId: 'teacher-1', title: 'Haftalık tekrar', status: 'Active', dueDate: '2030-01-05T10:00:00Z', studentCount: 6, submittedStudentCount: 4, createdAt: '2030-01-01T10:00:00Z' }]
    })),
    getStudentRoster: vi.fn(() => of(studentPage(options.students ?? [], 1))),
    getTeacherRoster: vi.fn(() => of(teacherPage(options.teachers ?? [teacher('teacher-1')], 1))),
    getStudentDetail: vi.fn(() => of({
      studentId: 'student-1', totalAssignments: 0, submittedAssignments: 0,
      totalExams: 0, totalSessions: 0, totalGoals: 0, assignments: [], exams: []
    })),
    getStudentHistory: vi.fn(() => of({ items: [], totalCount: 0 }))
  };
}

function student(id: string): CoachingInstitutionStudent {
  return { userId: id, firstName: id === 'student-1' ? 'Ada' : 'Ece', lastName: 'Yılmaz', email: `${id}@example.test`, gradeLevel: 8, teacherName: 'Ayşe Demir', teacherUserId: 'teacher-1' };
}

function teacher(id: string): CoachingInstitutionTeacher {
  return { userId: id, firstName: 'Zeynep', lastName: 'Koç', email: `${id}@example.test` };
}

function studentPage(items: CoachingInstitutionStudent[], pageNumber: number, totalPages = 1) {
  return { students: items, totalCount: totalPages * 25 };
}

function teacherPage(items: CoachingInstitutionTeacher[], totalCount: number) {
  return { teachers: items, totalCount };
}
