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

  it('writes exercise commands through the dedicated service with the bounded-context contract', () => {
    service.createExercise({
      title: 'Göz Takibi',
      description: 'Temel egzersiz',
      difficultyLevel: 2,
      exerciseTypeId: 'type-1',
      configurationJson: '{}',
      targetAgeGroupId: 'age-1'
    }, 'exercise-create-key').subscribe();

    const request = http.expectOne('/api/speed-reading/exercises');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('exercise-create-key');
    expect(request.request.body).toEqual({
      title: 'Göz Takibi',
      description: 'Temel egzersiz',
      difficultyLevel: 2,
      exerciseTypeId: 'type-1',
      configurationJson: '{}',
      targetAgeGroupConfigurationId: 'age-1'
    });
    request.flush({ id: 'exercise-1' });

    service.updateExercise('exercise-1', {
      title: 'Göz Takibi 2',
      difficultyLevel: 3,
      exerciseTypeId: 'type-1',
      configurationJson: '{}'
    }, 'exercise-update-key').subscribe();

    const updateRequest = http.expectOne('/api/speed-reading/exercises/exercise-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('exercise-update-key');
    expect(updateRequest.request.body.targetAgeGroupConfigurationId).toBeNull();
    updateRequest.flush({ id: 'exercise-1' });

    service.deleteExercise('exercise-1', 'exercise-delete-key').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/exercises/exercise-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    expect(deleteRequest.request.headers.get('Idempotency-Key')).toBe('exercise-delete-key');
    deleteRequest.flush(null);
  });
});
