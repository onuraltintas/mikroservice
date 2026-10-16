import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CoachingLearningSetupService } from './coaching-learning-setup.service';

describe('CoachingLearningSetupService', () => {
  it('uses only own coaching setup endpoints and sends expected versions', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingLearningSetupService); const http = TestBed.inject(HttpTestingController);
    service.get().subscribe(r => expect(r.nextStep).toBe('Purpose'));
    http.expectOne('/api/coaching/study-planning/setup').flush({ data: { nextStep: 'Purpose' } });
    const request = { expectedVersion: 3, academicYearStart: 2026, purpose: 'General' as const, targetExamYear: null, primaryScoreType: null, foreignLanguage: null, courses: [], topics: [], startingStatusReviewed: false };
    service.save(request).subscribe();
    const save = http.expectOne('/api/coaching/study-planning/setup'); expect(save.request.method).toBe('PUT'); expect(save.request.body).toEqual(request); save.flush({ data: {} });
    service.catalog().subscribe(); const first = http.expectOne(r => r.url.endsWith('/setup/catalog')); expect(first.request.params.has('lessonId')).toBe(false); first.flush({ data: {} });
    service.catalog('lesson-id', 2).subscribe(); const next = http.expectOne(r => r.url.endsWith('/setup/catalog')); expect(next.request.params.get('lessonId')).toBe('lesson-id'); expect(next.request.params.get('pageNumber')).toBe('2'); next.flush({ data: {} });
    http.verify();
  });
});
