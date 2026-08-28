import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AssessmentService } from './assessment.service';

describe('AssessmentService', () => {
  let service: AssessmentService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AssessmentService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AssessmentService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts a versioned assessment attempt with the canonical measurement metadata', () => {
    service.startAttempt({
      phase: 1,
      formVersion: 'tr-baseline-v1',
      language: 'tr-TR',
      expectedExerciseCount: 3
    }).subscribe(attempt => expect(attempt.id).toBe('attempt-1'));

    const request = http.expectOne('/api/speed-reading/assessment/attempts');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      phase: 1,
      formVersion: 'tr-baseline-v1',
      language: 'tr-TR',
      expectedExerciseCount: 3
    });
    request.flush({
      id: 'attempt-1',
      phase: 1,
      status: 1,
      formVersion: 'tr-baseline-v1',
      language: 'tr-TR',
      expectedExerciseCount: 3,
      completedExerciseCount: 0,
      startedAt: '2026-08-28T19:00:00Z',
      completedAt: null
    });
  });

  it('scopes level calculation to the assessment attempt', () => {
    service.calculateLevel([], 'attempt-1').subscribe();

    const request = http.expectOne('/api/speed-reading/assessment/calculate');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      exerciseResults: [],
      attemptId: 'attempt-1'
    });
    request.flush({});
  });
});
