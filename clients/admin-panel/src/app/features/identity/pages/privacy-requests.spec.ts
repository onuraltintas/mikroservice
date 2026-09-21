import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { IdentityService } from '../../../core/services/identity.service';
import { PrivacyRequestsComponent } from './privacy-requests';

describe('PrivacyRequestsComponent', () => {
  it('shows the product scope and service-specific inventory', () => {
    const executeErasure = vi.fn(() => of({
      id: 'request-1', requesterUserId: 'user-1', requestType: 'Erasure',
      scope: 'SpeedReading' as const, status: 'Processing' as const,
      reason: 'Kullanıcı talebi', submittedAt: '2026-09-21T00:00:00Z'
    }));
    TestBed.configureTestingModule({
      imports: [PrivacyRequestsComponent],
      providers: [{
        provide: IdentityService,
        useValue: {
          getDataSubjectRequests: () => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }),
          getDataSubjectRequestDetail: () => of(null),
          executeDataSubjectRequestErasure: executeErasure
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
    const buttons = fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>;
    const executeButton = Array.from(buttons)
      .find(button => button.textContent?.includes('Silmeyi başlat'));
    expect(executeButton).toBeTruthy();
    if (!executeButton) throw new Error('Silmeyi başlat button was not rendered.');
    executeButton.click();
    expect(executeErasure).toHaveBeenCalledWith('request-1');
  });
});
