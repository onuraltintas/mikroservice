import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherGoal, CoachingTeacherGoalsService } from './coaching-teacher-goals.service';
import { CoachingTeacherGoalsComponent } from './coaching-teacher-goals.component';

describe('CoachingTeacherGoalsComponent', () => {
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
