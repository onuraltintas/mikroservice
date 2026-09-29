import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingTeacherExamsService } from './coaching-teacher-exams.service';

describe('CoachingTeacherExamsService', () => {
  let http: HttpTestingController;
  let service: CoachingTeacherExamsService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(CoachingTeacherExamsService);
  });

  afterEach(() => http.verify());

  it('loads a bounded teacher-scoped exam page', () => {
    service.getTeacherExams('teacher/1', 2, 10).subscribe();
    const request = http.expectOne('/api/exams/teacher/teacher%2F1?pageNumber=2&pageSize=10');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 0 });
  });

  it('creates exams with an idempotency key and only supported create fields', () => {
    service.createExam({
      teacherId: 'teacher-1', title: '  LGS denemesi  ', type: 4,
      examDate: '2030-01-05T10:00:00Z', maxScore: 100, description: '  Açıklama  '
    }, 'exam-key').subscribe();
    const request = http.expectOne('/api/exams');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('exam-key');
    expect(request.request.body).toEqual({
      teacherId: 'teacher-1', title: 'LGS denemesi', type: 4,
      examDate: '2030-01-05T10:00:00Z', maxScore: 100, institutionId: null, description: 'Açıklama'
    });
    request.flush({ examId: 'exam-1' });
  });

  it('updates an exam using the server-supported editable fields', () => {
    service.updateExam('exam-1', {
      examId: 'exam-1', title: '  Deneme 2  ', type: 1, subject: '  Matematik  ',
      description: '  Not  ', examDate: '2030-02-01T10:00:00Z', durationMinutes: 90,
      maxScore: 120, targetGradeLevel: 8
    }).subscribe();
    const request = http.expectOne('/api/exams/exam-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      examId: 'exam-1', title: 'Deneme 2', type: 1, subject: 'Matematik', description: 'Not',
      examDate: '2030-02-01T10:00:00Z', durationMinutes: 90, maxScore: 120, targetGradeLevel: 8
    });
    request.flush({ examId: 'exam-1', examDate: '2030-02-01T10:00:00Z', maxScore: 120 });
  });

  it('loads paged results from the teacher-authorized exam detail endpoint', () => {
    service.getExamDetail('exam-1', 3, 20).subscribe();
    const request = http.expectOne('/api/exams/exam-1/teacher-detail?pageNumber=3&pageSize=20');
    expect(request.request.method).toBe('GET');
    request.flush({ id: 'exam-1', results: [], resultPageNumber: 3, resultPageSize: 20, resultTotalPages: 3 });
  });

  it('adds a student result with an idempotency key and normalized notes', () => {
    service.addExamResult('exam-1', {
      examId: 'exam-1', studentId: 'student-1', score: 80, correctAnswers: 20,
      wrongAnswers: 3, emptyAnswers: 2, subjectScores: { Matematik: 80 }, ranking: 4, notes: '  İyi  '
    }, 'result-key').subscribe();
    const request = http.expectOne('/api/exams/exam-1/results');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('result-key');
    expect(request.request.body).toMatchObject({ examId: 'exam-1', studentId: 'student-1', notes: 'İyi' });
    request.flush({ message: 'ok' });
  });

  it('updates a result without sending a replacement student identity', () => {
    service.updateExamResult('exam-1', 'result-1', {
      examId: 'exam-1', resultId: 'result-1', score: 82, correctAnswers: 21,
      wrongAnswers: 2, emptyAnswers: 2, subjectScores: null, ranking: null, notes: null
    }).subscribe();
    const request = http.expectOne('/api/exams/exam-1/results/result-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body['studentId']).toBeUndefined();
    request.flush({ examId: 'exam-1', resultId: 'result-1', score: 82 });
  });
});
