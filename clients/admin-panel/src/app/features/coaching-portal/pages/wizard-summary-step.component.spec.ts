import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { CoachingLearningSetupService, LearningCatalog, LearningContext } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { CoachingWeeklyDraftService, WeeklyProposal, WeeklyProposalMode } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingWeeklyOutcomePlanService, WeeklyOutcomePreview, WeeklyOutcomeRequest } from '../../../core/services/coaching-weekly-outcome-plan.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { SummaryChoices, WizardSummaryStepComponent } from './wizard-summary-step.component';
import { WeeklySlot } from './weekly-calendar';

// Paket H4, step 5 "Özet": the proposed week, the yearly preview with its fixes, "Şu konuları öne al", and publishing.
describe('WizardSummaryStepComponent', () => {
  const saved: WeeklySlot = { id: 'old', lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', day: 'Monday', startMinute: 1080, durationMinutes: 60 };
  const tuesday: WeeklySlot = { id: 'p1', lessonId: 'tur', lessonName: 'Türkçe', examCode: 'LGS', day: 'Tuesday', startMinute: 1080, durationMinutes: 60 };
  const proposalOf = (mode: WeeklyProposalMode, slots: WeeklySlot[], over: Partial<WeeklyProposal> = {}): WeeklyProposal => ({ mode, examFocus: null, slots,
    lessons: [{ lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', existingMinutes: 60, proposedMinutes: 0, neededMinutes: 60, noTime: false },
      { lessonId: 'tur', lessonName: 'Türkçe', examCode: 'LGS', existingMinutes: 0, proposedMinutes: slots.length * 60, neededMinutes: 60, noTime: false }],
    freeMinutes: 120, neededMinutes: 120, shortageMinutes: 0, coveredMinutes: 120, maxDayMinutes: 480, weeksUntilEnd: 20, endDate: '2027-05-12', middleSchool: true,
    draftVersion: 3, finishBy: '2027-05-12', ...over });
  const context: LearningContext = { id: 'c', version: 5, academicYearStart: 2026, purpose: 'Lgs', targetExamYear: 2027, primaryScoreType: null, foreignLanguage: null,
    curriculumVersionId: 'cur', startingStatusReviewed: true, topicStates: [], coursePreferences: [{ lessonId: 'math', priority: 3, selfReportedDifficulty: null },
      { lessonId: 'tur', priority: 3, selfReportedDifficulty: 4 }] };
  const previewOf = (request: WeeklyOutcomeRequest, over: Partial<WeeklyOutcomePreview> = {}): WeeklyOutcomePreview => ({ request, timeZoneId: 'Europe/Istanbul',
    fingerprint: 'f'.repeat(64), canCreate: true, protectedTaskCount: 0, issues: [],
    leftOutTopics: [{ lessonId: 'tur', lessonName: 'Türkçe', topicId: 'paragraf', topicName: 'Paragraf', minutes: 90 }],
    schedule: { freeMinutes: 0, recommendedEndDate: '2027-06-01', tasks: [
      { date: '2026-11-16', startMinute: 1080, lessonId: 'math', lessonName: 'Matematik', minutes: 30, reusedSlot: false, kind: 'Spaced',
        outcome: { topicId: 't', topicName: 'Çarpanlar', outcomeId: 'o', code: 'M', name: 'n', estimateId: 'e', estimateNumber: 1, minutes: 30 } }],
      lessons: [{ lessonId: 'math', lessonName: 'Matematik', outcomeCount: 3, weeklyMinutes: 60, requiredMinutes: 600, scheduledMinutes: 600, remainingMinutes: 0, finishDate: '2027-04-20', extraWeeklyMinutes: 0 },
        { lessonId: 'tur', lessonName: 'Türkçe', outcomeCount: 3, weeklyMinutes: 60, requiredMinutes: 900, scheduledMinutes: 810, remainingMinutes: 90, finishDate: null, extraWeeklyMinutes: 30 }] }, ...over });

  async function setup(opts: { slots?: WeeklySlot[]; staff?: boolean; finishBy?: string | null; windows?: { day: string; startMinute: number; endMinute: number }[] | null; tempoNote?: string; replaceOnOpen?: boolean; tempo?: 'Relaxed' | 'Balanced' | 'Intense'; choices?: SummaryChoices } = {}) {
    const slots = opts.slots ?? [saved];
    const weekly = { get: vi.fn(() => of({ draft: slots.length ? { version: 3, timeZoneId: 'Europe/Istanbul', slots } : null, lessons: [], maxDayMinutes: 480 })),
      save: vi.fn((_v: number | null, _tz: string, next: WeeklySlot[]) => of({ version: 4, timeZoneId: 'Europe/Istanbul', slots: next })),
      proposal: vi.fn((mode: WeeklyProposalMode, ..._rest: unknown[]) => of(proposalOf(mode, [tuesday], { draftVersion: slots.length ? 3 : null }))),
      // Replace-mode options (the wizard's own options are for the draft's mode).
      tempoOptions: vi.fn((..._a: unknown[]) => of({ planEnd: '2027-06-20', options: [{ tempo: 'Relaxed', finishBy: '2027-06-20', neededWeeklyMinutes: 60, capacityWeeklyMinutes: 120, fits: true },
        { tempo: 'Balanced', finishBy: '2027-05-15', neededWeeklyMinutes: 90, capacityWeeklyMinutes: 120, fits: true }] })) };
    const engine = { options: vi.fn(() => of({ startDate: '2026-11-09', endDate: '2027-06-13', weeklyDraftVersion: 4, timeZoneId: 'Europe/Istanbul', exams: [] })),
      preview: vi.fn((request: WeeklyOutcomeRequest, _s?: string) => of(previewOf(request))), create: vi.fn((..._a: unknown[]) => of({ id: 'new-plan', version: 1 })) };
    const planning = { publish: vi.fn((..._a: unknown[]) => of({ id: 'new-plan', version: 2, title: 'x', status: 'Active', tasks: [] })) };
    const learning = { save: vi.fn((_r: unknown) => of({ context: { ...context, version: 6 }, nextStep: 'Preview', isReady: true, missingFields: [], warnings: [] })),
      catalog: vi.fn((_l: string | null, page = 1) => of({ curriculum: null, lessons: [], missingDurationCount: 0, warnings: [], topics: { totalCount: 3, pageNumber: page, pageSize: 2,
        items: page === 1 ? [{ id: 'paragraf', name: 'Paragraf', lessonName: 'Türkçe', unitName: 'Okuma', gradeNumber: 8, examCode: 'LGS', estimatedMinutes: 90 },
          { id: 'fiil', name: 'Fiilimsiler', lessonName: 'Türkçe', unitName: 'Dil bilgisi', gradeNumber: 8, examCode: 'LGS', estimatedMinutes: 60 }]
          : [{ id: 'cumle', name: 'Cümlenin ögeleri', lessonName: 'Türkçe', unitName: 'Dil bilgisi', gradeNumber: 8, examCode: 'LGS', estimatedMinutes: 60 }] } })),
      catalogForStudent: vi.fn((..._a: unknown[]) => of({ curriculum: null, lessons: [], missingDurationCount: 0, warnings: [], topics: { totalCount: 0, pageNumber: 1, pageSize: 50, items: [] } })) };
    TestBed.configureTestingModule({ imports: [WizardSummaryStepComponent], providers: [provideRouter([]),
      { provide: CoachingWeeklyDraftService, useValue: weekly }, { provide: CoachingWeeklyOutcomePlanService, useValue: engine },
      { provide: CoachingStudyPlanningService, useValue: planning }, { provide: CoachingLearningSetupService, useValue: learning }] });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(WizardSummaryStepComponent), c = fixture.componentInstance;
    c.staffStudentId = opts.staff ? 'student-1' : null; c.finishBy = opts.finishBy === undefined ? '2027-05-12' : opts.finishBy; c.context = context;
    c.windows = opts.windows ?? null; c.tempoNote = opts.tempoNote ?? ''; c.replaceOnOpen = !!opts.replaceOnOpen;
    c.finishByMode = slots.length ? 'Fill' : 'Replace'; c.tempo = opts.tempo ?? 'Balanced'; if (opts.choices) c.choices = opts.choices;
    const events = { addHours: vi.fn(), planCreated: vi.fn(), contextSaved: vi.fn(), draftSaved: vi.fn() };
    c.addHours.subscribe(events.addHours); c.planCreated.subscribe(events.planCreated); c.contextSaved.subscribe(events.contextSaved); c.draftSaved.subscribe(events.draftSaved);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, c, el: fixture.nativeElement as HTMLElement, weekly, engine, planning, learning, navigate, events,
      toaster: TestBed.inject(ToasterService) as unknown as { confirm: ReturnType<typeof vi.fn>; success: ReturnType<typeof vi.fn> } };
  }
  const button = (el: HTMLElement, text: string) => [...el.querySelectorAll('button')].find(x => x.textContent?.trim() === text) as HTMLButtonElement | undefined;
  const text = (el: Element | null) => {
    if (!el) return undefined;
    const parts: string[] = [], walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    while (walker.nextNode()) parts.push(walker.currentNode.textContent ?? '');
    return parts.join(' ').replace(/\s+/g, ' ').trim();
  };
  async function keep(fixture: { detectChanges(): void; whenStable(): Promise<unknown>; nativeElement: HTMLElement }) {
    button(fixture.nativeElement, 'Bu haftayla devam et')!.click(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
  }

  it('proposes in Fill mode with the tempo\'s finish date when the week has cards, and shows the week day by day', async () => {
    const { el, weekly } = await setup();
    expect(weekly.proposal).toHaveBeenCalledWith('Fill', null, undefined, undefined, '2027-05-12');
    expect([...el.querySelectorAll('[data-testid="proposal-day"]')].map(text)).toEqual(['Pazartesi 18:00–19:00 · Matematik', 'Salı 18:00–19:00 · Türkçe Yeni']);
    expect([...el.querySelectorAll('[data-testid="proposal-lessons"] li')].map(text)).toEqual(['Matematik · haftada 1 saat', 'Türkçe · haftada 1 saat']);
  });

  it('proposes in Replace mode when the weekly draft is empty, without "Haftanı baştan öner"', async () => {
    const { el, weekly } = await setup({ slots: [] });
    expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, undefined, undefined, '2027-05-12');
    expect(button(el, 'Haftanı baştan öner')).toBeUndefined();
  });

  it('opens with "Haftanı baştan öner" when asked (?yeniden=oner), and keeps the week when the student says no', async () => {
    const { weekly } = await setup({ replaceOnOpen: true });
    // S1: a Replace on a week with cards asks the Replace options and keeps the tempo (Dengeli).
    expect(weekly.tempoOptions).toHaveBeenCalledWith(null, undefined, undefined, 'Replace');
    expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, undefined, undefined, '2027-05-15');
    TestBed.resetTestingModule();
    TestBed.overrideProvider(ToasterService, { useValue: { confirm: vi.fn().mockResolvedValue(false), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } });
    const no = await setup({ replaceOnOpen: true });
    expect(no.weekly.proposal).toHaveBeenCalledWith('Fill', null, undefined, undefined, '2027-05-12');
  });

  it('"Haftanı baştan öner" proposes the whole week again only after a confirmation', async () => {
    const { el, weekly, toaster, fixture } = await setup();
    toaster.confirm.mockResolvedValueOnce(false);
    button(el, 'Haftanı baştan öner')!.click(); await fixture.whenStable();
    expect(weekly.proposal).toHaveBeenCalledTimes(1);
    button(el, 'Haftanı baştan öner')!.click(); await fixture.whenStable(); fixture.detectChanges();
    expect(weekly.proposal).toHaveBeenLastCalledWith('Replace', null, undefined, undefined, '2027-05-15');
    expect(text(el)).toContain('Bu düzen, mevcut haftalık düzeninin yerine geçer.');
  });

  it('saves the kept week with its version, then previews the year to the tempo\'s finish date and shows each course', async () => {
    const { el, fixture, weekly, engine, events } = await setup();
    await keep(fixture);
    expect(weekly.save).toHaveBeenCalledWith(3, 'Europe/Istanbul', [saved, tuesday], undefined);
    expect(events.draftSaved).toHaveBeenCalledWith(true);
    expect(engine.preview).toHaveBeenCalledWith({ startDate: '2026-11-09', endDate: '2027-05-12', expectedWeeklyDraftVersion: 4, reuseFreedSlots: true, engineVersion: 2,
      prioritizeWhenShort: false }, undefined);
    const rows = [...el.querySelectorAll('[data-testid="course-finish"] li')];
    expect(text(rows[0])).toBe('Matematik Bitiş: 20 Nisan Haftada 1 saat 1 aralıklı tekrar');
    expect(text(rows[1])).toContain('Türkçe Bitiş: yetişmiyor');
    expect(text(rows[1]?.querySelector('[data-testid="left-out"]') ?? null)).toBe('Dışarıda kalan konular: 1 (Paragraf)');
    expect(rows[0].querySelector('[data-testid="late-fixes"]')).toBeNull();
    expect(text(rows[1].querySelector('[data-testid="late-fixes"]'))).toBe('Saat ekle Bu dersi öne al Önemli konulara odaklan');
  });

  it('goes straight to the yearly preview when the week already has enough time', async () => {
    const setupOnce = await setup();
    setupOnce.weekly.proposal.mockReturnValueOnce(of(proposalOf('Fill', [])));
    setupOnce.c.propose('Fill');
    expect(setupOnce.engine.preview).toHaveBeenCalled();
  });

  it('stops a proposal over the day limit and asks for a new one when the draft changed elsewhere', async () => {
    const full = Array.from({ length: 8 }, (_, i) => ({ ...saved, id: 's' + i, day: 'Tuesday' as const, startMinute: 480 + i * 60 }));
    const { el, fixture, weekly } = await setup({ slots: full });
    await keep(fixture);
    expect(weekly.save).not.toHaveBeenCalled();
    expect(el.querySelector('.notice.error')).toBeTruthy();
    TestBed.resetTestingModule();
    const again = await setup();
    again.weekly.proposal.mockReturnValueOnce(of(proposalOf('Fill', [tuesday], { draftVersion: 1 })));
    again.c.propose('Fill'); again.fixture.detectChanges();
    await keep(again.fixture);
    expect(again.weekly.save).not.toHaveBeenCalled();
    expect(text(again.el)).toContain('Haftalık düzenin başka bir yerde değişti');
  });

  it('"Saat ekle" goes back to the hours; "Önemli konulara odaklan" previews with the important topics first', async () => {
    const { el, fixture, engine, events } = await setup();
    await keep(fixture);
    button(el, 'Saat ekle')!.click(); expect(events.addHours).toHaveBeenCalled();
    button(el, 'Önemli konulara odaklan')!.click(); await fixture.whenStable(); fixture.detectChanges();
    expect(engine.preview).toHaveBeenLastCalledWith(expect.objectContaining({ prioritizeWhenShort: true }), undefined);
    expect(button(el, 'Önemli konulara odaklan')).toBeUndefined();
  });

  it('"Bu dersi öne al" saves the course as Yüksek with the version, then proposes the week again', async () => {
    const { el, fixture, learning, weekly, events } = await setup();
    await keep(fixture);
    button(el, 'Bu dersi öne al')!.click(); await fixture.whenStable(); fixture.detectChanges();
    expect(learning.save).toHaveBeenCalledWith(expect.objectContaining({ expectedVersion: 5, courses: [{ lessonId: 'math', priority: 3, selfReportedDifficulty: null },
      { lessonId: 'tur', priority: 1, selfReportedDifficulty: 4 }] }));
    expect(events.contextSaved).toHaveBeenCalled();
    expect(weekly.proposal).toHaveBeenCalledTimes(2);
    expect(weekly.proposal).toHaveBeenLastCalledWith('Fill', null, undefined, undefined, '2027-05-12');
  });

  it('"Şu konuları öne al": picks topics of a weekly lesson, sends them with the preview and lists them with remove buttons', async () => {
    const { el, fixture, engine, learning, c } = await setup();
    await keep(fixture);
    button(el, 'Konu seç')!.click(); fixture.detectChanges();
    expect(learning.catalog).toHaveBeenCalledWith('math', 1);
    c.chooseLesson('tur'); fixture.detectChanges();
    // S10: every page of the lesson is read before the search filters it.
    expect(learning.catalog).toHaveBeenCalledWith('tur', 1); expect(learning.catalog).toHaveBeenLastCalledWith('tur', 2);
    expect(text(el.querySelector('#focus-count'))).toBe('3 konu');
    c.choices.pickerSearch.set('ögeler'); fixture.detectChanges();
    expect([...el.querySelectorAll('.topic-list li')].map(text)).toEqual(['Cümlenin ögeleri Dil bilgisi Öne al']);
    c.choices.pickerSearch.set('para'); fixture.detectChanges();
    expect([...el.querySelectorAll('.topic-list li')].map(text)).toEqual(['Paragraf Okuma Öne al']);
    (el.querySelector('[aria-label="Paragraf konusunu öne al"]') as HTMLButtonElement).click(); fixture.detectChanges();
    c.choices.pickerSearch.set(''); fixture.detectChanges();
    (el.querySelector('[aria-label="Cümlenin ögeleri konusunu öne al"]') as HTMLButtonElement).click(); fixture.detectChanges();
    expect([...el.querySelectorAll('.chosen-topics li')].map(text)).toEqual(['Türkçe · Paragraf Kaldır', 'Türkçe · Cümlenin ögeleri Kaldır']);
    // The plan cannot be published from a preview made without the chosen topics.
    expect((el.querySelector('[data-testid="publish-plan"]') as HTMLButtonElement).disabled).toBe(true);
    (el.querySelector('[aria-label="Cümlenin ögeleri konusunu kaldır"]') as HTMLButtonElement).click(); fixture.detectChanges();
    (el.querySelector('[data-testid="focus-preview"]') as HTMLButtonElement).click(); await fixture.whenStable(); fixture.detectChanges();
    expect(engine.preview).toHaveBeenLastCalledWith(expect.objectContaining({ focusTopicIds: ['paragraf'] }), undefined);
    expect((el.querySelector('[data-testid="publish-plan"]') as HTMLButtonElement).disabled).toBe(false);
  });

  it('chooses at most 50 topics', async () => {
    const { c } = await setup();
    for (let i = 0; i < 55; i++) c.addTopic({ id: 't' + i, name: 'Konu ' + i, lessonName: 'Türkçe', unitName: '', gradeNumber: null, examCode: null, estimatedMinutes: null });
    expect(c.choices.focusTopics()).toHaveLength(50);
  });

  it('"Planımı yayımla" asks first, creates the plan from the checked preview, publishes it and opens Planım', async () => {
    const { el, fixture, engine, planning, toaster, navigate } = await setup();
    await keep(fixture);
    toaster.confirm.mockResolvedValueOnce(false);
    button(el, 'Planımı yayımla')!.click(); await fixture.whenStable();
    expect(toaster.confirm).toHaveBeenCalledWith('Yayımlarsan bugünden sonraki çalışmaların bu plana göre değişir. Tamamladığın çalışmalar korunur.', { confirmText: 'Yayımla', cancelText: 'Vazgeç' });
    expect(engine.create).not.toHaveBeenCalled();
    button(el, 'Planımı yayımla')!.click(); await fixture.whenStable();
    const request = engine.preview.mock.calls.at(-1)![0];
    expect(engine.create).toHaveBeenCalledWith({ title: 'Haftalık derslerimden çalışma planım', preview: request, fingerprint: 'f'.repeat(64) }, undefined);
    expect(planning.publish).toHaveBeenCalledWith('new-plan', 1);
    expect(toaster.success).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/coaching-portal/plan'], { queryParams: { plan: 'new-plan' } });
  });

  // The published plan used the chosen topics, so leaving for Planım does not ask about them.
  it('clears the chosen topics once the plan is published', async () => {
    const choices = new SummaryChoices();
    const { el, fixture, c } = await setup({ choices });
    await keep(fixture);
    c.addTopic({ id: 'paragraf', name: 'Paragraf', lessonName: 'Türkçe', unitName: '', gradeNumber: null, examCode: null, estimatedMinutes: null });
    c.runPreview(); await fixture.whenStable(); fixture.detectChanges();
    button(el, 'Planımı yayımla')!.click(); await fixture.whenStable();
    expect(choices.focusTopics()).toEqual([]);
  });

  it('offers Planım when a draft already exists', async () => {
    const { el, fixture, engine, planning } = await setup();
    await keep(fixture);
    engine.create.mockReturnValueOnce(throwError(() => ({ status: 409, error: { code: 'StudyPlanning.DraftExists', message: 'Önce mevcut plan taslağınızı yayımlayın veya arşivleyin.' } })));
    button(el, 'Planımı yayımla')!.click(); await fixture.whenStable(); fixture.detectChanges();
    const issue = el.querySelector('[data-testid="publish-issue"]')!;
    expect(text(issue)).toContain('Bekleyen bir plan taslağın var.');
    expect(issue.querySelector('a')?.getAttribute('href')).toBe('/coaching-portal/plan');
    expect(planning.publish).not.toHaveBeenCalled();
  });

  it('asks to recompute when the plan\'s sources changed, and blocks publishing until it is', async () => {
    const { el, fixture, engine } = await setup();
    await keep(fixture);
    engine.create.mockReturnValueOnce(throwError(() => ({ status: 409, error: { message: 'Haftalık taslak, kazanımlar, süreler veya mevcut çalışmalar değişti. Yeniden önizleyin.' } })));
    button(el, 'Planımı yayımla')!.click(); await fixture.whenStable(); fixture.detectChanges();
    expect(button(el, 'Yeniden hesapla')).toBeTruthy();
    expect((el.querySelector('[data-testid="publish-plan"]') as HTMLButtonElement).disabled).toBe(true);
  });

  // Review of PR 135 (S2): a created plan whose publish failed is published again, never created twice.
  it('names the saved draft when publishing fails and "Yeniden yayımla" only publishes it', async () => {
    const { el, fixture, engine, planning, navigate } = await setup();
    await keep(fixture);
    planning.publish.mockReturnValueOnce(throwError(() => ({ status: 400, error: { message: 'Aktif plan değişti.' } })));
    button(el, 'Planımı yayımla')!.click(); await fixture.whenStable(); fixture.detectChanges();
    const issue = el.querySelector('[data-testid="publish-issue"]')!;
    expect(text(issue)).toContain('Planın taslak olarak kaydedildi ama yayımlanamadı. Aktif plan değişti.');
    expect(issue.querySelector('a')?.getAttribute('href')).toBe('/coaching-portal/plan?plan=new-plan');
    button(el, 'Yeniden yayımla')!.click(); await fixture.whenStable(); fixture.detectChanges();
    expect(engine.create).toHaveBeenCalledTimes(1);
    expect(planning.publish).toHaveBeenCalledTimes(2); expect(planning.publish).toHaveBeenLastCalledWith('new-plan', 1);
    expect(navigate).toHaveBeenCalledWith(['/coaching-portal/plan'], { queryParams: { plan: 'new-plan' } });
  });

  // Contract item 7: only the code counts; another 409 with a similar sentence is a changed source.
  it('recognises a pending draft by the server\'s DraftExists code only', async () => {
    const { el, fixture, engine } = await setup();
    await keep(fixture);
    engine.create.mockReturnValueOnce(throwError(() => ({ status: 409, error: { message: 'Önce mevcut plan taslağınızı yayımlayın veya arşivleyin.' } })));
    button(el, 'Planımı yayımla')!.click(); await fixture.whenStable(); fixture.detectChanges();
    expect(text(el.querySelector('[data-testid="publish-issue"]'))).not.toContain('Bekleyen bir plan taslağın var.');
    expect(button(el, 'Yeniden hesapla')).toBeTruthy();
  });

  // Review of PR 135 (S4): the choices belong to the wizard, so a new summary keeps them; "Önemli konulara odaklan" can be undone.
  it('keeps the chosen topics and the important-topics choice across a new summary, and undoes the latter', async () => {
    const choices = new SummaryChoices();
    const first = await setup({ choices });
    await keep(first.fixture);
    first.c.addTopic({ id: 'paragraf', name: 'Paragraf', lessonName: 'Türkçe', unitName: '', gradeNumber: null, examCode: null, estimatedMinutes: null });
    button(first.el, 'Önemli konulara odaklan')!.click(); await first.fixture.whenStable();
    TestBed.resetTestingModule(); TestBed.overrideProvider(ToasterService, { useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } });
    const again = await setup({ choices });
    await keep(again.fixture);
    expect(again.engine.preview).toHaveBeenLastCalledWith(expect.objectContaining({ prioritizeWhenShort: true, focusTopicIds: ['paragraf'] }), undefined);
    expect(text(again.el.querySelector('.chosen-topics'))).toContain('Paragraf');
    button(again.el, 'Bütün konuları planla')!.click(); await again.fixture.whenStable(); again.fixture.detectChanges();
    expect(again.engine.preview).toHaveBeenLastCalledWith(expect.objectContaining({ prioritizeWhenShort: false }), undefined);
    expect(button(again.el, 'Önemli konulara odaklan')).toBeTruthy();
  });

  // S10: an answer for a lesson no longer chosen is dropped.
  it('ignores a late topic list of a lesson the student left', async () => {
    const { fixture, el, c, learning } = await setup();
    await keep(fixture);
    const slow = new Subject<LearningCatalog>();
    learning.catalog.mockReturnValueOnce(slow as never);
    c.choices.pickerOpen.set(true); c.chooseLesson('math'); c.chooseLesson('tur'); fixture.detectChanges();
    slow.next({ curriculum: null, lessons: [], missingDurationCount: 0, warnings: [], topics: { totalCount: 1, pageNumber: 1, pageSize: 50,
      items: [{ id: 'carpan', name: 'Çarpanlar', lessonName: 'Matematik', unitName: '', gradeNumber: 8, examCode: 'LGS', estimatedMinutes: 60 }] } });
    fixture.detectChanges();
    expect(c.pickerTopics().map(x => x.id)).toEqual(['paragraf', 'fiil', 'cumle']);
    expect(text(el.querySelector('.topic-list'))).not.toContain('Çarpanlar');
  });

  // S1: a saved or reloaded draft tells the wizard whether the next proposal is Fill or Replace.
  it('tells the wizard when the weekly draft was saved, removed from or reloaded', async () => {
    const { fixture, c, weekly, events } = await setup({ slots: [] });
    weekly.proposal.mockReturnValueOnce(of(proposalOf('Fill', [tuesday], { draftVersion: 9 })));
    c.propose('Replace'); fixture.detectChanges();
    await keep(fixture);
    expect(events.draftSaved).toHaveBeenCalledWith(false);
  });

  it('names the lessons no longer chosen and removes them from the week after a confirmation', async () => {
    const { el, fixture, engine, weekly } = await setup();
    engine.preview.mockReturnValueOnce(throwError(() => ({ status: 409, error: { code: 'StudyPlanning.LessonsNotChosen', message: 'Fen Bilimleri artık seçili değil.',
      lessons: [{ lessonId: 'math', lessonName: 'Matematik' }] } })));
    await keep(fixture);
    expect(text(el.querySelector('[data-testid="preview-error"]'))).toContain('Fen Bilimleri artık seçili değil.');
    button(el, 'Bu dersleri haftalık düzenimden kaldır')!.click(); await fixture.whenStable(); fixture.detectChanges();
    expect(weekly.save).toHaveBeenLastCalledWith(4, 'Europe/Istanbul', [tuesday], undefined);
    expect(engine.preview).toHaveBeenCalledTimes(2);
  });

  it('a teacher proposes with the given windows, sees no student actions and creates the draft without publishing', async () => {
    const windows = [{ day: 'Tuesday', startMinute: 1080, endMinute: 1200 }];
    const { el, fixture, weekly, engine, planning, events, toaster } = await setup({ staff: true, windows });
    expect(weekly.proposal).toHaveBeenCalledWith('Fill', null, 'student-1', windows, '2027-05-12');
    expect(text(el.querySelector('#wizard-proposal-title'))).toBe('Öğrencinin haftası');
    await keep(fixture);
    expect(engine.preview).toHaveBeenCalledWith(expect.anything(), 'student-1');
    expect(button(el, 'Saat ekle')).toBeUndefined(); expect(button(el, 'Bu dersi öne al')).toBeUndefined();
    expect(el.querySelectorAll('a[href*="/coaching-portal"]').length).toBe(0);
    button(el, 'Planı oluştur')!.click(); await fixture.whenStable();
    expect(toaster.confirm).not.toHaveBeenCalled();
    expect(engine.create).toHaveBeenCalledWith(expect.objectContaining({ fingerprint: 'f'.repeat(64) }), 'student-1');
    expect(planning.publish).not.toHaveBeenCalled();
    expect(events.planCreated).toHaveBeenCalledWith('new-plan');
  });

  it('ends on the server\'s default date without a tempo and says when only one tempo fits', async () => {
    const { fixture, engine, el } = await setup({ finishBy: null, tempoNote: 'Sınavına kadar tek bir tempo uygun: Rahat, bitiş 13 Haziran.' });
    expect(text(el.querySelector('[data-testid="tempo-note"]'))).toBe('Sınavına kadar tek bir tempo uygun: Rahat, bitiş 13 Haziran.');
    await keep(fixture);
    expect(engine.preview).toHaveBeenCalledWith(expect.objectContaining({ endDate: '2027-06-13' }), undefined);
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, { useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } }); });
