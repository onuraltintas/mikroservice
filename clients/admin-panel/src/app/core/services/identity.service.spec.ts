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

describe('IdentityService shared platform legal pages', () => {
  function setup() {
    TestBed.configureTestingModule({
      providers: [IdentityService, provideHttpClient(), provideHttpClientTesting()]
    });
    return { service: TestBed.inject(IdentityService), http: TestBed.inject(HttpTestingController) };
  }

  it('reads published legal pages from the shared platform endpoint', () => {
    const { service, http } = setup();
    service.getPublicLegalPage('privacy').subscribe(page => expect(page.slug).toBe('privacy'));
    const request = http.expectOne('/api/platform/legal-pages/privacy');
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, data: { slug: 'privacy', title: 'Gizlilik', isPublished: true } });
    http.verify();
  });

  it('lists, edits and loads revisions from permission-protected Identity admin endpoints', () => {
    const { service, http } = setup();
    service.getLegalPages().subscribe(pages => expect(pages).toHaveLength(1));
    http.expectOne('/api/platform/admin/legal-pages').flush({ success: true, data: [{ slug: 'privacy' }] });

    service.upsertLegalPage('privacy', { title: 'Gizlilik', content: 'Onaylanmış metin', isPublished: false }).subscribe();
    const update = http.expectOne('/api/platform/admin/legal-pages/privacy');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body.isPublished).toBe(false);
    update.flush({ success: true, data: { slug: 'privacy' } });

    service.getLegalPageRevisions('privacy').subscribe(revisions => expect(revisions).toHaveLength(1));
    http.expectOne('/api/platform/admin/legal-pages/privacy/revisions')
      .flush({ success: true, data: [{ slug: 'privacy', version: 1 }] });
    http.verify();
  });

  it('archives and restores legal documents through the protected admin API', () => {
    const { service, http } = setup();
    service.archiveLegalPage('privacy').subscribe(page => expect(page.isArchived).toBe(true));
    const archive = http.expectOne('/api/platform/admin/legal-pages/privacy');
    expect(archive.request.method).toBe('DELETE');
    archive.flush({ success: true, data: { slug: 'privacy', isArchived: true } });

    service.restoreLegalPage('privacy').subscribe(page => expect(page.isArchived).toBe(false));
    const restore = http.expectOne('/api/platform/admin/legal-pages/privacy/restore');
    expect(restore.request.method).toBe('POST');
    restore.flush({ success: true, data: { slug: 'privacy', isArchived: false, isPublished: false } });
    http.verify();
  });
});
