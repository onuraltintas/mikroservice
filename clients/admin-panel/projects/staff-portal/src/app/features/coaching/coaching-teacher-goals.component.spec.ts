import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { By } from '@angular/platform-browser';
import { StudentGoalTargetComponent } from '../../../../../../src/app/features/coaching-portal/pages/student-goal-target.component';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherGoal, CoachingTeacherGoalsService } from './coaching-teacher-goals.service';
import { CoachingTeacherGoalsComponent } from './coaching-teacher-goals.component';

describe('CoachingTeacherGoalsComponent', () => {
  it('keeps the latest version when a catalog-only target change has no score target', () => {
    const service = goalsService([goal('goal-1')]); const fixture = createFixture(service); fixture.detectChanges();
    const component = fixture.componentInstance; component.editGoal(component.goals()[0]);
    component.goalTargetChanged({ goalId: 'goal-1', version: 5, targetSchoolId: 'school-1', targetUniversityProgramId: null, canEdit: true, scoreTarget: null });
    component.saveGoal(); expect(service.updateGoal).toHaveBeenCalledWith('goal-1', expect.objectContaining({ expectedVersion: 5, targetScore: 85 }));
  });
  it('keeps the stale form visible and asks for a fresh goal after a conflict', () => {
    const service = goalsService([goal('goal-1')]); service.updateGoal.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 409 })));
    const fixture = createFixture(service); fixture.detectChanges(); const component = fixture.componentInstance;
    component.editGoal(goal('goal-1')); component.form.title = 'Benim değişikliğim'; component.saveGoal();
    expect(component.errorMessage()).toContain('Listeyi yenileyip'); expect(component.form.title).toBe('Benim değişikliğim'); expect(component.isSaving()).toBe(false);
    fixture.detectChanges();
    const refresh = fixture.nativeElement.querySelector('[data-testid="reload-conflicted-goals"]') as HTMLButtonElement;
    expect(refresh).toBeTruthy(); service.getTeacherGoals.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 503 }))); refresh.click(); fixture.detectChanges();
    const retry = fixture.nativeElement.querySelector('[data-testid="reload-conflicted-goals"]') as HTMLButtonElement;
    expect(retry).toBeTruthy(); retry.click();
    expect(service.getTeacherGoals).toHaveBeenCalledTimes(3); expect(component.editingGoalId()).toBeNull();
  });
  it('preserves a newly saved score target during subsequent title editing', () => {
    const service = goalsService([goal('goal-1')]);
    const fixture = createFixture(service); fixture.detectChanges();
    fixture.componentInstance.goalTargetChanged({ goalId: 'goal-1', version: 4, targetSchoolId: null, targetUniversityProgramId: null,
      canEdit: true, scoreTarget: { targetScore: 450, maxScore: 500, examType: 'LGS', subject: null } });
    fixture.componentInstance.editGoal(fixture.componentInstance.goals()[0]);
    fixture.componentInstance.form.title = 'Başlık değişti'; fixture.componentInstance.saveGoal();
    expect(service.updateGoal).toHaveBeenCalledWith('goal-1', expect.objectContaining({ targetScore: 450, targetExamType: 4, expectedVersion: 4 }));
  });
  it('protects a pending target save from list refresh and goal replacement', () => {
    const service = goalsService([goal('goal-1')]);
    const fixture = createFixture(service); fixture.detectChanges();
    const target = fixture.debugElement.query(By.directive(StudentGoalTargetComponent)).componentInstance;
    target.saving.set(true);
    expect(fixture.componentInstance.canLeavePage()).toBe(false);
    fixture.componentInstance.loadGoals();
    expect(service.getTeacherGoals).toHaveBeenCalledTimes(1);
    fixture.componentInstance.form.studentId = 'student-1'; fixture.componentInstance.form.title = 'Yeni';
    fixture.componentInstance.saveGoal();
    expect(service.createGoal).not.toHaveBeenCalled();
  });
  it('loads only the authenticated teacher goals and displays authorized student context', () => {
    const goals = goalsService([goal('goal-1')]);
    const students = studentService();
    const fixture = createFixture(goals, students);
    fixture.detectChanges();

    expect(goals.getTeacherGoals).toHaveBeenCalledWith('teacher-1', 1, 25);
    expect(fixture.nativeElement.textContent).toContain('LGS hazırlığı');
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('Sınav hazırlığı');
    expect(fixture.nativeElement.textContent).toContain('65%');
  });

  it('maps API enum categories to their real values when editing a goal', () => {
    const fixture = createFixture(goalsService([goal('goal-1')]));
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.editGoal(goal('goal-1'));

    expect(component.form.category).toBe(1);
    expect(component.form.targetExamType).toBe(4);
    expect(component.goalCategoryLabel('SubjectMastery')).toBe('Ders hakimiyeti');
  });

  it('creates a valid teacher goal with an idempotency key', () => {
    const goals = goalsService([]);
    const fixture = createFixture(goals);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.form.studentId = 'student-1';
    component.form.title = '  Haftalık tekrar  ';
    component.form.targetDate = '2030-01-05T10:00';
    component.form.targetScore = 82;
    component.saveGoal();

    expect(goals.createGoal).toHaveBeenCalledOnce();
    const [request, key] = goals.createGoal.mock.calls[0] as unknown as [Record<string, unknown>, string];
    expect(request).toMatchObject({
      teacherId: 'teacher-1', studentId: 'student-1', title: 'Haftalık tekrar', category: 1, targetScore: 82
    });
    expect(key).toEqual(expect.any(String));
    expect(component.successMessage()).toBe('Hedef oluşturuldu.');
  });

  it('rejects missing student/title, invalid target dates and out-of-range scores', () => {
    const goals = goalsService([]);
    const fixture = createFixture(goals);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.saveGoal();
    expect(component.errorMessage()).toContain('zorunludur');

    component.form.studentId = 'student-1';
    component.form.title = 'Hedef';
    component.form.targetDate = 'not-a-date';
    component.saveGoal();
    expect(component.errorMessage()).toContain('geçerli olmalıdır');

    component.form.targetDate = '';
    component.form.targetScore = 1000;
    component.saveGoal();
    expect(component.errorMessage()).toContain('0 ile 999,99');
    expect(goals.createGoal).not.toHaveBeenCalled();
  });

  it('edits supported fields while preserving the original student relationship', () => {
    const goals = goalsService([goal('goal-1')]);
    const fixture = createFixture(goals);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.editGoal(goal('goal-1'));
    component.form.title = 'Yeni akademik hedef';
    component.form.targetSubject = 'Türkçe';
    component.saveGoal();

    expect(goals.updateGoal).toHaveBeenCalledOnce();
    const [goalId, request] = goals.updateGoal.mock.calls[0] as unknown as [string, Record<string, unknown>];
    expect(goalId).toBe('goal-1');
    expect(request).toMatchObject({ title: 'Yeni akademik hedef', targetExamType: 4, targetSubject: 'Türkçe' });
    expect(request['studentId']).toBeUndefined();
  });
});

