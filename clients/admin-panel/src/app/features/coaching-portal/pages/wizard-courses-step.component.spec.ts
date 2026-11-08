import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CoachingLearningSetupService, LearningContext } from '../../../core/services/coaching-learning-setup.service';
import { WizardCoursesStepComponent, difficultyChoice, priorityChoice } from './wizard-courses-step.component';

// Paket H4, step 2 "Derslerin": two plain choices per course, saved through the study preferences save with its version.
describe('WizardCoursesStepComponent', () => {
  const context: LearningContext = { id: 'c', version: 7, academicYearStart: 2026, purpose: 'Yks', targetExamYear: 2027, targetExamSessionId: 'session',
    primaryScoreType: 'SAY', foreignLanguage: null, curriculumVersionId: 'cur', startingStatusReviewed: true,
    topicStates: [{ topicId: 't1', declaredStatus: 'Studied' }],
    coursePreferences: [{ lessonId: 'tyt-mat', priority: 2, selfReportedDifficulty: 1 }, { lessonId: 'ayt-mat', priority: 3, selfReportedDifficulty: null },
      { lessonId: 'ayt-fiz', priority: 4, selfReportedDifficulty: 5 }] };
  const lessons = [{ id: 'tyt-mat', name: 'TYT Matematik', examCode: 'TYT', gradeNumber: null }, { id: 'ayt-mat', name: 'AYT Matematik', examCode: 'AYT', gradeNumber: null },
    { id: 'ayt-fiz', name: 'AYT Fizik', examCode: 'AYT', gradeNumber: null }];
  async function setup(staff = false, save = vi.fn((_: unknown) => of({ context: { ...context, version: 8 }, nextStep: 'Preview', isReady: true, missingFields: [], warnings: [] }))) {
    const toaster = { confirm: vi.fn().mockResolvedValue(false) };
    TestBed.configureTestingModule({ imports: [WizardCoursesStepComponent], providers: [provideRouter([]), { provide: CoachingLearningSetupService, useValue: { save } },
      { provide: ToasterService, useValue: toaster }] });
    const fixture = TestBed.createComponent(WizardCoursesStepComponent), c = fixture.componentInstance;
    c.context = context; c.lessons = lessons; c.staffStudentId = staff ? 'student-1' : null; c.ngOnChanges();
    const done = vi.fn(), saved = vi.fn(), reload = vi.fn();
    c.done.subscribe(done); c.saved.subscribe(saved); c.reload.subscribe(reload);
    fixture.detectChanges();
    const back = vi.fn(); c.back.subscribe(back);
    return { fixture, c, el: fixture.nativeElement as HTMLElement, save, done, saved, reload, back, toaster };
  }
  const radio = (el: HTMLElement, group: string, label: string) =>
    [...[...el.querySelectorAll('fieldset')].find(f => f.querySelector('legend')?.textContent?.trim() === group)!
      .querySelectorAll('label')].find(l => l.textContent?.trim() === label)!.querySelector('input') as HTMLInputElement;

  it('maps saved values to the nearest choice', () => {
    expect([1, 2, 3, 4, 5].map(priorityChoice)).toEqual([1, 1, 3, 5, 5]);
    expect([null, 1, 2, 3, 4, 5].map(difficultyChoice)).toEqual([3, 2, 2, 3, 4, 4]);
  });

  it('groups the courses by exam with labelled choices showing the nearest saved value', async () => {
    const { el } = await setup();
    expect([...el.querySelectorAll('.course-group h3')].map(x => x.textContent)).toEqual(['TYT', 'AYT']);
    expect(radio(el, 'TYT Matematik önceliği', 'Yüksek').checked).toBe(true);
    expect(radio(el, 'TYT Matematik zorluğu', 'Kolay').checked).toBe(true);
    expect(radio(el, 'AYT Fizik önceliği', 'Düşük').checked).toBe(true);
    expect(radio(el, 'AYT Fizik zorluğu', 'Zor').checked).toBe(true);
    expect(radio(el, 'AYT Matematik zorluğu', 'Normal').checked).toBe(true);
    expect(el.querySelector('a')?.getAttribute('href')).toBe('/coaching-portal/account/settings?adim=dersler&donus=sihirbaz');
    expect(el.querySelector('a')?.textContent).toBe('Ders ekle/çıkar');
  });

  it('goes on without saving when nothing changed', async () => {
    const { c, save, done } = await setup();
    c.next();
    expect(save).not.toHaveBeenCalled(); expect(done).toHaveBeenCalled();
  });

  it('saves only the changed course as 1/3/5 and 2/3/4 with the version, keeping 1 and 5 set elsewhere', async () => {
    const { el, fixture, save, done, saved } = await setup();
    radio(el, 'AYT Matematik önceliği', 'Yüksek').click();
    radio(el, 'AYT Matematik zorluğu', 'Zor').click();
    fixture.detectChanges();
    (el.querySelector('[data-testid="courses-next"]') as HTMLButtonElement).click();
    expect(save).toHaveBeenCalledWith(expect.objectContaining({ expectedVersion: 7, purpose: 'Yks', primaryScoreType: 'SAY', targetExamSessionId: 'session',
      topics: context.topicStates, startingStatusReviewed: true,
      courses: [{ lessonId: 'tyt-mat', priority: 2, selfReportedDifficulty: 1 }, { lessonId: 'ayt-mat', priority: 1, selfReportedDifficulty: 4 },
        { lessonId: 'ayt-fiz', priority: 4, selfReportedDifficulty: 5 }] }));
    expect(saved).toHaveBeenCalled(); expect(done).toHaveBeenCalled();
  });

  it('reloads and says so after a conflict', async () => {
    const { el, fixture, c, reload, done } = await setup(false, vi.fn(() => throwError(() => ({ status: 409, error: { message: 'x' } }))));
    radio(el, 'AYT Fizik önceliği', 'Normal').click(); c.next(); fixture.detectChanges();
    expect(reload).toHaveBeenCalled(); expect(done).not.toHaveBeenCalled();
    expect(el.querySelector('[role=alert]')?.textContent).toContain('Derslerin başka bir yerde değişti');
  });

  it('shows a refused course\'s message in its own row', async () => {
    const { el, fixture, c } = await setup(false, vi.fn(() => throwError(() => ({ status: 400, error: { field: 'courses', lessonId: 'ayt-fiz', message: 'Bu ders seçilemez.' } }))));
    radio(el, 'AYT Fizik zorluğu', 'Kolay').click(); c.next(); fixture.detectChanges();
    const rows = [...el.querySelectorAll('[data-testid="course-row"]')];
    expect(rows[2].querySelector('.field-error')?.textContent).toBe('Bu ders seçilemez.');
    expect(rows[0].querySelector('.field-error')).toBeNull();
    // S12: both choices of the row name the message and are marked invalid.
    for (const fieldset of rows[2].querySelectorAll('fieldset')) {
      expect(fieldset.getAttribute('aria-invalid')).toBe('true'); expect(fieldset.getAttribute('aria-describedby')).toBe('course-error-ayt-fiz');
    }
    expect(rows[2].querySelector('#course-error-ayt-fiz')).toBeTruthy();
    expect(rows[0].querySelector('fieldset')?.getAttribute('aria-invalid')).toBeNull();
  });

  it('asks before Geri drops unsaved choices', async () => {
    const { el, fixture, c, back, toaster } = await setup();
    c.canGoBack = true; fixture.detectChanges();
    radio(el, 'AYT Fizik önceliği', 'Normal').click(); fixture.detectChanges();
    await c.goBack();
    expect(toaster.confirm).toHaveBeenCalled(); expect(back).not.toHaveBeenCalled();
    toaster.confirm.mockResolvedValueOnce(true);
    await c.goBack();
    expect(back).toHaveBeenCalled(); expect(c.dirty()).toBe(false);
  });

  it('lets a teacher only read the choices, in third person', async () => {
    const { el, c, save, done } = await setup(true);
    expect(el.querySelectorAll('input[type=radio]').length).toBe(0);
    expect(el.textContent).toContain('Öncelik: Yüksek · Zorluk: Kolay');
    expect(el.textContent).toContain('Bunları yalnız öğrenci değiştirebilir.');
    expect(el.querySelector('a')).toBeNull();
    c.next(); expect(save).not.toHaveBeenCalled(); expect(done).toHaveBeenCalled();
  });
});
