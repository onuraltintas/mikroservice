import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CoachingTeacherTrackingService } from './coaching-teacher-tracking.service';

describe('CoachingTeacherTrackingService', () => {
  it('uses only GET with encoded filters and server paging', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(CoachingTeacherTrackingService).get({ fromDate: '2026-10-01', toDate: '2026-10-20', mode: 'review', search: '  Öğrenci & ödev  ' }, 3).subscribe();
    const request = http.expectOne(r => r.url.endsWith('/coaching/teachers/me/tracking'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('search')).toBe('Öğrenci & ödev');
    expect(request.request.params.get('mode')).toBe('review');
    expect(request.request.params.get('pageNumber')).toBe('3');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('fromDate')).toBe('2026-10-01');
    expect(request.request.params.get('toDate')).toBe('2026-10-20');
    request.flush({ items: [] }); http.verify();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
