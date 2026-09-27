import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ReviewService } from './review.service';

describe('ReviewService', () => {
  it('submits the completed session identifier rather than a client-provided score', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(ReviewService);
    const http = TestBed.inject(HttpTestingController);

    service.submitReview('review-1', 'session-1').subscribe();

    const request = http.expectOne(req => req.method === 'POST' && req.url.endsWith('/review/review-1/submit'));
    expect(request.request.body).toEqual({ sessionId: 'session-1' });
    request.flush({ success: true });
    http.match(() => true).forEach(req => req.flush([]));
    http.verify();
  });

  it('loads all review items so completed reviews remain available in history', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(ReviewService);
    const http = TestBed.inject(HttpTestingController);

    service.getAllReviews().subscribe(items => expect(items.length).toBe(1));

    http.expectOne(req => req.method === 'GET' && req.url.endsWith('/review/items'))
      .flush([{ reviewItemId: 'review-1' }]);
    http.verify();
  });
});
