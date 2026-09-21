import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingAgreementService } from './coaching-agreement.service';

describe('CoachingAgreementService', () => {
  let service: CoachingAgreementService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CoachingAgreementService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CoachingAgreementService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the current Turkish agreement', () => {
    service.getCurrent('tr-TR').subscribe();

    const request = http.expectOne(candidate =>
      candidate.url === '/api/coaching-agreements/current'
      && candidate.params.get('locale') === 'tr-TR');
    expect(request.request.method).toBe('GET');
    request.flush({ documentId: 'document-1', acknowledgedByCurrentStudent: false });
  });

  it('acknowledges only the selected server document', () => {
    service.acknowledge('document-1').subscribe();

    const request = http.expectOne('/api/coaching-agreements/current/acknowledgements');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ agreementDocumentId: 'document-1' });
    request.flush({ acknowledgementId: 'ack-1' });
  });
});
