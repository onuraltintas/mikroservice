import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { StudentLearningSetupComponent } from './student-learning-setup.component';
import { CoachingLearningSetupService, LearningSetupUpdate, LearningSetupView } from '../../../core/services/coaching-learning-setup.service';

describe('StudentLearningSetupComponent', () => {
  const empty: LearningSetupView = { context: null, nextStep: 'Purpose', isReady: false, missingFields: ['Purpose'], warnings: [] };
  function setup(view = empty) {
    const service = { get: vi.fn(() => of(view)), save: vi.fn((_request: LearningSetupUpdate) => of(view)),
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
  const context = { id: 'ctx', version: 3, academicYearStart: 2026, purpose: 'School' as const, targetExamYear: null,
    primaryScoreType: null, foreignLanguage: null, curriculumVersionId: 'program', coursePreferences: [{ lessonId: 'lesson', priority: 1, selfReportedDifficulty: 3 }], topicStates: [], startingStatusReviewed: false };
  it('prefills existing fields and saves course priorities before starting topics', () => {
    const { component, service } = setup({ ...empty, context, nextStep: 'Courses' });
    expect(component.academicYearStart).toBe(2026); expect(component.included('lesson')).toBe(true);
    component.courses[0].priority = 2; component.changed(); component.saveCourses();
    expect(service.save.mock.calls[0][0]).toMatchObject({ expectedVersion: 3, courses: [{ lessonId: 'lesson', priority: 2 }] });
    expect(component.step()).toBe('Starting');
  });
  it('lets the student record and clear topic declarations across paginated topics', () => {
    const { component, service } = setup({ ...empty, context, nextStep: 'Starting' });
    component.selectedLesson = 'lesson'; component.loadCatalog(2);
    expect(service.catalog).toHaveBeenLastCalledWith('lesson', 2);
    component.setDeclaration('topic', 'NeedsReview'); expect(component.declaration('topic')).toBe('NeedsReview');
    component.setDeclaration('topic', ''); expect(component.declaration('topic')).toBe('');
    component.finishStarting(); expect(service.save).not.toHaveBeenCalled();
    component.startingStatusReviewed = true; component.finishStarting();
    expect(service.save.mock.calls[0][0].startingStatusReviewed).toBe(true);
  });
  it('saves purpose changes with empty old scope and keeps optional school goals absent', () => {
    const { component, service } = setup({ ...empty, context });
    component.purpose = 'General'; component.savePurpose();
    expect(service.save.mock.calls[0][0]).toMatchObject({ courses: [], topics: [], primaryScoreType: null, targetExamYear: null });
  });
  it('requires at least one course and resets reviewed state when the course scope changes', () => {
    const { component, service } = setup();
    component.saveCourses(); expect(service.save).not.toHaveBeenCalled();
    component.toggleCourse('lesson'); expect(component.included('lesson')).toBe(true);
    component.startingStatusReviewed = true; component.toggleCourse('lesson');
    expect(component.included('lesson')).toBe(false); expect(component.startingStatusReviewed).toBe(false);
  });
  it('blocks step changes while dirty and applies the leave confirmation', () => {
    const { component } = setup(); const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component.changed(); component.goTo('Courses'); expect(component.step()).toBe('Purpose');
    expect(component.canLeavePage()).toBe(false);
    const event = new Event('beforeunload', { cancelable: true }); component.beforeUnload(event as BeforeUnloadEvent);
    expect(event.defaultPrevented).toBe(true); confirm.mockRestore();
  });
  it('makes missing profile provisioning available without treating server failure as absence', () => {
    const { component, service } = setup(); service.get.mockReturnValueOnce(throwError(() => ({ status: 404 })));
    component.load(); expect(component.step()).toBe('Profile'); expect(component.ready()).toBe(false);
  });
  it('shows catalog failures with a retry while retaining choices', () => {
    const { component, service } = setup({ ...empty, context });
    service.catalog.mockReturnValueOnce(throwError(() => ({ status: 503 })));
    component.loadCatalog(); expect(component.catalogError()).toContain('yüklenemedi'); expect(component.courses).toHaveLength(1);
    component.loadCatalog(); expect(component.catalogError()).toBeNull();
  });
  it('clears obsolete course scope when profile grade has changed', () => {
    const { component, service } = setup({ ...empty, context, missingFields: ['Purpose'] });
    component.savePurpose();
    expect(service.save).toHaveBeenCalledWith(expect.objectContaining({ courses: [], topics: [] }));
  });
});
