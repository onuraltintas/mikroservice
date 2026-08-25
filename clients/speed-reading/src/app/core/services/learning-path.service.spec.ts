import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { LearningPathService } from './learning-path.service';

describe('LearningPathService', () => {
  let service: LearningPathService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [LearningPathService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(LearningPathService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the next personalized item through the central service', () => {
    service.getNextPersonalizedItem().subscribe(item => {
      expect(item?.id).toBe('item-1');
      expect(item?.contentType).toBe('ReadingText');
    });

    const request = http.expectOne('/api/speed-reading/learning-paths/personalized/next');
    expect(request.request.method).toBe('GET');
    request.flush({
      id: 'item-1',
      pathIndex: 1,
      contentType: 'ReadingText',
      contentId: 'text-1',
      contentTitle: 'Odaklanma',
      difficultyLevel: 2,
      estimatedDurationMinutes: 5,
      isCompleted: false,
      completedAt: null,
      achievedScore: null,
      recommendationReason: 'Sıradaki öneri',
      isUnlocked: true
    });
  });
});
