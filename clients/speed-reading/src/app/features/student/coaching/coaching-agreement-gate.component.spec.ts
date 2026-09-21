import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CoachingAgreementService, CurrentCoachingAgreement } from '../../../core/services/coaching-agreement.service';
import { CoachingAgreementGateComponent } from './coaching-agreement-gate.component';

describe('CoachingAgreementGateComponent', () => {
  let fixture: ComponentFixture<CoachingAgreementGateComponent>;
  let service: jasmine.SpyObj<CoachingAgreementService>;

  const agreement: CurrentCoachingAgreement = {
    documentId: 'document-1',
    documentVersion: '2026.1',
    locale: 'tr-TR',
    title: 'Öğrenci Koçluk Anlaşması',
    documentReference: 'https://legal.example.test/coaching/2026.1',
    contentSha256: 'a'.repeat(64),
    effectiveAt: '2026-09-21T00:00:00Z',
    acknowledgedByCurrentStudent: false,
    acknowledgementId: null
  };

  beforeEach(async () => {
    service = jasmine.createSpyObj<CoachingAgreementService>(
      'CoachingAgreementService',
      ['getCurrent', 'acknowledge']);
    await TestBed.configureTestingModule({
      imports: [CoachingAgreementGateComponent],
      providers: [{ provide: CoachingAgreementService, useValue: service }]
    }).compileComponents();
  });

  it('does not unlock coaching before an explicit acknowledgement', () => {
    service.getCurrent.and.returnValue(of(agreement));
    fixture = TestBed.createComponent(CoachingAgreementGateComponent);
    const ready = jasmine.createSpy('ready');
    fixture.componentInstance.ready.subscribe(ready);

    fixture.detectChanges();

    expect(ready).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Öğrenci Koçluk Anlaşması');
  });

  it('unlocks immediately when the current version is already acknowledged', () => {
    service.getCurrent.and.returnValue(of({
      ...agreement,
      acknowledgedByCurrentStudent: true,
      acknowledgementId: 'ack-1'
    }));
    fixture = TestBed.createComponent(CoachingAgreementGateComponent);
    const ready = jasmine.createSpy('ready');
    fixture.componentInstance.ready.subscribe(ready);

    fixture.detectChanges();

    expect(ready).toHaveBeenCalledTimes(1);
  });

  it('requires the review checkbox before acknowledgement', () => {
    service.getCurrent.and.returnValue(of(agreement));
    service.acknowledge.and.returnValue(of({
      acknowledgementId: 'ack-1',
      agreementDocumentId: 'document-1',
      locale: 'tr-TR',
      acknowledgedAt: '2026-09-21T12:00:00Z',
      withdrawnAt: null
    }));
    fixture = TestBed.createComponent(CoachingAgreementGateComponent);
    fixture.detectChanges();

    fixture.componentInstance.accept();
    expect(service.acknowledge).not.toHaveBeenCalled();

    fixture.componentInstance.reviewConfirmed = true;
    fixture.componentInstance.accept();
    expect(service.acknowledge).toHaveBeenCalledOnceWith('document-1');
  });

  it('offers retry without unlocking when the agreement cannot be loaded', () => {
    service.getCurrent.and.returnValue(throwError(() => new Error('offline')));
    fixture = TestBed.createComponent(CoachingAgreementGateComponent);
    const ready = jasmine.createSpy('ready');
    fixture.componentInstance.ready.subscribe(ready);

    fixture.detectChanges();

    expect(ready).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('yeniden deneyin');
  });
});
