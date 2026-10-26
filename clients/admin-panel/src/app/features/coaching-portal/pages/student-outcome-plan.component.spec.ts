import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { StudentAutomaticPlanComponent } from './student-automatic-plan.component';

describe('Student outcome based plan', () => {
  async function setup() {
    const outcome = { outcomeId: 'o1', code: 'M.8.1.1.1', name: 'Çarpanları bulur', estimateId: 'e2', estimateNumber: 2, learningMinutes: 20, practiceMinutes: 30, checkMinutes: 10, totalMinutes: 60, editionCode: 'meb-2018', officialLessonPeriods: 10 };
    const service = { getAvailability: vi.fn(() => of({ version: 0, timeZoneId: 'Europe/Istanbul', windows: [] })),
      getTopicOutcomes: vi.fn(() => of([outcome])), previewAutomatic: vi.fn((_: any) => of({})), saveAutomaticDraft: vi.fn(), searchTopics: vi.fn() };
    TestBed.configureTestingModule({ imports: [StudentAutomaticPlanComponent], providers: [provideRouter([]), { provide: CoachingStudyPlanningService, useValue: service }] });
    const f = TestBed.createComponent(StudentAutomaticPlanComponent); f.detectChanges(); await f.whenStable(); const c = f.componentInstance;
    c.results.set({ items: [{ id: 't1', name: 'Çarpanlar', lessonName: 'Matematik', unitName: 'LGS', examCode: 'LGS', gradeNumber: null, estimatedMinutes: null }], totalCount: 1, pageNumber: 1, pageSize: 20 }); c.addTopic('t1');
    return { f, c, service };
  }
  it('loads reviewed outcomes explicitly, shows components and sends versioned choices', async () => {
    const { f, c, service } = await setup(); c.loadOutcomes('t1'); f.detectChanges(); await f.whenStable();
    expect(f.nativeElement.textContent).toContain('Çarpanları bulur');
    c.startDate = '2026-10-19'; c.generate();
    expect(service.previewAutomatic.mock.calls[0][0].topics).toEqual([{ topicId: 't1', requiredMinutes: null, outcomes: [{ outcomeId: 'o1', estimateId: 'e2', requiredMinutes: 60 }] }]);
  });
  it('does not turn an outcome load failure into a fabricated estimate', async () => {
    const { c, service } = await setup(); service.getTopicOutcomes.mockReturnValueOnce(throwError(() => ({ status: 503 })));
    c.loadOutcomes('t1'); c.startDate = '2026-10-19'; c.generate();
    expect(c.error()).toBeTruthy(); expect(service.previewAutomatic).not.toHaveBeenCalled(); expect(c.selected[0].minutes).toBeNull();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
