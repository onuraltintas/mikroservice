import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingManagementService } from '../../../core/services/coaching-management.service';
import { CoachingNewsletterResultComponent } from './coaching-newsletter-result.component';

describe('CoachingNewsletterResultComponent', () => {
  it('does not confirm on GET; the user must activate the confirmation button', () => {
    const coaching = {
      confirmCoachingNewsletter: vi.fn(() => of({ success: true, message: 'Bülten aboneliğiniz onaylandı.' })),
      unsubscribeFromCoachingNewsletter: vi.fn(() => of({ success: true, message: 'Bülten aboneliğiniz iptal edildi.' }))
    };
    TestBed.configureTestingModule({
      imports: [CoachingNewsletterResultComponent],
      providers: [
        { provide: CoachingManagementService, useValue: coaching },
        { provide: ActivatedRoute, useValue: { data: of({ action: 'confirm' }), queryParamMap: of(convertToParamMap({ token: 'secure-token' })) } }
      ]
    });
    const fixture = TestBed.createComponent(CoachingNewsletterResultComponent);
    fixture.detectChanges();
    expect(coaching.confirmCoachingNewsletter).not.toHaveBeenCalled();

    fixture.nativeElement.querySelector('button').click();
    fixture.detectChanges();
    expect(coaching.confirmCoachingNewsletter).toHaveBeenCalledWith('secure-token');
    expect(fixture.nativeElement.textContent).toContain('Bülten aboneliğiniz onaylandı.');
  });

  it('requires an explicit click before sending an unsubscribe request', () => {
    const coaching = {
      confirmCoachingNewsletter: vi.fn(() => of({ success: true, message: 'Bülten aboneliğiniz onaylandı.' })),
      unsubscribeFromCoachingNewsletter: vi.fn(() => of({ success: true, message: 'Bülten aboneliğiniz iptal edildi.' }))
    };
    TestBed.configureTestingModule({
      imports: [CoachingNewsletterResultComponent],
      providers: [
        { provide: CoachingManagementService, useValue: coaching },
        { provide: ActivatedRoute, useValue: { data: of({ action: 'unsubscribe' }), queryParamMap: of(convertToParamMap({ token: 'secure-token' })) } }
      ]
    });
    const fixture = TestBed.createComponent(CoachingNewsletterResultComponent);
    fixture.detectChanges();
    expect(coaching.unsubscribeFromCoachingNewsletter).not.toHaveBeenCalled();

    fixture.nativeElement.querySelector('button').click();
    fixture.detectChanges();
    expect(coaching.unsubscribeFromCoachingNewsletter).toHaveBeenCalledWith('secure-token');
    expect(fixture.nativeElement.textContent).toContain('Bülten aboneliğiniz iptal edildi.');
  });
});
