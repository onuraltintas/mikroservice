import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LearningSetupEntryComponent } from './learning-setup-entry.component';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';

describe('Learning setup entry', () => {
  it('offers a resumable setup without redirecting away from current work', () => {
    TestBed.configureTestingModule({ imports: [LearningSetupEntryComponent], providers: [provideRouter([]), { provide: CoachingLearningSetupService,
      useValue: { get: () => of({ context: { id: 'context' }, isReady: false, nextStep: 'Availability' }) } }] });
    const fixture = TestBed.createComponent(LearningSetupEntryComponent); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Kuruluma devam et');
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/study-plans"]')).toBeTruthy();
  });
  it('shows service errors separately from incomplete setup and supports retry', () => {
    const get = vi.fn().mockReturnValueOnce(throwError(() => ({ status: 503 }))).mockReturnValue(of({ isReady: true }));
    TestBed.configureTestingModule({ imports: [LearningSetupEntryComponent], providers: [provideRouter([]), { provide: CoachingLearningSetupService, useValue: { get } }] });
    const fixture = TestBed.createComponent(LearningSetupEntryComponent); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('yüklenemedi');
    fixture.componentInstance.load(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('hazır');
  });
});
