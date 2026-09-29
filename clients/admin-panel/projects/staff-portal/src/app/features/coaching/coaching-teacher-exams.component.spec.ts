import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherExam, CoachingTeacherExamDetail, CoachingTeacherExamsService } from './coaching-teacher-exams.service';
import { CoachingTeacherExamsComponent } from './coaching-teacher-exams.component';

describe('CoachingTeacherExamsComponent', () => {
  it('loads only the authenticated teacher exams and active student roster', () => {
    const exams = examsService([exam('exam-1')]);
    const students = studentService();
    const fixture = createFixture(exams, students);
    fixture.detectChanges();

    expect(exams.getTeacherExams).toHaveBeenCalledWith('teacher-1', 1, 25);
    expect(students.getMyStudents).toHaveBeenCalledWith(1, 100, undefined);
    expect(fixture.nativeElement.textContent).toContain('LGS Denemesi');
    expect(fixture.componentInstance.studentNames()['student-1']).toBe('Ada Yılmaz');
  });

  it('loads the next exam page without discarding previously loaded exams', () => {
    const exams = examsService([exam('exam-1')]);
    exams.getTeacherExams.mockReturnValueOnce(of(page([exam('exam-1')], 1, 2)))
      .mockReturnValueOnce(of(page([exam('exam-2')], 2, 2)));
    const fixture = createFixture(exams);
    fixture.detectChanges();
    fixture.componentInstance.loadMoreExams();

    expect(exams.getTeacherExams).toHaveBeenLastCalledWith('teacher-1', 2, 25);
    expect(fixture.componentInstance.exams().map(item => item.id)).toEqual(['exam-1', 'exam-2']);
  });

  it('creates a valid future exam with a fresh idempotency key', () => {
    const exams = examsService([]);
    const fixture = createFixture(exams);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.createExam();
    component.form.title = '  Yeni sınav  ';
    component.form.examDate = '2030-01-05T10:00';
    component.form.maxScore = 100;
    component.saveExam();

    expect(exams.createExam).toHaveBeenCalledOnce();
    const [request, key] = exams.createExam.mock.calls[0] as unknown as [Record<string, unknown>, string];
    expect(request).toMatchObject({ teacherId: 'teacher-1', title: 'Yeni sınav', type: 1, maxScore: 100 });
    expect(key).toEqual(expect.any(String));
    expect(component.successMessage()).toBe('Sınav oluşturuldu.');
  });

  it('rejects invalid exam title, past date, type, and score before calling the API', () => {
    const exams = examsService([]);
    const fixture = createFixture(exams);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.saveExam();
    expect(component.errorMessage()).toContain('zorunludur');
    component.form.title = 'Sınav';
    component.form.examDate = '2020-01-01T10:00';
    component.saveExam();
    expect(component.errorMessage()).toContain('gelecekte');
    component.form.examDate = '2030-01-01T10:00';
    component.form.type = 999;
    component.saveExam();
    expect(component.errorMessage()).toContain('tür');
    component.form.type = 1;
    component.form.maxScore = 1000;
    component.saveExam();
    expect(component.errorMessage()).toContain('0,01 ile 999,99');
    expect(exams.createExam).not.toHaveBeenCalled();
  });

  it('edits a teacher-owned exam without changing ownership', () => {
    const exams = examsService([exam('exam-1')]);
    const fixture = createFixture(exams);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.editExam(exam('exam-1'));
    component.form.title = 'Güncel sınav';
    component.form.subject = 'Matematik';
    component.saveExam();

    expect(exams.updateExam).toHaveBeenCalledOnce();
    const [examId, request] = exams.updateExam.mock.calls[0] as unknown as [string, Record<string, unknown>];
    expect(examId).toBe('exam-1');
    expect(request).toMatchObject({ examId: 'exam-1', title: 'Güncel sınav', subject: 'Matematik' });
    expect(request['teacherId']).toBeUndefined();
  });

  it('allows editing an existing exam that is already in the past', () => {
    const pastExam = { ...exam('exam-1'), examDate: '2020-01-05T10:00:00Z' };
    const exams = examsService([pastExam]);
    const fixture = createFixture(exams);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.editExam(pastExam);
    component.form.title = 'Geçmiş sınavı düzelt';
    component.saveExam();

    expect(exams.updateExam).toHaveBeenCalledOnce();
    expect(component.errorMessage()).toBeNull();
  });

  it('shows result detail and resolves the student name', () => {
    const detail = examDetail('exam-1', [result('result-1')]);
    const exams = examsService([exam('exam-1')]);
    exams.getExamDetail.mockReturnValue(of(detail));
    const fixture = createFixture(exams);
    fixture.detectChanges();
    fixture.componentInstance.openResults(exam('exam-1'));
    fixture.detectChanges();

    expect(exams.getExamDetail).toHaveBeenCalledWith('exam-1', 1, 25);
    expect(fixture.nativeElement.textContent).toContain('Sınav sonuçları');
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('80 / 100');
  });

  it('validates and adds a result with an idempotency key', () => {
    const exams = examsService([exam('exam-1')]);
    exams.getExamDetail.mockReturnValue(of(examDetail('exam-1', [])));
    const fixture = createFixture(exams);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.openResults(exam('exam-1'));
    component.resultForm.studentId = 'student-1';
    component.resultForm.score = 80;
    component.resultForm.subjectScoresText = '{"Matematik":80}';
    component.saveExamResult();

    expect(exams.addExamResult).toHaveBeenCalledOnce();
    const [examId, request, key] = exams.addExamResult.mock.calls[0] as unknown as [string, Record<string, unknown>, string];
    expect(examId).toBe('exam-1');
    expect(request).toMatchObject({ studentId: 'student-1', score: 80, subjectScores: { Matematik: 80 } });
    expect(key).toEqual(expect.any(String));
  });

  it('rejects malformed or out-of-range subject score JSON and duplicate-invalid score data', () => {
    const exams = examsService([exam('exam-1')]);
    exams.getExamDetail.mockReturnValue(of(examDetail('exam-1', [])));
    const fixture = createFixture(exams);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.openResults(exam('exam-1'));
    component.resultForm.studentId = 'student-1';
    component.resultForm.score = 101;
    component.saveExamResult();
    expect(component.errorMessage()).toContain('0 ile 100');
    component.resultForm.score = 80;
    component.resultForm.subjectScoresText = '{broken';
    component.saveExamResult();
    expect(component.errorMessage()).toContain('geçerli JSON');
    component.resultForm.subjectScoresText = '{"Matematik":101}';
    component.saveExamResult();
    expect(component.errorMessage()).toContain('0-100');
    expect(exams.addExamResult).not.toHaveBeenCalled();
  });

  it('updates a result without changing the linked student', () => {
    const existing = result('result-1');
    const exams = examsService([exam('exam-1')]);
    exams.getExamDetail.mockReturnValue(of(examDetail('exam-1', [existing])));
    const fixture = createFixture(exams);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.openResults(exam('exam-1'));
    component.editExamResult(existing);
    component.resultForm.score = 85;
    component.saveExamResult();

    const [examId, resultId, request] = exams.updateExamResult.mock.calls[0] as unknown as [string, string, Record<string, unknown>];
    expect(examId).toBe('exam-1');
    expect(resultId).toBe('result-1');
    expect(request).toMatchObject({ resultId: 'result-1', score: 85 });
    expect(request['studentId']).toBeUndefined();
  });

  it('keeps existing data visible and reports failed exam loads', () => {
    const exams = examsService([]);
    exams.getTeacherExams.mockReturnValue(throwError(() => new Error('network')));
    const fixture = createFixture(exams);
    fixture.detectChanges();
    expect(fixture.componentInstance.errorMessage()).toContain('Sınavlar yüklenemedi');
  });
});

