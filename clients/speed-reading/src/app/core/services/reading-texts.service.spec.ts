import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ReadingTextsService } from './reading-texts.service';

describe('ReadingTextsService', () => {
  let service: ReadingTextsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ReadingTextsService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ReadingTextsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads admin text summaries through the central service and preserves filters', () => {
    service.getAllTexts('Bilim', 3, 'age-1', false, 'hücre').subscribe();

    const request = http.expectOne(req => req.url === '/api/speed-reading/reading-texts');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('category')).toBe('Bilim');
    expect(request.request.params.get('difficultyLevel')).toBe('3');
    expect(request.request.params.get('targetAgeGroupId')).toBe('age-1');
    expect(request.request.params.get('isActive')).toBe('false');
    expect(request.request.params.get('searchTerm')).toBe('hücre');
    request.flush([{ id: 'text-1', title: 'Hücre', wordCount: 10, category: 'Bilim', difficultyLevel: 3, language: 'tr', isActive: true, exerciseId: null, questionCount: 2 }]);
  });

  it('maps central text details and uses idempotent content commands', () => {
    service.getTextWithQuestions('text-1').subscribe(value => {
      expect(value.content).toBe('İçerik');
      expect(value.readingQuestions?.[0].bloomLevel).toBe(2);
      expect(value.readingQuestions?.[0].readingTextId).toBe('text-1');
      expect(value.readingQuestions?.[0].optionA).toBe('A');
    });

    const detailsRequest = http.expectOne('/api/speed-reading/reading-texts/text-1?includeQuestions=true');
    detailsRequest.flush({
      id: 'text-1',
      title: 'Hücre',
      content: 'İçerik',
      wordCount: 1,
      category: 'Bilim',
      difficultyLevel: 3,
      targetAgeGroupConfigurationId: null,
      language: 'tr',
      isActive: true,
      tags: [],
      exerciseId: null,
      recommendedMinLevel: 1,
      recommendedMaxLevel: 10,
      questions: [{ id: 'q-1', questionText: 'Soru', type: 1, bloomLevel: 2, difficultyLevel: 3, explanation: null, optionA: 'A', optionB: 'B', optionC: 'C', optionD: 'D', correctAnswer: 'A', orderIndex: 0 }]
    });

    service.createText({ title: 'Yeni', content: 'Bir iki üç', category: 'Bilim', difficultyLevel: 2, language: 'tr', isActive: true }, 'text-create-key').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/reading-texts');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('text-create-key');
    expect(createRequest.request.body.wordCount).toBe(3);
    createRequest.flush({ id: 'text-2' });

    service.createQuestion({ readingTextId: 'text-1', questionText: 'Yeni soru', type: 1, optionA: 'A', optionB: 'B', optionC: 'C', optionD: 'D', correctAnswer: 'A', orderIndex: 1 }, 'question-key').subscribe();
    const questionRequest = http.expectOne('/api/speed-reading/reading-questions');
    expect(questionRequest.request.method).toBe('POST');
    expect(questionRequest.request.headers.get('Idempotency-Key')).toBe('question-key');
    expect(questionRequest.request.body.bloomLevel).toBe(1);
    questionRequest.flush({ id: 'q-2' });
  });

  it('preserves central metadata when updating a partial text form', () => {
    service.updateText('text-1', { title: 'Yeni başlık' }, 'text-update-key').subscribe();

    const detailsRequest = http.expectOne('/api/speed-reading/reading-texts/text-1?includeQuestions=false');
    detailsRequest.flush({
      id: 'text-1',
      title: 'Eski başlık',
      content: 'Mevcut içerik',
      wordCount: 2,
      category: 'Bilim',
      difficultyLevel: 3,
      targetAgeGroupConfigurationId: 'age-1',
      language: 'tr',
      isActive: true,
      tags: ['hücre', 'biyoloji'],
      exerciseId: 'exercise-1',
      recommendedMinLevel: 2,
      recommendedMaxLevel: 8,
      questions: []
    });

    const updateRequest = http.expectOne('/api/speed-reading/reading-texts/text-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('text-update-key');
    expect(updateRequest.request.body).toEqual({
      title: 'Yeni başlık',
      content: 'Mevcut içerik',
      wordCount: 2,
      category: 'Bilim',
      difficultyLevel: 3,
      targetAgeGroupConfigurationId: 'age-1',
      language: 'tr',
      isActive: true,
      tags: 'hücre,biyoloji',
      recommendedMinLevel: 2,
      recommendedMaxLevel: 8,
      exerciseId: 'exercise-1'
    });
    updateRequest.flush({ id: 'text-1' });
  });

  it('sends question metadata when updating a question', () => {
    service.updateQuestion('question-1', {
      readingTextId: 'text-1',
      questionText: 'Güncel soru',
      bloomLevel: 4,
      difficultyLevel: 3,
      explanation: 'Çünkü...'
    }, 'question-update-key').subscribe();

    const detailsRequest = http.expectOne('/api/speed-reading/reading-texts/text-1?includeQuestions=true');
    detailsRequest.flush({
      id: 'text-1', title: 'Metin', content: 'İçerik', wordCount: 1,
      category: 'Bilim', difficultyLevel: 1, targetAgeGroupConfigurationId: null,
      language: 'tr', isActive: true, tags: [], exerciseId: null,
      recommendedMinLevel: 1, recommendedMaxLevel: 10,
      questions: [{ id: 'question-1', questionText: 'Eski soru', type: 1,
        bloomLevel: 2, difficultyLevel: 2, explanation: 'Eski açıklama',
        optionA: 'Eski A', optionB: 'Eski B', optionC: 'Eski C', optionD: 'Eski D',
        correctAnswer: 'B', orderIndex: 4 }]
    });

    const request = http.expectOne('/api/speed-reading/reading-questions/question-1');
    expect(request.request.body.optionA).toBe('Eski A');
    expect(request.request.body.correctAnswer).toBe('B');
    expect(request.request.body.bloomLevel).toBe(4);
    expect(request.request.body.difficultyLevel).toBe(3);
    expect(request.request.body.explanation).toBe('Çünkü...');
    expect(request.request.headers.get('Idempotency-Key')).toBe('question-update-key');
    request.flush({ id: 'question-1' });
  });

  it('changes text status through the central idempotent update command', () => {
    service.updateStatus('text-1', false).subscribe();

    const detailsRequest = http.expectOne('/api/speed-reading/reading-texts/text-1?includeQuestions=false');
    detailsRequest.flush({
      id: 'text-1',
      title: 'Metin',
      content: 'İçerik',
      wordCount: 1,
      category: 'Bilim',
      difficultyLevel: 1,
      targetAgeGroupConfigurationId: null,
      language: 'tr',
      isActive: true,
      tags: [],
      exerciseId: null,
      recommendedMinLevel: 1,
      recommendedMaxLevel: 10,
      questions: []
    });

    const updateRequest = http.expectOne('/api/speed-reading/reading-texts/text-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body.isActive).toBeFalse();
    updateRequest.flush({ id: 'text-1' });
  });

  it('loads short RSVP texts from the central catalog', () => {
    service.getShortTexts(5).subscribe(texts => {
      expect(texts[0].content).toBe('Kısa içerik');
      expect(texts[0].wordCount).toBe(12);
    });

    const request = http.expectOne(req => req.url === '/api/speed-reading/reading-texts/short');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('limit')).toBe('5');
    request.flush([{ id: 'text-1', title: 'Kısa', content: 'Kısa içerik', wordCount: 12, category: 'Genel' }]);
  });
});
