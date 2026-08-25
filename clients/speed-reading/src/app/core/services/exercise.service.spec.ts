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

  it('uses the central reading-text commands instead of the legacy exercise route', () => {
    service.createReadingText({
      title: 'Bilim metni',
      content: 'Bir iki üç',
      wordCount: 99,
      category: 'Bilim',
      difficultyLevel: 2
    }, 'reading-create-key').subscribe();

    const createRequest = http.expectOne('/api/speed-reading/reading-texts');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('reading-create-key');
    expect(createRequest.request.body).toEqual({
      title: 'Bilim metni',
      content: 'Bir iki üç',
      wordCount: 3,
      category: 'Bilim',
      difficultyLevel: 2,
      targetAgeGroupConfigurationId: null,
      language: 'tr',
      isActive: true,
      tags: null,
      recommendedMinLevel: 1,
      recommendedMaxLevel: 10,
      exerciseId: null
    });
    createRequest.flush({ id: 'reading-1' });

    service.updateReadingText('reading-1', { title: 'Bilim metni 2' }, 'reading-update-key').subscribe();
    const detailsRequest = http.expectOne('/api/speed-reading/reading-texts/reading-1?includeQuestions=false');
    detailsRequest.flush({
      id: 'reading-1', title: 'Bilim metni', content: 'Bir iki üç', wordCount: 3,
      category: 'Bilim', difficultyLevel: 2, targetAgeGroupConfigurationId: null,
      language: 'tr', isActive: true, tags: [], exerciseId: null,
      recommendedMinLevel: 1, recommendedMaxLevel: 10, questions: []
    });
    const updateRequest = http.expectOne('/api/speed-reading/reading-texts/reading-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('reading-update-key');
    expect(updateRequest.request.body.title).toBe('Bilim metni 2');
    updateRequest.flush({ id: 'reading-1' });

    service.deleteReadingText('reading-1', 'reading-delete-key').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/reading-texts/reading-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    expect(deleteRequest.request.headers.get('Idempotency-Key')).toBe('reading-delete-key');
    deleteRequest.flush(null);
  });
});
