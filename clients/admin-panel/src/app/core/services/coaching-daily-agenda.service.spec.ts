import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingDailyAgendaService } from './coaching-daily-agenda.service';

describe('CoachingDailyAgendaService', () => {
  it('reads the own-student endpoint with period and page and propagates authorization failures', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingDailyAgendaService); const http = TestBed.inject(HttpTestingController);
    let total = -1; service.get('overdue', 2).subscribe(value => total = value.totalCount);
    const request = http.expectOne(r => r.url.endsWith('/coaching/study-planning/daily-agenda'));
    expect(request.request.method).toBe('GET'); expect(request.request.params.keys().sort()).toEqual(['mode', 'pageNumber']);
    expect(request.request.params.get('mode')).toBe('overdue'); expect(request.request.params.get('pageNumber')).toBe('2');
    request.flush({ totalCount: 27 }); expect(total).toBe(27);
    let status = 0; service.get('today').subscribe({ error: error => status = error.status });
    http.expectOne(r => r.params.get('mode') === 'today').flush({}, { status: 403, statusText: 'Forbidden' });
    expect(status).toBe(403); http.verify();
  });
});
