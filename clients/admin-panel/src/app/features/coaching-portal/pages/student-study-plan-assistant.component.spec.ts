import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CoachingWeeklyDraftService, WeeklyProposalMode, WeeklyTempoOption } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingLearningSetupService, LearningContext } from '../../../core/services/coaching-learning-setup.service';
import { CoachingWeeklyOutcomePlanService } from '../../../core/services/coaching-weekly-outcome-plan.service';
import { CoachingStudyPlanningService, StudyAvailability } from '../../../core/services/coaching-study-planning.service';
import { CoachingStudyRoutineService, StudyRoutineAnswers, StudyRoutineUpdate, StudyRoutineView } from '../../../core/services/coaching-study-routine.service';
import { StudentStudyPlanAssistantComponent } from './student-study-plan-assistant.component';
import { routineWindows } from './study-routine';

// Paket H4: the plan wizard is one flow of steps with "Adım 2 / 5", Geri and Devam, and an edit question for an active plan.
describe('StudentStudyPlanAssistantComponent', () => {
  const lessons = [{ id: 'math', name: 'Matematik', examCode: 'LGS', gradeNumber: 8 }];
  const yksLessons = [{ id: 'tyt-mat', name: 'TYT Matematik', examCode: 'TYT', gradeNumber: null }, { id: 'ayt-mat', name: 'AYT Matematik', examCode: 'AYT', gradeNumber: null }];
  const context: LearningContext = { id: 'context', version: 8, academicYearStart: 2026, purpose: 'Lgs', targetExamYear: 2027, primaryScoreType: null, foreignLanguage: null,
    curriculumVersionId: 'curriculum', coursePreferences: [{ lessonId: 'math', priority: 2, selfReportedDifficulty: 3 }],
    topicStates: [{ topicId: 'previous-topic', declaredStatus: 'Studied' }], startingStatusReviewed: true };
  const yksContext: LearningContext = { ...context, purpose: 'Yks', primaryScoreType: 'SAY', coursePreferences: [{ lessonId: 'tyt-mat', priority: 3, selfReportedDifficulty: null },
    { lessonId: 'ayt-mat', priority: 3, selfReportedDifficulty: null }] };
  const answers: StudyRoutineAnswers = { schoolShift: 'Morning', schoolStart: 480, schoolEnd: 840, afterSchoolStart: 900, latestEnd: 1320, morningStudy: false, morningStart: null,
    dayStart: null, dinnerStart: 1140, dinnerEnd: 1200, busyDays: [], busyDaysOff: false, busyStart: null, weekdayMinutes: 180, weekend: 'None', weekendStart: null,
    weekendEnd: null, weekendMinutes: null, examFocus: null };
  const routineOf = (a: StudyRoutineAnswers, matches = true): StudyRoutineView => {
    const w = routineWindows(a);
    return { version: 2, schemaVersion: 1, answers: a, windows: w.windows, dayBudgets: w.budgets, daysWithoutWindows: [], availabilityVersion: 5, currentAvailabilityVersion: 5,
      availabilityMatches: matches, updatedAt: null };
  };
  const hours: StudyAvailability = { version: 5, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 900, endMinute: 1140 }] };
  const three: WeeklyTempoOption[] = [
    { tempo: 'Relaxed', finishBy: '2027-06-13', neededWeeklyMinutes: 300, capacityWeeklyMinutes: 900, fits: true },
    { tempo: 'Balanced', finishBy: '2027-05-12', neededWeeklyMinutes: 360, capacityWeeklyMinutes: 900, fits: true },
    { tempo: 'Intense', finishBy: '2027-04-01', neededWeeklyMinutes: 960, capacityWeeklyMinutes: 900, fits: false }];

  interface Opts { yks?: boolean; routine?: StudyRoutineView | null; availability?: StudyAvailability | null; staff?: string | null; active?: boolean; adim?: string;
    tempo?: WeeklyTempoOption[]; availabilityError?: boolean; cards?: boolean; noContext?: boolean; purposeNotForEducation?: boolean; }
  async function setup(o: Opts = {}) {
    const ctx = o.yks ? yksContext : context;
    const weekly = { get: vi.fn((_s?: string) => of({ draft: o.cards ? { version: 1, timeZoneId: 'Europe/Istanbul', slots: [{ id: 's', lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', day: 'Monday' as const, startMinute: 900, durationMinutes: 60 }] } : null, lessons: o.yks ? yksLessons : lessons, maxDayMinutes: 480 })), save: vi.fn(),
      proposal: vi.fn((mode: WeeklyProposalMode, ..._r: unknown[]) => of({ mode, examFocus: null, slots: [], lessons: [], freeMinutes: 0, neededMinutes: 0, shortageMinutes: 0,
        coveredMinutes: 0, maxDayMinutes: 480, weeksUntilEnd: 20, endDate: '2027-06-13', middleSchool: true, draftVersion: null })),
      tempoOptions: vi.fn((..._a: unknown[]) => of({ planEnd: '2027-06-13', options: o.tempo ?? three })) };
    const view = () => of({ context: o.noContext ? null : ctx, nextStep: 'Preview', isReady: true, missingFields: [], warnings: [], purposeNotForEducation: o.purposeNotForEducation });
    const learning = { get: vi.fn(view), getForStudent: vi.fn((_s: string) => view()), save: vi.fn((_r: unknown) => of({ context: { ...ctx, version: 9 }, nextStep: 'Preview', isReady: true, missingFields: [], warnings: [] })),
      catalog: vi.fn(), catalogForStudent: vi.fn() };
    const engine = { options: vi.fn(() => of({ startDate: '2026-11-09', endDate: '2027-06-13', weeklyDraftVersion: 1, timeZoneId: 'Europe/Istanbul', exams: [] })), preview: vi.fn(), create: vi.fn() };
    const planning = { getAvailability: vi.fn((_s?: string | null) => o.availabilityError ? throwError(() => ({ status: 500 })) : of(o.availability ?? null)),
      list: vi.fn((..._a: unknown[]) => of({ items: o.active ? [{ id: 'p', version: 1, title: 'Plan', status: 'Active' }] : [], totalCount: 0, pageNumber: 1, pageSize: 20 })),
      publish: vi.fn() };
    const routines = { get: vi.fn((_s?: string | null) => of(o.routine ?? null)), save: vi.fn((u: StudyRoutineUpdate) => of({ ...routineOf(u.answers), version: 3 })) };
    TestBed.configureTestingModule({ imports: [StudentStudyPlanAssistantComponent], providers: [provideRouter([]),
      { provide: CoachingWeeklyDraftService, useValue: weekly }, { provide: CoachingLearningSetupService, useValue: learning },
      { provide: CoachingWeeklyOutcomePlanService, useValue: engine }, { provide: CoachingStudyPlanningService, useValue: planning },
      { provide: CoachingStudyRoutineService, useValue: routines },
      ...(o.adim ? [{ provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ adim: o.adim }) } } }] : [])] });
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(StudentStudyPlanAssistantComponent); fixture.componentInstance.staffStudentId = o.staff ?? null;
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, c: fixture.componentInstance, el: fixture.nativeElement as HTMLElement, weekly, learning, engine, planning, routines };
  }
  const words = (el: Element | null) => {
    if (!el) return undefined;
    const parts: string[] = [], walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    while (walker.nextNode()) parts.push(walker.currentNode.textContent ?? '');
    return parts.join(' ').replace(/\s+/g, ' ').trim();
  };
  const button = (el: HTMLElement, text: string) => [...el.querySelectorAll('button')].find(x => words(x) === text) as HTMLButtonElement | undefined;
  const progress = (el: HTMLElement) => el.querySelector('[data-testid="wizard-progress"]')?.textContent?.trim();
  const title = (el: HTMLElement) => el.querySelector('#step-title')?.textContent?.trim();
  async function click(fixture: { detectChanges(): void; whenStable(): Promise<unknown>; nativeElement: HTMLElement }, text: string) {
    button(fixture.nativeElement, text)!.click(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
  }

  it('starts a new student on the questions of step 1 with the progress and the step list', async () => {
    const { el } = await setup();
    expect(progress(el)).toBe('Adım 1 / 4');
    expect(title(el)).toBe('Saatlerin');
    expect([...el.querySelectorAll('.wizard-progress li')].map(x => x.textContent?.trim())).toEqual(['Saatlerin', 'Derslerin', 'Tempo', 'Özet']);
    expect(el.querySelector('.wizard-progress [aria-current=step]')?.textContent?.trim()).toBe('Saatlerin');
    expect(el.querySelector('[data-testid="routine-question"]')?.getAttribute('data-question')).toBe('shift');
  });

  it('adds the exam weight step for YKS (five steps)', async () => {
    const { el } = await setup({ yks: true, routine: routineOf(answers), availability: hours });
    expect(progress(el)).toBe('Adım 1 / 5');
    expect([...el.querySelectorAll('.wizard-progress li')].map(x => x.textContent?.trim())).toEqual(['Saatlerin', 'Derslerin', 'Sınav ağırlığı', 'Tempo', 'Özet']);
  });

  it('goes step by step with Geri and Devam: routine summary, courses, tempo cards, summary with the chosen finish date', async () => {
    const { fixture, el, weekly, routines } = await setup({ routine: routineOf(answers), availability: hours });
    expect(el.querySelector('[data-testid="routine-summary"]')).toBeTruthy();
    await click(fixture, 'Doğru, devam');
    expect(progress(el)).toBe('Adım 2 / 4'); expect(title(el)).toBe('Derslerin');
    // Paket H1 (P8): the Başlangıç durumum line sits with the courses.
    const line = el.querySelector('[data-testid="starting-status"]') as HTMLElement;
    expect(line.textContent?.replace(/\s+/g, ' ').trim()).toBe('“Başlangıç durumum” bölümünde 1 konuyu “Çalıştım” olarak işaretledin. Başlangıç durumunu düzenle');
    expect(line.querySelector('a')?.getAttribute('href')).toBe('/coaching-portal/account/settings?adim=baslangic&donus=sihirbaz');
    await click(fixture, 'Geri');
    expect(title(el)).toBe('Saatlerin');
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam');
    expect(progress(el)).toBe('Adım 3 / 4'); expect(title(el)).toBe('Tempo');
    expect(weekly.tempoOptions).toHaveBeenCalledWith(null, undefined, undefined, 'Replace');
    const cards = [...el.querySelectorAll('[data-testid="tempo-option"]')].map(words);
    expect(cards[0]).toBe('Rahat Konular sınava kadar yayılır. Bitiş: 13 Haziran Haftada yaklaşık 5 saat gerekiyor');
    expect(cards[1]).toBe('Dengeli Sona tekrar için zaman kalır. Bitiş: 12 Mayıs Haftada yaklaşık 6 saat gerekiyor');
    expect(cards[2]).toBe('Yoğun Konuları erken bitirip tekrara geçersin. Bitiş: 1 Nisan Haftada yaklaşık 16 saat gerekiyor Saatlerin buna yetmiyor (haftada 15 saat)');
    (el.querySelectorAll('[data-testid="tempo-option"] input')[1] as HTMLInputElement).click(); fixture.detectChanges();
    await click(fixture, 'Devam');
    // The tempo is kept in the routine (it still writes the free hours).
    expect(routines.save).toHaveBeenCalledWith({ expectedVersion: 2, expectedAvailabilityVersion: 5, answers: { ...answers, tempo: 'Balanced' } });
    expect(progress(el)).toBe('Adım 4 / 4'); expect(title(el)).toBe('Özet');
    expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, undefined, undefined, '2027-05-12');
  });

  it('asks the tempo options in the mode the summary will use (Fill when the week has cards)', async () => {
    const { fixture, weekly } = await setup({ routine: routineOf(answers), availability: hours, cards: true });
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam');
    expect(weekly.tempoOptions).toHaveBeenCalledWith(null, undefined, undefined, 'Fill');
  });

  it('keeps the tempo on the page when the free hours were edited by hand (no routine save)', async () => {
    const { fixture, c, routines, weekly } = await setup({ routine: routineOf(answers, false), availability: hours });
    await click(fixture, 'Bu saatlerle devam'); await click(fixture, 'Devam');
    c.tempo.set('Intense'); await click(fixture, 'Devam');
    expect(routines.save).not.toHaveBeenCalled();
    expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, undefined, undefined, '2027-04-01');
  });

  it('skips the tempo step when only one tempo comes back and says so in the summary', async () => {
    const { fixture, el } = await setup({ routine: routineOf(answers), availability: hours, tempo: [three[0]] });
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam');
    expect(title(el)).toBe('Özet'); expect(progress(el)).toBe('Adım 3 / 3');
    expect(el.querySelector('[data-testid="tempo-note"]')?.textContent?.trim()).toBe('Sınavına kadar tek bir tempo uygun: Rahat, bitiş 13 Haziran.');
    await click(fixture, 'Geri');
    expect(title(el)).toBe('Derslerin');
  });

  it('asks a YKS student the exam weight and keeps it in the routine, then sends it to the tempo options', async () => {
    const { fixture, el, routines, weekly } = await setup({ yks: true, routine: routineOf(answers), availability: hours });
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam');
    expect(title(el)).toBe('Sınav ağırlığı');
    expect(el.textContent).toContain('Bu dönem hangi sınava ağırlık verelim?');
    (el.querySelectorAll('[data-testid="exam-focus"] input')[2] as HTMLInputElement).click(); fixture.detectChanges();
    await click(fixture, 'Devam');
    expect(routines.save).toHaveBeenCalledWith(expect.objectContaining({ answers: expect.objectContaining({ examFocus: 'Ayt' }) }));
    expect(weekly.tempoOptions).toHaveBeenCalledWith('ayt', undefined, undefined, 'Replace');
  });

  it('keeps the exam weight on the page without a routine (as in H2)', async () => {
    const { fixture, c, routines, weekly } = await setup({ yks: true, availability: hours });
    await click(fixture, 'Bu saatlerle devam'); await click(fixture, 'Devam');
    c.setExamFocus('tyt'); await click(fixture, 'Devam');
    expect(routines.save).not.toHaveBeenCalled();
    expect(weekly.tempoOptions).toHaveBeenCalledWith('tyt', undefined, undefined, 'Replace');
  });

  it('starts from the saved exam weight and shows a conflict next to the step, reloading the routine', async () => {
    const { fixture, el, c, routines } = await setup({ yks: true, routine: routineOf({ ...answers, examFocus: 'Tyt' }), availability: hours });
    expect(c.examFocus()).toBe('tyt');
    routines.save.mockReturnValueOnce(throwError(() => ({ status: 409, error: { message: 'Günlük düzenin başka bir yerde değişti; yeniden yükle.' } })));
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam');
    c.setExamFocus('balanced'); await click(fixture, 'Devam');
    expect(el.querySelector('[data-testid="step-error"]')?.textContent).toBe('Günlük düzenin başka bir yerde değişti; yeniden yükle.');
    expect(title(el)).toBe('Sınav ağırlığı');
    expect(routines.get).toHaveBeenCalledTimes(2);
  });

  describe('edit mode', () => {
    it('asks what to change when there is an active plan', async () => {
      const { el } = await setup({ active: true, routine: routineOf(answers), availability: hours });
      expect(el.querySelector('#edit-title')?.textContent).toBe('Planında neyi değiştirmek istiyorsun?');
      expect(['Saatlerim', 'Derslerim', 'Tempo'].map(x => !!button(el, x + ' ' + { Saatlerim: 'Okul, yemek, kurs ve çalışma saatlerin', Derslerim: 'Derslerinin önceliği ve zorluğu',
        Tempo: 'Konuları ne zaman bitireceğin' }[x]))).toEqual([true, true, true]);
      expect(button(el, 'Planı baştan kur')).toBeTruthy();
    });

    it('"Saatlerim" opens the questions with the saved answers, then the summary', async () => {
      const { fixture, el, c } = await setup({ active: true, routine: routineOf(answers), availability: hours });
      c.edit('hours'); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
      expect(progress(el)).toBe('Adım 1 / 2');
      expect(el.querySelector('[data-testid="routine-question"]')?.getAttribute('data-question')).toBe('shift');
      expect((el.querySelectorAll('[data-testid="routine-question"] input')[0] as HTMLInputElement).checked).toBe(true);
      // Geri on the first question returns to the edit question.
      await click(fixture, 'Geri');
      expect(el.querySelector('[data-testid="edit-menu"]')).toBeTruthy();
    });

    it('"Derslerim" opens the courses, then the summary', async () => {
      const { fixture, el, c } = await setup({ active: true, routine: routineOf(answers), availability: hours });
      c.edit('courses'); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
      expect(progress(el)).toBe('Adım 1 / 2'); expect(title(el)).toBe('Derslerin');
      await click(fixture, 'Devam');
      expect(title(el)).toBe('Özet'); expect(progress(el)).toBe('Adım 2 / 2');
    });

    it('"Tempo" opens the tempo with the saved choice selected; "Planı baştan kur" runs every step', async () => {
      const { fixture, el, c } = await setup({ active: true, routine: routineOf({ ...answers, tempo: 'Intense' }), availability: hours });
      c.edit('tempo'); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
      expect(title(el)).toBe('Tempo'); expect(progress(el)).toBe('Adım 1 / 2');
      expect((el.querySelectorAll('[data-testid="tempo-option"] input')[2] as HTMLInputElement).checked).toBe(true);
      await click(fixture, 'Geri');
      await click(fixture, 'Planı baştan kur');
      expect(progress(el)).toBe('Adım 1 / 4');
    });
  });

  it('opens the Özet step directly for the old preview address (?adim=ozet)', async () => {
    const { el, weekly } = await setup({ adim: 'ozet', active: true, routine: routineOf({ ...answers, tempo: 'Balanced' }), availability: hours });
    expect(title(el)).toBe('Özet');
    expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, undefined, undefined, '2027-05-12');
  });

  it('"Saat ekle" in the summary opens the questions and returns to the summary after saving', async () => {
    const { fixture, el, c, routines } = await setup({ adim: 'ozet', routine: routineOf(answers), availability: hours });
    c.addHours(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(title(el)).toBe('Saatlerin');
    expect(el.querySelector('[data-testid="routine-question"]')).toBeTruthy();
    const hoursStep = (c as unknown as { hoursStep: { index: { set(i: number): void }; questions(): string[]; next(): void } }).hoursStep;
    hoursStep.index.set(hoursStep.questions().indexOf('review')); hoursStep.next();
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(routines.save).toHaveBeenCalled();
    expect(title(el)).toBe('Özet');
  });

  describe('teacher view', () => {
    it('shows the routine read only, third person, and never saves it', async () => {
      const { fixture, el, routines, weekly, learning, planning } = await setup({ staff: 'student-1', routine: routineOf(answers), availability: hours });
      expect(el.querySelector('h1')?.textContent).toBe('Öğrenci için plan hazırla');
      expect(routines.get).toHaveBeenCalledWith('student-1'); expect(learning.getForStudent).toHaveBeenCalledWith('student-1');
      expect(planning.list).not.toHaveBeenCalled();
      expect(title(el)).toBe('Saatleri');
      expect(button(el, 'Değiştir')).toBeUndefined();
      await click(fixture, 'Devam'); expect(title(el)).toBe('Dersleri');
      await click(fixture, 'Devam'); expect(el.textContent).toContain('Öğrenci konuları ne zaman bitirsin?');
      await click(fixture, 'Devam');
      expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, 'student-1', undefined, '2027-06-13');
      expect(routines.save).not.toHaveBeenCalled();
      expect(el.querySelectorAll('a[href*="/coaching-portal"]').length).toBe(0);
    });

    it('sends the quick answers as windows to the tempo options and the proposal for a student without hours', async () => {
      const { fixture, el, weekly } = await setup({ staff: 'student-1' });
      button(el, 'Salı')!.click(); await click(fixture, 'Devam');
      await click(fixture, 'Devam');
      const windows = [{ day: 'Tuesday', startMinute: 1080, endMinute: 1140 }];
      expect(weekly.tempoOptions).toHaveBeenCalledWith(null, 'student-1', windows, 'Replace');
      await click(fixture, 'Devam');
      expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, 'student-1', windows, '2027-06-13');
    });

    it('tells the teacher the count of studied topics in third person, without the link', async () => {
      const { fixture, el } = await setup({ staff: 'student-1', routine: routineOf(answers) });
      await click(fixture, 'Devam');
      const line = el.querySelector('[data-testid="starting-status"]') as HTMLElement;
      expect(line.textContent?.replace(/\s+/g, ' ').trim()).toBe('Öğrenci “Başlangıç durumum” bölümünde 1 konuyu “Çalıştım” olarak işaretledi.');
      expect(line.querySelector('a')).toBeNull();
    });
  });

  it('asks to retry a failed load of the hours instead of asking the questions', async () => {
    const { el } = await setup({ availabilityError: true });
    expect(el.querySelector('[data-testid="availability-load-error"]')?.textContent).toContain('Çalışma saatlerin yüklenemedi.');
    expect(el.querySelector('[data-testid="routine-question"]')).toBeNull();
  });

  it('asks a student without study preferences to complete them first', async () => {
    const { el } = await setup({ noContext: true });
    expect(el.querySelector('[data-testid="preferences-missing"]')?.textContent).toContain('önce çalışma tercihlerini tamamla');
    expect(el.querySelector('[data-testid="routine-question"]')).toBeNull();
  });

  it('asks to update the exam when it no longer fits the grade', async () => {
    const { el } = await setup({ purposeNotForEducation: true });
    expect(el.querySelector('[data-testid="grade-purpose"]')?.textContent).toContain('Seçili sınavın eğitim durumuna ya da sınıfına uymuyor');
  });

  it('no longer offers the topic-based plan and shows no technical note', async () => {
    const { el } = await setup({ routine: routineOf(answers), availability: hours });
    expect(el.textContent).not.toContain('Konulardan plan');
    expect(el.querySelector('app-student-automatic-plan')).toBeNull();
    expect(el.textContent).not.toMatch(/Yapay zekâ|kurallı olarak değerlendirilir/);
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, { useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } }); });
