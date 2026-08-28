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

  it('loads the student assessment attempt history', () => {
    service.getAttemptHistory().subscribe(attempts => expect(attempts).toEqual([
      jasmine.objectContaining({ id: 'attempt-1', phase: 1 })
    ]));

    const request = http.expectOne('/api/speed-reading/assessment/attempts');
    expect(request.request.method).toBe('GET');
    request.flush({
      data: [{
        id: 'attempt-1',
        phase: 1,
        status: 2,
        formVersion: 'tr-baseline-v1',
        language: 'tr-TR',
        expectedExerciseCount: 3,
        completedExerciseCount: 3,
        startedAt: '2026-08-28T19:00:00Z',
        completedAt: '2026-08-28T19:05:00Z'
      }]
    });
  });

  it('loads the baseline comparison for assessment phases', () => {
    service.getAttemptComparison().subscribe(comparison => {
      expect(comparison.baseline?.averageWpm).toBe(200);
      expect(comparison.attempts[1].wpmDeltaFromBaseline).toBe(50);
    });

    const request = http.expectOne('/api/speed-reading/assessment/comparison');
    expect(request.request.method).toBe('GET');
    request.flush({
      baseline: {
        attemptId: 'baseline-1',
        phase: 1,
        status: 2,
        formVersion: 'tr-baseline-v1',
        startedAt: '2026-08-20T19:00:00Z',
        completedAt: '2026-08-20T19:05:00Z',
        expectedExerciseCount: 3,
        completedExerciseCount: 3,
        averageWpm: 200,
        averageComprehension: 80,
        averageScore: 75,
        wpmDeltaFromBaseline: null,
        comprehensionDeltaFromBaseline: null
      },
      attempts: [
        {
          attemptId: 'baseline-1',
          phase: 1,
          status: 2,
          formVersion: 'tr-baseline-v1',
          startedAt: '2026-08-20T19:00:00Z',
          completedAt: '2026-08-20T19:05:00Z',
          expectedExerciseCount: 3,
          completedExerciseCount: 3,
          averageWpm: 200,
          averageComprehension: 80,
          averageScore: 75,
          wpmDeltaFromBaseline: null,
          comprehensionDeltaFromBaseline: null
        },
        {
          attemptId: 'post-1',
          phase: 2,
          status: 2,
          formVersion: 'tr-posttraining-v1',
          startedAt: '2026-08-28T19:00:00Z',
          completedAt: '2026-08-28T19:05:00Z',
          expectedExerciseCount: 3,
          completedExerciseCount: 3,
          averageWpm: 250,
          averageComprehension: 90,
          averageScore: 85,
          wpmDeltaFromBaseline: 50,
          comprehensionDeltaFromBaseline: 10
        }
      ]
    });
  });
});
