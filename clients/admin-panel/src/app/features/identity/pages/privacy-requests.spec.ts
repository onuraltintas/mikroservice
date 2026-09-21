import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { IdentityService } from '../../../core/services/identity.service';
import { PrivacyRequestsComponent } from './privacy-requests';

describe('PrivacyRequestsComponent', () => {
  it('shows the product scope and service-specific inventory', () => {
    TestBed.configureTestingModule({
      imports: [PrivacyRequestsComponent],
      providers: [{
        provide: IdentityService,
        useValue: {
          getDataSubjectRequests: () => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }),
          getDataSubjectRequestDetail: () => of(null)
        }
      }]
    });

    const fixture = TestBed.createComponent(PrivacyRequestsComponent);
    fixture.componentInstance.detail.set({
      request: {
        id: 'request-1',
        requesterUserId: 'user-1',
        requestType: 'Erasure',
        scope: 'SpeedReading',
        status: 'Approved',
        reason: 'Kullanıcı talebi',
        submittedAt: '2026-09-21T00:00:00Z'
      },
      assessment: {
        services: [{
          serviceName: 'SpeedReading',
          canProceed: true,
          hasActiveLegalHold: false,
          totalRecordCount: 4,
          recordCounts: { profiles: 1, readingAttempts: 3 },
          assessedAt: '2026-09-21T00:01:00Z'
        }],
        missingServices: [],
        isComplete: true,
        hasBlockingLegalHold: false,
        isReadyForErasure: true,
        totalRecordCount: 4
      }
    });

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Hızlı Okuma');
    expect(fixture.nativeElement.textContent).toContain('readingAttempts: 3');
  });
});