function createFixture(goals = goalsService([]), students = studentService(), teacherId: string | null = 'teacher-1') {
  TestBed.configureTestingModule({
    imports: [CoachingTeacherGoalsComponent],
    providers: [
      provideHttpClient(),
      { provide: StaffAuthService, useValue: { getCurrentUserId: () => teacherId } },
      { provide: CoachingTeacherGoalsService, useValue: goals },
      { provide: CoachingTeacherStudentsService, useValue: students }
    ]
  });
  return TestBed.createComponent(CoachingTeacherGoalsComponent);
}

function goal(id: string): CoachingTeacherGoal {
  return {
    id, studentId: 'student-1', title: 'LGS hazırlığı', description: 'Haftalık deneme çöz',
    category: 'ExamPreparation', targetDate: '2030-01-05T10:00:00Z', targetScore: 85,
    targetExamType: 'LGS', targetSubject: 'Matematik', progress: 65, isCompleted: false
  };
}

function goalsService(items: CoachingTeacherGoal[]) {
  return {
    getTeacherGoals: vi.fn(() => of({ items, pageNumber: 1, pageSize: 25, totalCount: items.length, totalPages: 1 })),
    createGoal: vi.fn(() => of({ goalId: 'goal-new' })),
    updateGoal: vi.fn(() => of({ goalId: 'goal-1', title: 'Updated' }))
  };
}

function studentService() {
  return {
    getMyStudents: vi.fn((_page = 1, _size = 25, _search?: string, _ids?: readonly string[]) => of({
      items: [{ userId: 'student-1', firstName: 'Ada', lastName: 'Yılmaz', fullName: 'Ada Yılmaz', gradeLevel: 8, assignmentStartDate: '2030-01-01T00:00:00Z' }],
      pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1
    }))
  };
}

beforeEach(() => TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn()}}));
