import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SpeedReadingAdminService } from './speed-reading-admin.service';

describe('SpeedReadingAdminService', () => {
  let service: SpeedReadingAdminService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SpeedReadingAdminService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(SpeedReadingAdminService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads capabilities through the Gateway speed-reading route', () => {
    const response = {
      mode: 'Standalone',
      coachingIntegrationEnabled: false,
      notificationIntegrationEnabled: false,
      subscriptionIntegrationEnabled: false
    };

    service.getCapabilities().subscribe(value => expect(value).toEqual(response));

    const request = http.expectOne('/api/speed-reading/capabilities');
    expect(request.request.method).toBe('GET');
    request.flush(response);
  });

  it('loads exercise types from the legacy content catalog', () => {
    const response = {
      items: [{
        id: 'type-1',
        name: 'schulte',
        displayName: 'Schulte Tablosu',
        description: 'Odaklanma',
        iconName: 'grid',
        colorCode: '#2563eb',
        sortOrder: 1,
        isActive: true,
        engineType: 'SchulteTable',
        categoryId: null
      }],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1
    };

    service.getExerciseTypes().subscribe(value => expect(value).toEqual(response));

    const request = http.expectOne('/api/speed-reading/exercise-types?pageNumber=1&pageSize=20');
    expect(request.request.method).toBe('GET');
    request.flush(response);
  });

  it('creates an exercise type with an idempotency key', () => {
    service.createExerciseType({
      name: 'schulte',
      displayName: 'Schulte Tablosu',
      description: 'Odaklanma',
      iconName: 'grid',
      colorCode: '#2563eb',
      sortOrder: 1,
      isActive: true,
      engineType: 'SchulteTable',
      categoryId: null
    }, 'admin-type-key-123456').subscribe();

    const request = http.expectOne('/api/speed-reading/exercise-types');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('admin-type-key-123456');
    request.flush({ id: 'type-1' });
  });

  it('loads exercises with paging and writes through the dedicated route', () => {
    service.getExercises(2, 10).subscribe();
    const listRequest = http.expectOne('/api/speed-reading/exercises?pageNumber=2&pageSize=10');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0 });

    service.createExercise({
      title: 'Odak egzersizi',
      description: 'Kısa açıklama',
      difficultyLevel: 2,
      exerciseTypeId: 'type-1',
      configurationJson: '{}',
      targetAgeGroupConfigurationId: null
    }, 'exercise-key-123456').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/exercises');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('exercise-key-123456');
    createRequest.flush({ id: 'exercise-1' });
  });

  it('loads reading texts and details, then sends idempotent mutations', () => {
    service.getReadingTexts('exercise-1').subscribe(value => expect(value).toEqual([]));
    const listRequest = http.expectOne('/api/speed-reading/reading-texts?exerciseId=exercise-1');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([]);

    service.getReadingText('text-1').subscribe(value => expect(value.id).toBe('text-1'));
    const detailRequest = http.expectOne('/api/speed-reading/reading-texts/text-1');
    expect(detailRequest.request.method).toBe('GET');
    detailRequest.flush({
      id: 'text-1', title: 'Metin', content: 'İçerik', wordCount: 1, category: 'Genel',
      difficultyLevel: 1, language: 'tr', isActive: true, exerciseId: null,
      targetAgeGroupConfigurationId: null, tags: [], recommendedMinLevel: 0, recommendedMaxLevel: 10,
      questions: []
    });

    const request = {
      title: 'Metin',
      content: 'İçerik',
      wordCount: 1,
      category: 'Genel',
      difficultyLevel: 1,
      targetAgeGroupConfigurationId: null,
      language: 'tr',
      isActive: true,
      tags: 'odak',
      recommendedMinLevel: 0,
      recommendedMaxLevel: 10,
      exerciseId: null
    };

    service.createReadingText(request, 'reading-text-key-123456').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/reading-texts');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('reading-text-key-123456');
    createRequest.flush({ id: 'text-1' });

    service.updateReadingText('text-1', request, 'reading-text-update-123456').subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/reading-texts/text-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('reading-text-update-123456');
    updateRequest.flush({ id: 'text-1' });

    service.deleteReadingText('text-1', 'reading-text-delete-123456').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/reading-texts/text-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    expect(deleteRequest.request.headers.get('Idempotency-Key')).toBe('reading-text-delete-123456');
    deleteRequest.flush(null);
  });

  it('writes reading questions through the dedicated route with idempotency', () => {
    const request = {
      readingTextId: 'text-1',
      questionText: 'Ana fikir nedir?',
      type: 1,
      bloomLevel: 2,
      difficultyLevel: 1,
      explanation: 'Metnin ana düşüncesi.',
      optionA: 'A',
      optionB: 'B',
      optionC: 'C',
      optionD: 'D',
      correctAnswer: 'A',
      orderIndex: 0
    };

    service.createReadingQuestion(request, 'question-create-key-123456').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/reading-questions');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('question-create-key-123456');
    createRequest.flush({ id: 'question-1' });

    service.updateReadingQuestion('question-1', {
      questionText: 'Güncellenen soru',
      type: request.type,
      bloomLevel: request.bloomLevel,
      difficultyLevel: request.difficultyLevel,
      explanation: request.explanation,
      optionA: request.optionA,
      optionB: request.optionB,
      optionC: request.optionC,
      optionD: request.optionD,
      correctAnswer: request.correctAnswer,
      orderIndex: request.orderIndex
    }, 'question-update-key-123456').subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/reading-questions/question-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('question-update-key-123456');
    updateRequest.flush({ id: 'question-1' });

    service.deleteReadingQuestion('question-1', 'question-delete-key-123456').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/reading-questions/question-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    expect(deleteRequest.request.headers.get('Idempotency-Key')).toBe('question-delete-key-123456');
    deleteRequest.flush(null);
  });

  it('loads and writes program templates through the ProgramManage route', () => {
    service.getProgramTemplates().subscribe(value => expect(value).toEqual([]));
    const listRequest = http.expectOne('/api/speed-reading/program-templates/admin');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([]);

    const request = {
      name: 'Temel program',
      description: 'Başlangıç programı',
      targetAgeGroupConfigurationId: 'age-1',
      minAssessmentScore: 0,
      maxAssessmentScore: 100,
      weeklyPatternJson: '{}',
      initialDifficultyLevel: 0,
      weeksPerDifficultyIncrease: 1,
      maxDifficultyLevel: 10,
      totalWeeks: 4,
      totalDays: 28,
      isActive: true,
      displayOrder: 1,
      programType: 0,
      examType: null,
      isAssessment: false
    };

    service.createProgramTemplate(request, 'program-create-key-123456').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/program-templates');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('program-create-key-123456');
    createRequest.flush({ id: 'program-1' });

    service.updateProgramTemplate('program-1', request, 'program-update-key-123456').subscribe();
    const updateRequest = http.expectOne('/api/speed-reading/program-templates/program-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('program-update-key-123456');
    updateRequest.flush({ id: 'program-1' });

    service.deleteProgramTemplate('program-1', 'program-delete-key-123456').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/program-templates/program-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    expect(deleteRequest.request.headers.get('Idempotency-Key')).toBe('program-delete-key-123456');
    deleteRequest.flush(null);
  });
});
