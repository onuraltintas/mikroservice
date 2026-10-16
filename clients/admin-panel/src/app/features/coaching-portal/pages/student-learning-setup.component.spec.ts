import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { StudentLearningSetupComponent } from './student-learning-setup.component';
import { CoachingLearningSetupService, LearningSetupView } from '../../../core/services/coaching-learning-setup.service';

describe('StudentLearningSetupComponent', () => {
  const empty: LearningSetupView = { context: null, nextStep: 'Purpose', isReady: false, missingFields: ['Purpose'], warnings: [] };
  function setup(view = empty) {
    const service = { get: vi.fn(() => of(view)), save: vi.fn(() => of(view)),
      catalog: vi.fn(() => of({ curriculum: null, lessons: [], topics: { items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: ['Katalog hazırlanıyor.'] })) };
    TestBed.configureTestingModule({ imports: [StudentLearningSetupComponent], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: CoachingLearningSetupService, useValue: service }] });
    const fixture = TestBed.createComponent(StudentLearningSetupComponent); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, service };
  }
  it('keeps plan and assignment links visible without completing setup', () => {
    const { fixture } = setup();
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/study-plans"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/assignments"]')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Çalışma amacım');
  });
  it('requires conditional exam fields and preserves input on save failure', () => {
    const { component, service, fixture } = setup();
    component.purpose = 'Yks'; component.targetExamYear = null; component.savePurpose();
    expect(service.save).not.toHaveBeenCalled();
    component.targetExamYear = 2027; component.primaryScoreType = 'DIL'; component.foreignLanguage = 'English';
    service.save.mockReturnValueOnce(throwError(() => ({ status: 409 })));
    component.savePurpose(); fixture.detectChanges();
    expect(component.foreignLanguage).toBe('English');
    expect(fixture.nativeElement.textContent).toContain('başka bir oturumda');
  });
  it('resumes from server state and reports catalog absence', () => {
    const { component, fixture } = setup({ ...empty, nextStep: 'Catalog' });
    expect(component.step()).toBe('Courses');
    expect(fixture.nativeElement.textContent).toContain('Katalog hazırlanıyor.');
    expect(fixture.nativeElement.textContent).toContain('Mevcut planlarım');
  });
  it('prevents repeated saves and leaving during a pending request', () => {
    const { component, service } = setup();
    const pending = new Subject<LearningSetupView>(); service.save.mockReturnValueOnce(pending);
    component.purpose = 'School'; component.savePurpose(); component.savePurpose();
    expect(service.save).toHaveBeenCalledTimes(1); expect(component.canLeavePage()).toBe(false);
    pending.next(empty); pending.complete(); expect(component.saving()).toBe(false);
  });
  it('shows a retryable load error without treating it as an empty profile', () => {
    const { component, service, fixture } = setup();
    service.get.mockReturnValueOnce(throwError(() => ({ status: 503 })));
    component.load(); fixture.detectChanges();
    expect(component.ready()).toBe(false); expect(fixture.nativeElement.textContent).toContain('yüklenemedi');
  });
});
