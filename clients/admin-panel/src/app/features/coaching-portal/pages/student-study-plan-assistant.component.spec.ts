import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { NEVER, of, throwError } from 'rxjs';
import { By } from '@angular/platform-browser';
import { WizardHoursStepComponent } from './wizard-hours-step.component';
import { WizardSummaryStepComponent } from './wizard-summary-step.component';
import { CoachingWeeklyDraftService, WeeklyProposalMode, WeeklyTempoOption } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingLearningSetupService, LearningContext } from '../../../core/services/coaching-learning-setup.service';
import { CoachingWeeklyOutcomePlanService } from '../../../core/services/coaching-weekly-outcome-plan.service';
import { CoachingStudyPlanningService, StudyAvailability } from '../../../core/services/coaching-study-planning.service';
import { CoachingStudyRoutineService, StudyRoutineAnswers, StudyRoutineUpdate, StudyRoutineView } from '../../../core/services/coaching-study-routine.service';
import { CoachingPlanPreferencesService, StudyPlanPreferences } from '../../../core/services/coaching-plan-preferences.service';
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
  const card = { id: 's', lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', day: 'Monday' as const, startMinute: 900, durationMinutes: 60 };

  interface Opts { yks?: boolean; routine?: StudyRoutineView | null; availability?: StudyAvailability | null; staff?: string | null; active?: boolean; activeEnd?: string;
    draft?: boolean; adim?: string; tempo?: WeeklyTempoOption[]; availabilityError?: boolean; cards?: boolean; yeniden?: boolean; noContext?: boolean;
    purposeNotForEducation?: boolean; prefs?: StudyPlanPreferences | null; shortage?: boolean; education?: { educationStatus: string; gradeLevel: number | null } }
  async function setup(o: Opts = {}) {
    const ctx = o.yks ? yksContext : context;
    const weekly = { get: vi.fn((_s?: string) => of({ draft: o.cards ? { version: 1, timeZoneId: 'Europe/Istanbul', slots: [card] } : null, lessons: o.yks ? yksLessons : lessons, maxDayMinutes: 480 })),
      save: vi.fn(),
      proposal: vi.fn((mode: WeeklyProposalMode, ..._r: unknown[]) => of({ mode, examFocus: null, slots: o.shortage ? [{ ...card, id: 'p1', day: 'Tuesday' as const }] : [],
        lessons: o.shortage ? [{ lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', existingMinutes: 0, proposedMinutes: 60, neededMinutes: 120, noTime: false }] : [],
        freeMinutes: 60, neededMinutes: o.shortage ? 120 : 0, shortageMinutes: o.shortage ? 60 : 0, coveredMinutes: 60, maxDayMinutes: 480, weeksUntilEnd: 20,
        endDate: '2027-06-13', middleSchool: true, draftVersion: o.cards ? 1 : null })),
      tempoOptions: vi.fn((..._a: unknown[]) => of({ planEnd: '2027-06-13', options: o.tempo ?? three })) };
    const view = () => of({ context: o.noContext ? null : ctx, nextStep: 'Preview', isReady: true, missingFields: [], warnings: [], purposeNotForEducation: o.purposeNotForEducation,
      educationStatus: o.education?.educationStatus ?? (o.yks ? 'Student' : 'Student'), gradeLevel: o.education ? o.education.gradeLevel : (o.yks ? 11 : 8) });
    const learning = { get: vi.fn(view), getForStudent: vi.fn((_s: string) => view()), save: vi.fn((_r: unknown) => of({ context: { ...ctx, version: 9 }, nextStep: 'Preview', isReady: true, missingFields: [], warnings: [] })),
      catalog: vi.fn(), catalogForStudent: vi.fn() };
    const engine = { options: vi.fn(() => of({ startDate: '2026-11-09', endDate: '2027-06-13', weeklyDraftVersion: 1, timeZoneId: 'Europe/Istanbul', exams: [] })), preview: vi.fn(), create: vi.fn() };
    const planning = { getAvailability: vi.fn((_s?: string | null) => o.availabilityError ? throwError(() => ({ status: 500 })) : of(o.availability ?? null)),
      list: vi.fn((_p: number, status?: string) => of({ items: status === 'Active' && o.active ? [{ id: 'p', version: 1, title: 'Plan', status: 'Active' }]
        : status === 'Draft' && o.draft ? [{ id: 'd', version: 1, title: 'Bekleyen planım', status: 'Draft' }] : [], totalCount: 0, pageNumber: 1, pageSize: 20 })),
      get: vi.fn((..._a: unknown[]) => of({ id: 'p', version: 1, title: 'Plan', status: 'Active', tasks: [], weeklySource: o.activeEnd ? { endDate: o.activeEnd } : null })),
      publish: vi.fn() };
    const routines = { get: vi.fn((_s?: string | null) => of(o.routine ?? null)), save: vi.fn((u: StudyRoutineUpdate) => of({ ...routineOf(u.answers), version: 3 })) };
    const prefs = { get: vi.fn((_s?: string | null) => of(o.prefs ?? null)), save: vi.fn((u: StudyPlanPreferences) => of({ ...u, version: 1, updatedAt: null })) };
    TestBed.configureTestingModule({ imports: [StudentStudyPlanAssistantComponent], providers: [provideRouter([]),
      { provide: CoachingWeeklyDraftService, useValue: weekly }, { provide: CoachingLearningSetupService, useValue: learning },
      { provide: CoachingWeeklyOutcomePlanService, useValue: engine }, { provide: CoachingStudyPlanningService, useValue: planning },
      { provide: CoachingStudyRoutineService, useValue: routines }, { provide: CoachingPlanPreferencesService, useValue: prefs },
      ...(o.adim || o.yeniden ? [{ provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(o.yeniden ? { yeniden: 'oner' } : { adim: o.adim! }) } } }] : [])] });
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(StudentStudyPlanAssistantComponent); fixture.componentInstance.staffStudentId = o.staff ?? null;
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, c: fixture.componentInstance, el: fixture.nativeElement as HTMLElement, weekly, learning, engine, planning, routines, prefs,
      toaster: TestBed.inject(ToasterService) as unknown as { confirm: ReturnType<typeof vi.fn> } };
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
  const pills = (el: HTMLElement) => [...el.querySelectorAll('.wizard-progress li')].map(words);
  async function settle(fixture: { detectChanges(): void; whenStable(): Promise<unknown> }) { fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges(); }
  async function click(fixture: { detectChanges(): void; whenStable(): Promise<unknown>; nativeElement: HTMLElement }, text: string) {
    const target = button(fixture.nativeElement, text);
    if (!target) throw new Error(`no button "${text}"`);
    target.click(); await settle(fixture);
  }

  it('starts a new student on the questions of step 1 with the progress and the step list', async () => {
    const { el } = await setup();
    expect(progress(el)).toBe('Adım 1 / 4');
    expect(title(el)).toBe('Saatlerin');
    expect(pills(el)).toEqual(['Saatlerin', 'Derslerin', 'Tempo', 'Özet']);
    expect(el.querySelector('.wizard-progress [aria-current=step]')?.textContent?.trim()).toBe('Saatlerin');
    expect(el.querySelector('[data-testid="routine-question"]')?.getAttribute('data-question')).toBe('shift');
    // The step is announced once, by the focused heading (no second live region).
    expect(el.querySelector('.wizard-panel [aria-live]')).toBeNull();
    expect(document.activeElement?.id).toBe('step-title');
  });

  it('adds the exam weight step for YKS (five steps)', async () => {
    const { el } = await setup({ yks: true, routine: routineOf(answers), availability: hours });
    expect(progress(el)).toBe('Adım 1 / 5');
    expect(pills(el)).toEqual(['Saatlerin', 'Derslerin', 'Sınav ağırlığı', 'Tempo', 'Özet']);
  });

  it('offers "Okula gitmiyorum" to everyone but a middle-school student', async () => {
    const shift = async (o: Opts) => { const { el } = await setup(o); const text = words(el.querySelector('[role=radiogroup]')); TestBed.resetTestingModule(); return text; };
    expect(await shift({})).not.toContain('Okula gitmiyorum');
    TestBed.overrideProvider(ToasterService, { useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } });
    expect(await shift({ yks: true })).toContain('Okula gitmiyorum');
    TestBed.overrideProvider(ToasterService, { useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } });
    expect(await shift({ yks: true, education: { educationStatus: 'HighSchoolGraduate', gradeLevel: null } })).toContain('Okula gitmiyorum');
  });

  it('goes step by step with Geri and Devam and keeps the tempo in the plan preferences, never in the routine', async () => {
    const { fixture, el, weekly, routines, prefs } = await setup({ routine: routineOf(answers), availability: hours });
    expect(el.querySelector('[data-testid="routine-summary"]')).toBeTruthy();
    await click(fixture, 'Doğru, devam');
    expect(progress(el)).toBe('Adım 2 / 4'); expect(title(el)).toBe('Derslerin');
    expect(pills(el)[0]).toBe('Saatlerin (tamamlandı)');
    // Paket H1 (P8): the Başlangıç durumum line sits with the courses.
    const line = el.querySelector('[data-testid="starting-status"]') as HTMLElement;
    expect(line.textContent?.replace(/\s+/g, ' ').trim()).toBe('“Başlangıç durumum” bölümünde 1 konuyu “Çalıştım” olarak işaretledin. Başlangıç durumunu düzenle');
    await click(fixture, 'Geri');
    expect(title(el)).toBe('Saatlerin');
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam');
    expect(progress(el)).toBe('Adım 3 / 4'); expect(title(el)).toBe('Tempo');
    expect(weekly.tempoOptions).toHaveBeenCalledWith(null, undefined, undefined, 'Replace');
    const cards = [...el.querySelectorAll('[data-testid="tempo-option"]')].map(words);
    expect(cards[0]).toBe('Rahat Konular sınava kadar yayılır. Bitiş: 13 Haziran Haftada yaklaşık 5 saat gerekiyor');
    expect(cards[1]).toBe('Dengeli Sona tekrar için zaman kalır. Bitiş: 12 Mayıs Haftada yaklaşık 6 saat gerekiyor');
    // S9: the plain reason a tempo does not fit.
    expect(cards[2]).toBe('Yoğun Konuları erken bitirip tekrara geçersin. Bitiş: 1 Nisan Haftada yaklaşık 16 saat gerekiyor Haftada 15 saatin var; bu tempo 16 saat istiyor.');
    (el.querySelectorAll('[data-testid="tempo-option"] input')[1] as HTMLInputElement).click(); fixture.detectChanges();
    await click(fixture, 'Devam');
    expect(prefs.save).toHaveBeenCalledWith({ examFocus: null, tempo: 'Balanced' });
    expect(routines.save).not.toHaveBeenCalled();
    expect(progress(el)).toBe('Adım 4 / 4'); expect(title(el)).toBe('Özet');
    expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, undefined, undefined, '2027-05-12');
  });

  it('says when the hours are enough in total but not once spread over the days, in third person for a teacher', async () => {
    const spread: WeeklyTempoOption[] = [three[0], { ...three[2], neededWeeklyMinutes: 600, capacityWeeklyMinutes: 900 }];
    const { fixture, el } = await setup({ routine: routineOf(answers), availability: hours, tempo: spread });
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam');
    expect(words(el.querySelectorAll('[data-testid="tempo-option"]')[1])).toContain('Saatlerin günlere dağılınca bu tempoya yetmiyor.');
    TestBed.resetTestingModule();
    TestBed.overrideProvider(ToasterService, { useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } });
    const staff = await setup({ staff: 'student-1', routine: routineOf(answers), availability: hours });
    await click(staff.fixture, 'Devam'); await click(staff.fixture, 'Devam');
    expect(words(staff.el.querySelectorAll('[data-testid="tempo-option"]')[2])).toContain('Öğrencinin haftada 15 saati var; bu tempo 16 saat istiyor.');
  });

  it('asks the tempo options in the mode the summary will use (Fill when the week has cards)', async () => {
    const { fixture, weekly } = await setup({ routine: routineOf(answers), availability: hours, cards: true });
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam');
    expect(weekly.tempoOptions).toHaveBeenCalledWith(null, undefined, undefined, 'Fill');
  });

  it('keeps the tempo in the plan preferences even when the free hours were edited by hand', async () => {
    const { fixture, c, routines, weekly, prefs } = await setup({ routine: routineOf(answers, false), availability: hours });
    await click(fixture, 'Bu saatlerle devam'); await click(fixture, 'Devam');
    c.tempo.set('Intense'); await click(fixture, 'Devam');
    expect(prefs.save).toHaveBeenCalledWith({ examFocus: null, tempo: 'Intense' });
    expect(routines.save).not.toHaveBeenCalled();
    expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, undefined, undefined, '2027-04-01');
  });

  it('preselects the saved tempo, else the one ending closest to the active plan', async () => {
    const saved = await setup({ routine: routineOf(answers), availability: hours, prefs: { examFocus: null, tempo: 'Intense' } });
    await click(saved.fixture, 'Doğru, devam'); await click(saved.fixture, 'Devam');
    expect(saved.c.tempo()).toBe('Intense');
    TestBed.resetTestingModule();
    TestBed.overrideProvider(ToasterService, { useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } });
    const closest = await setup({ routine: routineOf(answers), availability: hours, active: true, activeEnd: '2027-05-10' });
    closest.c.edit('tempo'); await settle(closest.fixture);
    expect(closest.c.tempo()).toBe('Balanced');
    expect((closest.el.querySelectorAll('[data-testid="tempo-option"] input')[1] as HTMLInputElement).checked).toBe(true);
  });

  // H4a: options whose dates do not settle are dropped; a saved tempo that is gone falls back to the closest date, with a note.
  it('falls back to the closest tempo when the saved one is no longer offered, and says so', async () => {
    const { fixture, el, c } = await setup({ routine: routineOf(answers), availability: hours, tempo: [three[0], three[1]], prefs: { examFocus: null, tempo: 'Intense' },
      active: true, activeEnd: '2027-05-20' });
    c.edit('tempo'); await settle(fixture);
    expect(c.tempo()).toBe('Balanced');
    expect(el.querySelector('[data-testid="tempo-replaced"]')?.textContent).toContain('Kayıtlı temponun bitiş tarihi artık uygun değil');
  });

  it('reads the stored focus and tempo from the plan preferences, and a teacher reads the student\'s', async () => {
    const own = await setup({ yks: true, routine: routineOf(answers), availability: hours, prefs: { examFocus: 'Ayt', tempo: 'Balanced' } });
    expect(own.prefs.get).toHaveBeenCalledWith(null);
    expect(own.c.examFocus()).toBe('ayt'); expect(own.c.tempo()).toBe('Balanced');
    TestBed.resetTestingModule();
    TestBed.overrideProvider(ToasterService, { useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } });
    const staff = await setup({ staff: 'student-1', yks: true, routine: routineOf(answers), availability: hours, prefs: { examFocus: 'Tyt', tempo: null } });
    expect(staff.prefs.get).toHaveBeenCalledWith('student-1');
    expect(staff.c.examFocus()).toBe('tyt');
  });

  // H4a review: the dates may be in any order and Relaxed may be missing; each option keeps its own date and name.
  it('uses each option\'s own date and name when the options are out of order or Relaxed is missing', async () => {
    const odd: WeeklyTempoOption[] = [{ tempo: 'Intense', finishBy: '2027-05-30', neededWeeklyMinutes: 420, capacityWeeklyMinutes: 900, fits: true },
      { tempo: 'Balanced', finishBy: '2027-04-10', neededWeeklyMinutes: 480, capacityWeeklyMinutes: 900, fits: true }];
    const { fixture, el, c, weekly } = await setup({ routine: routineOf(answers), availability: hours, tempo: odd, active: true, activeEnd: '2027-04-01' });
    c.edit('tempo'); await settle(fixture);
    expect([...el.querySelectorAll('[data-testid="tempo-option"]')].map(x => words(x)?.split(' Haftada')[0]))
      .toEqual(['Yoğun Konuları erken bitirip tekrara geçersin. Bitiş: 30 Mayıs', 'Dengeli Sona tekrar için zaman kalır. Bitiş: 10 Nisan']);
    // The closest to the active plan's end (1 April) is Dengeli, even though it is listed second.
    expect(c.tempo()).toBe('Balanced');
    await click(fixture, 'Devam');
    expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, undefined, undefined, '2027-04-10');
  });

  it('tries a racing preferences save once more', async () => {
    const { fixture, c, prefs } = await setup({ routine: routineOf(answers), availability: hours, active: true });
    prefs.save.mockReturnValueOnce(throwError(() => ({ status: 409, error: { code: 'StudyPlanning.Conflict', message: 'x' } })));
    c.edit('tempo'); await settle(fixture);
    c.tempo.set('Intense'); await click(fixture, 'Devam');
    expect(prefs.save).toHaveBeenCalledTimes(2);
    expect(c.step()).toBe('summary');
  });

  it('skips the tempo step when only one tempo comes back and says so in the summary', async () => {
    const { fixture, el } = await setup({ routine: routineOf(answers), availability: hours, tempo: [three[0]] });
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam');
    expect(title(el)).toBe('Özet'); expect(progress(el)).toBe('Adım 3 / 3');
    expect(el.querySelector('[data-testid="tempo-note"]')?.textContent?.trim()).toBe('Sınavına kadar tek bir tempo uygun: Rahat, bitiş 13 Haziran.');
    await click(fixture, 'Geri');
    expect(title(el)).toBe('Derslerin');
  });

  it('asks a YKS student the exam weight, keeps it in the plan preferences and sends it to the tempo options', async () => {
    const { fixture, el, routines, weekly, prefs } = await setup({ yks: true, routine: routineOf(answers), availability: hours });
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam');
    expect(title(el)).toBe('Sınav ağırlığı');
    expect(el.textContent).toContain('Bu dönem hangi sınava ağırlık verelim?');
    (el.querySelectorAll('[data-testid="exam-focus"] input')[2] as HTMLInputElement).click(); fixture.detectChanges();
    await click(fixture, 'Devam');
    expect(prefs.save).toHaveBeenCalledWith({ examFocus: 'Ayt', tempo: null });
    expect(routines.save).not.toHaveBeenCalled();
    expect(weekly.tempoOptions).toHaveBeenCalledWith('ayt', undefined, undefined, 'Replace');
  });

  it('starts from the saved exam weight and shows a failed save next to the step', async () => {
    const { fixture, el, c, prefs } = await setup({ yks: true, routine: routineOf({ ...answers, examFocus: 'Tyt' }), availability: hours });
    expect(c.examFocus()).toBe('tyt');
    prefs.save.mockReturnValueOnce(throwError(() => ({ status: 400, error: { message: 'TYT ve AYT ağırlığını seç.' } })));
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam');
    c.setExamFocus('balanced'); await click(fixture, 'Devam');
    expect(el.querySelector('[data-testid="step-error"]')?.textContent).toBe('TYT ve AYT ağırlığını seç.');
    expect(title(el)).toBe('Sınav ağırlığı');
  });

  // S5: a tempo that cannot be read stops the way to the summary, with a retry where the student is.
  it('stays on the step with a retry when the tempo options cannot be read, and proposes nothing', async () => {
    const { fixture, el, c, weekly } = await setup({ active: true, routine: routineOf(answers), availability: hours });
    weekly.tempoOptions.mockReturnValueOnce(throwError(() => ({ status: 500 })));
    c.edit('courses'); await settle(fixture);
    await click(fixture, 'Devam');
    expect(title(el)).toBe('Derslerin');
    expect(el.querySelector('[data-testid="tempo-error"]')?.textContent).toContain('Tempo seçenekleri hazırlanamadı.');
    expect(weekly.proposal).not.toHaveBeenCalled();
    await click(fixture, 'Yeniden dene');
    expect(title(el)).toBe('Özet');
    expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, undefined, undefined, '2027-06-13');
  });

  // S14: other hours after a reload mean other tempo options.
  it('reads the tempo options again when reloaded hours differ', async () => {
    const { fixture, c, planning, weekly } = await setup({ routine: routineOf(answers), availability: hours });
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam');
    expect(weekly.tempoOptions).toHaveBeenCalledTimes(1);
    planning.getAvailability.mockReturnValue(of({ ...hours, version: 6, windows: [{ day: 'Tuesday', startMinute: 900, endMinute: 1140 }] }));
    c.loadHours(); await settle(fixture);
    expect(weekly.tempoOptions).toHaveBeenCalledTimes(2);
  });

  describe('edit mode', () => {
    it('asks what to change when there is an active plan', async () => {
      const { el } = await setup({ active: true, routine: routineOf(answers), availability: hours });
      expect(el.querySelector('#edit-title')?.textContent).toBe('Planında neyi değiştirmek istiyorsun?');
      expect(document.activeElement?.id).toBe('edit-title');
      expect(['Saatlerim Okul, yemek, kurs ve çalışma saatlerin', 'Derslerim Derslerinin önceliği ve zorluğu', 'Tempo Konuları ne zaman bitireceğin']
        .map(x => !!button(el, x))).toEqual([true, true, true]);
      expect(button(el, 'Planı baştan kur')).toBeTruthy();
    });

    it('"Saatlerim" opens the questions with the saved answers; Geri returns to the question with the focus on it', async () => {
      const { fixture, el, c } = await setup({ active: true, routine: routineOf(answers), availability: hours });
      c.edit('hours'); await settle(fixture);
      expect(progress(el)).toBe('Adım 1 / 2');
      expect(el.querySelector('[data-testid="routine-question"]')?.getAttribute('data-question')).toBe('shift');
      expect((el.querySelectorAll('[data-testid="routine-question"] input')[0] as HTMLInputElement).checked).toBe(true);
      await click(fixture, 'Geri');
      expect(el.querySelector('[data-testid="edit-menu"]')).toBeTruthy();
      expect(document.activeElement?.id).toBe('edit-title');
    });

    it('"Derslerim" opens the courses, then the summary', async () => {
      const { fixture, el, c } = await setup({ active: true, routine: routineOf(answers), availability: hours });
      c.edit('courses'); await settle(fixture);
      expect(progress(el)).toBe('Adım 1 / 2'); expect(title(el)).toBe('Derslerin');
      await click(fixture, 'Devam');
      expect(title(el)).toBe('Özet'); expect(progress(el)).toBe('Adım 2 / 2');
    });

    it('"Tempo" opens the tempo with the saved choice selected; "Planı baştan kur" runs every step', async () => {
      const { fixture, el, c } = await setup({ active: true, routine: routineOf(answers), availability: hours, prefs: { examFocus: null, tempo: 'Intense' } });
      c.edit('tempo'); await settle(fixture);
      expect(title(el)).toBe('Tempo'); expect(progress(el)).toBe('Adım 1 / 2');
      expect((el.querySelectorAll('[data-testid="tempo-option"] input')[2] as HTMLInputElement).checked).toBe(true);
      await click(fixture, 'Geri');
      await click(fixture, 'Planı baştan kur');
      expect(progress(el)).toBe('Adım 1 / 4');
    });
  });

  // S3: "Saat ekle" can be left with Geri, and leaves no flag behind.
  it('"Saat ekle" opens the questions, Geri returns to the summary, and a save also returns there', async () => {
    const { fixture, el, c, routines } = await setup({ active: true, shortage: true, routine: routineOf(answers), availability: hours });
    c.edit('courses'); await settle(fixture);
    await click(fixture, 'Devam');
    expect(title(el)).toBe('Özet');
    await click(fixture, 'Saat ekle');
    expect(title(el)).toBe('Saatlerin');
    expect(el.querySelector('[data-testid="routine-question"]')?.getAttribute('data-question')).toBe('shift');
    await click(fixture, 'Geri');
    expect(title(el)).toBe('Özet'); expect(document.activeElement?.id).toBe('step-title');
    // Save the answers this time: straight back to the summary.
    await click(fixture, 'Saat ekle');
    for (let i = 0; i < 7; i++) await click(fixture, 'Devam');
    await click(fixture, 'Kaydet ve devam');
    expect(routines.save).toHaveBeenCalledTimes(1);
    expect(title(el)).toBe('Özet');
    // Re-review (2): the hours step left the flow with the detour; back to the menu through the courses, then the whole plan.
    expect(pills(el)).toEqual(['Derslerin (tamamlandı)', 'Özet']);
    await click(fixture, 'Geri'); expect(title(el)).toBe('Derslerin');
    await click(fixture, 'Geri'); expect(el.querySelector('[data-testid="edit-menu"]')).toBeTruthy();
    await click(fixture, 'Planı baştan kur');
    await click(fixture, 'Doğru, devam');
    expect(title(el)).toBe('Derslerin');
  });

  it('"Saat ekle" from the tempo edit leaves no hours step behind (Geri reaches the menu)', async () => {
    const { fixture, el, c } = await setup({ active: true, shortage: true, routine: routineOf(answers), availability: hours });
    c.edit('tempo'); await settle(fixture);
    await click(fixture, 'Devam');
    expect(title(el)).toBe('Özet');
    await click(fixture, 'Saat ekle');
    expect(pills(el)).toEqual(['Saatlerin', 'Tempo', 'Özet']);
    await click(fixture, 'Geri');
    expect(title(el)).toBe('Özet'); expect(pills(el)).toEqual(['Tempo (tamamlandı)', 'Özet']);
    await click(fixture, 'Geri'); expect(title(el)).toBe('Tempo');
    await click(fixture, 'Geri'); expect(el.querySelector('[data-testid="edit-menu"]')).toBeTruthy();
  });

  // Re-review (5): a routine save sends only what the student confirmed; the server keeps saved values for null.
  it('saves the routine with null focus and tempo until the student confirms them', async () => {
    const { fixture, c, routines } = await setup({ yks: true, active: true, routine: routineOf(answers), availability: hours });
    expect(c.examFocus()).toBe('balanced');
    c.edit('hours'); await settle(fixture);
    const hoursStep = fixture.debugElement.query(By.directive(WizardHoursStepComponent)).componentInstance as WizardHoursStepComponent;
    hoursStep.index.set(hoursStep.questions().indexOf('review')); hoursStep.next(); await settle(fixture);
    expect(routines.save).toHaveBeenCalledWith(expect.objectContaining({ answers: expect.objectContaining({ examFocus: null, tempo: null }) }));
  });

  it('saves the routine with the confirmed focus and tempo after the student confirmed them', async () => {
    const { fixture, c, routines } = await setup({ yks: true, active: true, shortage: true, routine: routineOf(answers), availability: hours });
    c.edit('tempo'); await settle(fixture);
    c.setExamFocus('ayt'); await click(fixture, 'Devam');
    c.tempo.set('Intense'); await click(fixture, 'Devam');
    await click(fixture, 'Saat ekle');
    const hoursStep = fixture.debugElement.query(By.directive(WizardHoursStepComponent)).componentInstance as WizardHoursStepComponent;
    hoursStep.index.set(hoursStep.questions().indexOf('review')); hoursStep.next(); await settle(fixture);
    expect(routines.save).toHaveBeenCalledWith(expect.objectContaining({ answers: expect.objectContaining({ examFocus: 'Ayt', tempo: 'Intense' }) }));
  });

  // Re-review (7): a read in progress does not stop the student from leaving.
  it('lets the student leave while the summary is only reading', async () => {
    const { fixture, c, weekly } = await setup({ adim: 'ozet', routine: routineOf(answers), availability: hours });
    weekly.proposal.mockReturnValue(NEVER as never);
    (fixture.debugElement.query(By.directive(WizardSummaryStepComponent)).componentInstance as WizardSummaryStepComponent).propose('Fill');
    expect(await c.canLeavePage()).toBe(true);
  });

  it('opens the Özet step directly for the old preview address (?adim=ozet)', async () => {
    const { el, weekly } = await setup({ adim: 'ozet', active: true, routine: routineOf(answers), availability: hours, prefs: { examFocus: null, tempo: 'Balanced' } });
    expect(title(el)).toBe('Özet');
    expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, undefined, undefined, '2027-05-12');
  });

  // S1: "Yeniden öner" asks the Replace options for the same tempo; declining keeps the Fill proposal with the wizard's own options.
  it('opens the summary with a whole new week for H5\'s "Yeniden öner" (?yeniden=oner), after a confirmation', async () => {
    const { el, weekly, toaster } = await setup({ yeniden: true, cards: true, routine: routineOf(answers), availability: hours, active: true });
    expect(title(el)).toBe('Özet');
    expect(weekly.tempoOptions).toHaveBeenCalledWith(null, undefined, undefined, 'Fill');
    expect(toaster.confirm).toHaveBeenCalled();
    expect(weekly.tempoOptions).toHaveBeenLastCalledWith(null, undefined, undefined, 'Replace');
    expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, undefined, undefined, '2027-06-13');
  });

  it('keeps the week when "Yeniden öner" is declined: a Fill proposal with the wizard\'s own options', async () => {
    TestBed.overrideProvider(ToasterService, { useValue: { confirm: vi.fn().mockResolvedValue(false), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } });
    const { weekly } = await setup({ yeniden: true, cards: true, routine: routineOf(answers), availability: hours });
    expect(weekly.tempoOptions).toHaveBeenCalledTimes(1);
    expect(weekly.proposal).toHaveBeenCalledWith('Fill', null, undefined, undefined, '2027-06-13');
  });

  // S1: once the summary saved cards into an empty draft, the tempo options are read again in Fill mode.
  it('reads the tempo options in Fill mode after the summary saved the week (Geri from Özet)', async () => {
    const { fixture, el, c, weekly } = await setup({ routine: routineOf(answers), availability: hours });
    await click(fixture, 'Doğru, devam'); await click(fixture, 'Devam'); await click(fixture, 'Devam');
    expect(title(el)).toBe('Özet');
    expect(weekly.tempoOptions).toHaveBeenLastCalledWith(null, undefined, undefined, 'Replace');
    c.draftSaved(true);
    await click(fixture, 'Geri');
    expect(title(el)).toBe('Tempo');
    expect(weekly.tempoOptions).toHaveBeenLastCalledWith(null, undefined, undefined, 'Fill');
  });

  // S15: a pending draft is offered before the flow.
  it('offers a pending draft first, then lets the student start a new plan', async () => {
    const { fixture, el } = await setup({ draft: true, routine: routineOf(answers), availability: hours });
    const panel = el.querySelector('[data-testid="pending-draft"]')!;
    expect(words(panel)).toContain('“Bekleyen planım” henüz yayımlanmadı.');
    expect(panel.querySelector('a')?.getAttribute('href')).toBe('/coaching-portal/plan?plan=d');
    expect(panel.querySelector('a')?.textContent?.trim()).toBe('Bekleyen taslağını aç / yayımla');
    expect(document.activeElement?.id).toBe('draft-title');
    await click(fixture, 'Yine de yeni plan hazırla');
    expect(title(el)).toBe('Saatlerin');
  });

  it('asks before leaving with chosen topics that were not used', async () => {
    const { c, toaster } = await setup({ routine: routineOf(answers), availability: hours });
    expect(await c.canLeavePage()).toBe(true);
    c.summaryChoices.focusTopics.set([{ topicId: 't', topicName: 'Paragraf', lessonName: 'Türkçe' }]);
    toaster.confirm.mockResolvedValueOnce(false);
    expect(await c.canLeavePage()).toBe(false);
  });

  describe('teacher view', () => {
    it('shows the routine read only, third person, and never saves it', async () => {
      const { fixture, el, routines, weekly, learning, planning, prefs } = await setup({ staff: 'student-1', routine: routineOf(answers), availability: hours });
      expect(el.querySelector('h1')?.textContent).toBe('Öğrenci için plan hazırla');
      expect(routines.get).toHaveBeenCalledWith('student-1'); expect(learning.getForStudent).toHaveBeenCalledWith('student-1');
      expect(planning.list).not.toHaveBeenCalled();
      expect(title(el)).toBe('Öğrencinin saatleri');
      expect(button(el, 'Değiştir')).toBeUndefined();
      await click(fixture, 'Devam'); expect(title(el)).toBe('Öğrencinin dersleri');
      await click(fixture, 'Devam'); expect(el.textContent).toContain('Öğrenci konuları ne zaman bitirsin?');
      await click(fixture, 'Devam');
      expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, 'student-1', undefined, '2027-06-13');
      expect(routines.save).not.toHaveBeenCalled(); expect(prefs.save).not.toHaveBeenCalled();
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
