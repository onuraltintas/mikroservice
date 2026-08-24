import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ExerciseService } from './exercise.service';

describe('ExerciseService', () => {
  let service: ExerciseService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ExerciseService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ExerciseService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('writes an exercise result through the dedicated service with idempotency', () => {
    service.submitExerciseResult({
      exerciseId: 'exercise-1',
      readingTextId: 'reading-1',
      timeSpentSeconds: 90,
      wordsRead: 450,
      rawWPM: 300,
      comprehensionScore: 88,
      weightedKDP: 264,
      questionAnswersJson: '[]',
      readingMovementsJson: '[]'
    }, 'result-key-123456').subscribe();

    const request = http.expectOne('/api/speed-reading/progress/exercise-results');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('result-key-123456');
    expect(request.request.body).toEqual({
      exerciseId: 'exercise-1',
      readingTextId: 'reading-1',
      wordsRead: 450,
      timeSpentSeconds: 90,
      rawWpm: 300,
      comprehensionScore: 88,
      weightedKdp: 264,
      questionAnswersJson: '[]',
      readingMovementsJson: '[]'
    });
    request.flush({ id: 'result-1' });
  });
});