function createFixture(exams = examsService([]), students = studentService(), teacherId: string | null = 'teacher-1') {
  TestBed.configureTestingModule({
    imports: [CoachingTeacherExamsComponent],
    providers: [
      { provide: StaffAuthService, useValue: { getCurrentUserId: () => teacherId } },
      { provide: CoachingTeacherExamsService, useValue: exams },
      { provide: CoachingTeacherStudentsService, useValue: students }
    ]
  });
  return TestBed.createComponent(CoachingTeacherExamsComponent);
}

function exam(id: string): CoachingTeacherExam {
  return {
    id, title: 'LGS Denemesi', examType: 'LGS', examDate: '2030-01-05T10:00:00Z',
    maxScore: 100, subject: 'Genel', resultCount: 1, description: 'Deneme açıklaması',
    durationMinutes: 90, targetGradeLevel: 8
  };
}

function result(id: string) {
  return {
    id, studentId: 'student-1', score: 80, correctAnswers: 20,
    wrongAnswers: 3, emptyAnswers: 2, subjectScores: { Matematik: 80 }, ranking: 4, teacherNotes: 'İyi'
  };
}

function examDetail(id: string, results: CoachingTeacherExamDetail['results']): CoachingTeacherExamDetail {
  return { ...exam(id), results, resultPageNumber: 1, resultPageSize: 25, resultTotalPages: 1 };
}

function page<T>(items: T[], pageNumber: number, totalPages = 1) {
  return { items, pageNumber, pageSize: 25, totalCount: totalPages * 25, totalPages };
}

function examsService(items: CoachingTeacherExam[]) {
  return {
    getTeacherExams: vi.fn(() => of(page(items, 1))),
    createExam: vi.fn(() => of({ examId: 'exam-new' })),
    updateExam: vi.fn(() => of({ examId: 'exam-1', examDate: '2030-01-05T10:00:00Z', maxScore: 100 })),
    getExamDetail: vi.fn(() => of(examDetail('exam-1', []))),
    addExamResult: vi.fn(() => of({ message: 'ok' })),
    updateExamResult: vi.fn(() => of({ examId: 'exam-1', resultId: 'result-1', score: 85 }))
  };
}

function studentService() {
  return {
    getMyStudents: vi.fn(() => of({
      items: [{ userId: 'student-1', firstName: 'Ada', lastName: 'Yılmaz', fullName: 'Ada Yılmaz', gradeLevel: 8, assignmentStartDate: '2030-01-01T00:00:00Z' }],
      pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1
    }))
  };
}
