import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { InstitutionsService } from './institutions.service';
import { SubscriptionService } from './subscription.service';

describe('InstitutionsService', () => {
  let service: InstitutionsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [InstitutionsService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(InstitutionsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('maps the identity paged institution response and forwards backend query names', () => {
    service.getInstitutions('Kolej', true).subscribe(institutions => {
      expect(institutions[0].contactEmail).toBe('okul@example.com');
      expect(institutions[0].phoneNumber).toBe('555');
      expect(institutions[0].studentCount).toBe(12);
      expect(institutions[0].createdAt).toEqual(new Date('2026-08-01T10:00:00.000Z'));
    });

    const request = http.expectOne(candidate => candidate.url === '/api/v1/institutions');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageNumber')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('100');
    expect(request.request.params.get('search')).toBe('Kolej');
    expect(request.request.params.get('isActive')).toBe('true');
    request.flush({
      items: [{
        id: 'institution-1', name: 'Örnek Kolej', email: 'okul@example.com', phone: '555',
        address: 'Adres', city: 'Ankara', district: 'Çankaya', isActive: true,
        studentCount: 12, teacherCount: 3, createdAt: '2026-08-01T10:00:00.000Z'
      }],
      totalCount: 1, pageNumber: 1, pageSize: 100
    });
  });

  it('uses the identity active-state command when changing institution status', () => {
    service.setActive('institution-1', false).subscribe();

    const request = http.expectOne('/api/v1/institutions/institution-1/active');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ isActive: false });
    request.flush(null);
  });

  it('maps the single institution response to the frontend model', () => {
    service.getInstitutionById('institution-1').subscribe(institution => {
      expect(institution.contactEmail).toBe('okul@example.com');
      expect(institution.phoneNumber).toBe('555');
      expect(institution.city).toBe('Ankara');
      expect(institution.district).toBe('Çankaya');
      expect(institution.licenseType).toBe(4);
      expect(institution.maxStudents).toBe(120);
      expect(institution.maxTeachers).toBe(12);
      expect(institution.subscriptionStartDate).toEqual(new Date('2026-09-01T00:00:00.000Z'));
      expect(institution.subscriptionEndDate).toEqual(new Date('2027-09-01T00:00:00.000Z'));
    });

    const request = http.expectOne('/api/v1/institutions/institution-1');
    request.flush({
      id: 'institution-1', name: 'Örnek Kolej', email: 'okul@example.com', phone: '555',
      address: 'Adres', city: 'Ankara', district: 'Çankaya', isActive: true,
      licenseType: 4, maxStudents: 120, maxTeachers: 12,
      subscriptionStartDate: '2026-09-01T00:00:00.000Z',
      subscriptionEndDate: '2027-09-01T00:00:00.000Z'
    });
  });

  it('uses the identity field names when updating institution contact details', () => {
    service.updateInstitution('institution-1', {
      name: 'Güncel Kolej',
      address: 'Yeni adres',
      city: 'Ankara',
      district: 'Çankaya',
      phone: '555',
      email: 'okul@example.com'
    }).subscribe();

    const request = http.expectOne('/api/v1/institutions/institution-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      name: 'Güncel Kolej',
      address: 'Yeni adres',
      city: 'Ankara',
      district: 'Çankaya',
      phone: '555',
      email: 'okul@example.com'
    });
    request.flush(null);
  });

  it('loads the signed-in institution administrator’s access summary', () => {
    const subscriptions = TestBed.inject(SubscriptionService);

    subscriptions.getMyInstitutionAccess().subscribe(license => {
      expect(license?.seatCount).toBe(30);
      expect(license?.activeStudentIds).toEqual(['student-1']);
    });

    const request = http.expectOne('/api/speed-reading/subscriptions/institution-access/my');
    expect(request.request.method).toBe('GET');
    request.flush({
      success: true,
      data: {
        id: 'license-1', institutionId: 'institution-1', status: 'Active', seatCount: 30,
        usedSeatCount: 1, activeStudentIds: ['student-1'], startDate: '2026-09-01T00:00:00Z',
        endDate: '2027-09-01T00:00:00Z', paymentReference: 'EFT-2026-0001', notes: null,
        approvedAt: '2026-09-01T00:00:00Z', plan: { id: 'plan-1', name: 'Kurum' }
      }
    });
  });
});
