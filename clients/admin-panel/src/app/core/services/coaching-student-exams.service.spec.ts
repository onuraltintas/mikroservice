import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingStudentExamsService, StudentExamInput } from './coaching-student-exams.service';

describe('CoachingStudentExamsService', () => {
  it('uses owned endpoints and carries the read version on replace and delete', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingStudentExamsService);
    const http = TestBed.inject(HttpTestingController);
    const input: StudentExamInput = { title: 'Mock', examType: 'Mock', examDate: '2026-10-01', score: 80, maxScore: 100,
      correctAnswers: 8, wrongAnswers: 1, emptyAnswers: 1, lessons: [] };
    service.list(2).subscribe();
    http.expectOne('/api/coaching/study-planning/exams?pageNumber=2&pageSize=20').flush({ success: true, data: {} });
    service.create(input).subscribe();
    const create = http.expectOne('/api/coaching/study-planning/exams');
    expect(create.request.method).toBe('POST'); expect(create.request.body).toEqual(input);
    create.flush({ success: true, data: {} });
    service.replace('exam', 7, input).subscribe();
    const replace = http.expectOne('/api/coaching/study-planning/exams/exam');
    expect(replace.request.body).toEqual({ expectedVersion: 7, exam: input });
    replace.flush({ success: true, data: {} });
    service.delete('exam', 8).subscribe();
    const remove = http.expectOne('/api/coaching/study-planning/exams/exam?expectedVersion=8');
    expect(remove.request.method).toBe('DELETE'); remove.flush(null);
    http.verify();
  });
});
