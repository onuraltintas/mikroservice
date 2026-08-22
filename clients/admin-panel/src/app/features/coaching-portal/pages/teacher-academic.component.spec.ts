import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { TeacherAcademicComponent } from './teacher-academic.component';

describe('TeacherAcademicComponent', () => {
  it('loads teacher exams, goals and roster', () => {
    const service = academicService();

    TestBed.configureTestingModule({
      imports: [TeacherAcademicComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal({ id: 'teacher-1', role: 'Teacher' }) } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const fixture = TestBed.createComponent(TeacherAcademicComponent);
    fixture.detectChanges();

    expect(service.getTeacherExams).toHaveBeenCalledWith('teacher-1', 1, 25);
    expect(service.getTeacherGoals).toHaveBeenCalledWith('teacher-1', 1, 25);
    expect(service.getTeacherStudents).toHaveBeenCalledWith(1, 100);
    expect(fixture.nativeElement.textContent).toContain('Sınav yönetimi');
    expect(fixture.nativeElement.textContent).toContain('Hedef yönetimi');
  });

  it('creates a teacher exam with normalized values', () => {
    const service = academicService();
    TestBed.configureTestingModule({
      imports: [TeacherAcademicComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal({ id: 'teacher-1', role: 'Teacher' }) } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(TeacherAcademicComponent).componentInstance;
    component.ngOnInit();
    component.examForm.title = '  LGS denemesi  ';
    component.examForm.type = 4;
    component.examForm.examDate = '2030-02-01T10:00';
    component.examForm.maxScore = 500;
    component.saveExam();

    expect(service.createTeacherExam).toHaveBeenCalledWith(expect.objectContaining({
      teacherId: 'teacher-1',
      title: 'LGS denemesi',
      type: 4,
      maxScore: 500,
      examDate: new Date('2030-02-01T10:00').toISOString()
    }), expect.any(String));
  });

  it('adds a result to the selected exam and exposes the correction workflow', () => {
    const service = academicService();
    TestBed.configureTestingModule({
      imports: [TeacherAcademicComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal({ id: 'teacher-1', role: 'Teacher' }) } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(TeacherAcademicComponent).componentInstance;
    component.ngOnInit();
    const exam = { id: 'exam-1', title: 'LGS', examType: 'LGS', examDate: '2030-02-01T10:00:00Z', maxScore: 500, resultCount: 0 };
    component.selectExamResults(exam);
    component.resultForm.studentId = 'student-1';
    component.resultForm.score = 420;
    component.resultForm.correctAnswers = 80;
    component.resultForm.wrongAnswers = 10;
    component.resultForm.emptyAnswers = 0;
    component.saveExamResult();

    expect(service.addTeacherExamResult).toHaveBeenCalledWith('exam-1', expect.objectContaining({
      studentId: 'student-1',
      score: 420,
      correctAnswers: 80
    }), expect.any(String));
  });

  it('preserves subject scores when correcting an existing result', () => {
    const service = academicService();
    TestBed.configureTestingModule({
      imports: [TeacherAcademicComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal({ id: 'teacher-1', role: 'Teacher' }) } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(TeacherAcademicComponent).componentInstance;
    const exam = { id: 'exam-1', title: 'LGS', examType: 'LGS', examDate: '2030-02-01T10:00:00Z', maxScore: 500, resultCount: 1 };
    component.selectExamResults(exam);
    component.examDetail.set({ ...exam, resultPageNumber: 1, resultPageSize: 25, resultTotalPages: 1, results: [{ id: 'result-1', studentId: 'student-1', score: 420, correctAnswers: 80, wrongAnswers: 10, emptyAnswers: 0, subjectScores: { Matematik: 90 }, ranking: 4, teacherNotes: 'Not' }] });
    component.editExamResult(component.examDetail()!.results[0]);
    component.resultForm.score = 430;
    component.saveExamResult();

    expect(service.updateTeacherExamResult).toHaveBeenCalledWith('exam-1', 'result-1', expect.objectContaining({
      score: 430,
      subjectScores: { Matematik: 90 }
    }));
  });
});

function academicService() {
  return {
    getTeacherExams: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
    getTeacherGoals: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
    getTeacherStudents: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })),
    createTeacherExam: vi.fn(() => of({ examId: 'exam-1' })),
    updateTeacherExam: vi.fn(() => of({ examId: 'exam-1', examDate: '2030-02-01T10:00:00Z', maxScore: 500 })),
    createTeacherGoal: vi.fn(() => of({ goalId: 'goal-1' })),
    updateTeacherGoal: vi.fn(() => of({ goalId: 'goal-1', title: 'Hedef' })),
    getTeacherExamDetail: vi.fn(() => of({ id: 'exam-1', title: 'LGS', examType: 'LGS', examDate: '2030-02-01T10:00:00Z', maxScore: 500, resultCount: 0, resultPageNumber: 1, resultPageSize: 25, resultTotalPages: 1, results: [] })),
    addTeacherExamResult: vi.fn(() => of({ message: 'ok' })),
    updateTeacherExamResult: vi.fn(() => of({ examId: 'exam-1', resultId: 'result-1', score: 420 }))
  };
}
