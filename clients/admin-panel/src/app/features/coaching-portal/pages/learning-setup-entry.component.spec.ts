import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LearningSetupEntryComponent } from './learning-setup-entry.component';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';

describe('Learning setup entry', () => {
  it('offers a resumable setup without redirecting away from current work', async () => {
    TestBed.configureTestingModule({ imports: [LearningSetupEntryComponent], providers: [provideRouter([]), { provide: CoachingLearningSetupService,
      useValue: { get: () => of({ context: { id: 'context' }, isReady: false, nextStep: 'Availability' }) } }] });
    const fixture = TestBed.createComponent(LearningSetupEntryComponent); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Kuruluma devam et');
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/plan"]')).toBeTruthy();
  });
  it('shows service errors separately from incomplete setup and supports retry', async () => {
    const get = vi.fn().mockReturnValueOnce(throwError(() => ({ status: 503 }))).mockReturnValue(of({ isReady: true }));
    TestBed.configureTestingModule({ imports: [LearningSetupEntryComponent], providers: [provideRouter([]), { provide: CoachingLearningSetupService, useValue: { get } }] });
    const fixture = TestBed.createComponent(LearningSetupEntryComponent); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('yüklenemedi');
    await fixture.componentInstance.load(); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('hazır');
  });
  it('distinguishes missing provisioning from denied access', async () => {
    const get = vi.fn().mockReturnValueOnce(throwError(() => ({ status: 404 }))).mockReturnValueOnce(throwError(() => ({ status: 403 })));
    TestBed.configureTestingModule({ imports: [LearningSetupEntryComponent], providers: [provideRouter([]), { provide: CoachingLearningSetupService, useValue: { get } }] });
    const fixture = TestBed.createComponent(LearningSetupEntryComponent); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.componentInstance.error()).toBeNull();
    await fixture.componentInstance.load(); expect(fixture.componentInstance.error()).toContain('doğrulanamadı');
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
