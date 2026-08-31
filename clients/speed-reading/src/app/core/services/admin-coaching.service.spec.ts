import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminCoachingService } from './admin-coaching.service';

describe('AdminCoachingService', () => {
  let service: AdminCoachingService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AdminCoachingService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AdminCoachingService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads assignments from the coaching admin contract and maps pagination', () => {
    let page: any;
    service.getAssignments({ status: 'Pending', page: 2, pageSize: 10 }).subscribe(value => page = value);

    const request = http.expectOne(req => req.url === '/api/v1/coaching-admin/assignments');
    expect(request.request.params.get('pageNumber')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('10');
    expect(request.request.params.get('status')).toBe('Pending');
    request.flush({
      items: [{ id: 'a-1', teacherId: 't-1', title: 'Ödev', status: 'Pending', dueDate: '2026-08-31', studentCount: 4, createdAt: '2026-08-01' }],
      pageNumber: 2, pageSize: 10, totalCount: 11
    });

    expect(page.total).toBe(11);
    expect(page.items[0].assignedById).toBe('t-1');
    expect(page.items[0].studentName).toBe('4 öğrenci');
  });

  it('uses the admin cancellation command instead of the teacher update route', () => {
    service.cancelSession('session-1').subscribe();

    const request = http.expectOne('/api/v1/coaching-admin/sessions/session-1/cancel');
    expect(request.request.method).toBe('POST');
    request.flush(null);
  });
});
