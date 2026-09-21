import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { IdentityService } from './identity.service';

describe('IdentityService parent-student relationships', () => {
  function setup() {
    TestBed.configureTestingModule({
      providers: [IdentityService, provideHttpClient(), provideHttpClientTesting()]
    });
    return { service: TestBed.inject(IdentityService), http: TestBed.inject(HttpTestingController) };
  }

  it('lists relationships with explicit paging and filters', () => {
    const { service, http } = setup();
    service.getParentStudentRelationships(2, 25, ' ada ', 'Pending').subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/parent-student-relationships'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageNumber')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('search')).toBe('ada');
    expect(request.request.params.get('status')).toBe('Pending');
    request.flush({ items: [], totalCount: 0, pageNumber: 2, pageSize: 25 });
    http.verify();
  });

  it('uses dedicated endpoints for request, verification and revocation', () => {
    const { service, http } = setup();
    service.requestParentStudentRelationship('parent-1', 'student-1', 'Guardian').subscribe();
    let request = http.expectOne(candidate => candidate.url.endsWith('/parent-student-relationships'));
    expect(request.request.body).toEqual({ parentUserId: 'parent-1', studentUserId: 'student-1', relationship: 'Guardian' });
    request.flush({ relationshipId: 'relationship-1' });

    service.verifyParentStudentRelationship('relationship-1').subscribe();
    request = http.expectOne(candidate => candidate.url.endsWith('/parent-student-relationships/relationship-1/verify'));
    expect(request.request.method).toBe('POST');
    request.flush(null);

    service.revokeParentStudentRelationship('relationship-1', 'Yetki sona erdi.').subscribe();
    request = http.expectOne(candidate => candidate.url.endsWith('/parent-student-relationships/relationship-1/revoke'));
    expect(request.request.body).toEqual({ reason: 'Yetki sona erdi.' });
    request.flush(null);
    http.verify();
  });
});

describe('IdentityService privacy requests', () => {
  function setup() {
    TestBed.configureTestingModule({
      providers: [IdentityService, provideHttpClient(), provideHttpClientTesting()]
    });
    return { service: TestBed.inject(IdentityService), http: TestBed.inject(HttpTestingController) };
  }

  it('loads bounded admin requests and an encoded assessment detail', () => {
    const { service, http } = setup();

    service.getDataSubjectRequests('Approved', 2, 25).subscribe();
    let request = http.expectOne(candidate => candidate.url.endsWith('/data-subject-requests/admin'));
    expect(request.request.params.get('status')).toBe('Approved');
    expect(request.request.params.get('pageNumber')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    request.flush({ items: [], totalCount: 0, pageNumber: 2, pageSize: 25 });

    service.getDataSubjectRequestDetail('request/1').subscribe();
    request = http.expectOne(candidate => candidate.url.endsWith('/data-subject-requests/admin/request%2F1'));
    expect(request.request.method).toBe('GET');
    request.flush({});
    http.verify();
  });
});
