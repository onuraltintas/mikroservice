import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { PlanExtendComponent } from './plan-extend.component';
import { CoachingStudyPlanningService, WeeklyPlanSource } from '../../../core/services/coaching-study-planning.service';
import { CoachingWeeklyOutcomePlanService, weekSpanEnd } from '../../../core/services/coaching-weekly-outcome-plan.service';

// User decision 15.11.2026: a plan made for a week is continued with one click; the user chooses what happens to untouched past work.
describe('PlanExtendComponent', () => {
  const source = (over: Partial<WeeklyPlanSource> = {}): WeeklyPlanSource => ({ draftVersion: 4, learningContextVersion: 1, startDate: '2026-11-09', endDate: '2026-11-15',
    reuseFreedSlots: true, fingerprint: 'f', activeRevisionId: null, activeRevisionVersion: null, partial: true, ...over });
  function mount(over: { source?: WeeklyPlanSource | null; active?: boolean; readOnly?: boolean; staff?: string; preview?: object; create?: unknown } = {}) {
    const engine = {
      options: vi.fn(() => of({ startDate: '2026-11-13', endDate: '2027-06-13', weeklyDraftVersion: 4, timeZoneId: 'Europe/Istanbul', exams: [] })),
      preview: vi.fn((request: object) => of({ request, fingerprint: 'p'.repeat(64), canCreate: true, ...over.preview })),
      create: vi.fn(() => over.create ?? of({ id: 'next-plan', version: 1 }))
    };
    const planning = { publish: vi.fn(() => of({})) };
    TestBed.configureTestingModule({ imports: [PlanExtendComponent], providers: [
      { provide: CoachingWeeklyOutcomePlanService, useValue: engine }, { provide: CoachingStudyPlanningService, useValue: planning }] });
    const fixture = TestBed.createComponent(PlanExtendComponent);
    fixture.componentRef.setInput('source', 'source' in over ? over.source : source());
    fixture.componentRef.setInput('active', over.active ?? true); fixture.componentRef.setInput('readOnly', over.readOnly ?? false);
    fixture.componentRef.setInput('staffStudentId', over.staff ?? null);
    const extended = vi.fn(); fixture.componentInstance.extended.subscribe(extended);
    fixture.detectChanges();
    return { fixture, engine, planning, extended, el: fixture.nativeElement as HTMLElement };
  }
  const click = (el: HTMLElement, id: string) => (el.querySelector(`[data-testid="${id}"]`) as HTMLButtonElement).click();

  it('shows only on an active plan made for part of the way, to someone who may change it', () => {
    expect(mount().el.querySelector('[data-testid="plan-extend"]')).toBeTruthy();
    for (const over of [{ source: source({ partial: false }) }, { source: null }, { active: false }, { readOnly: true }]) {
      TestBed.resetTestingModule();
      expect(mount(over).el.querySelector('[data-testid="plan-extend"]')).toBeNull();
    }
  });

  it('plans again from today to a week after the plan\'s end, publishes it and reports the new plan', () => {
    const { fixture, engine, planning, extended, el } = mount();
    click(el, 'plan-extend-open'); fixture.detectChanges();
    click(el, 'plan-extend-confirm');
    expect(engine.preview).toHaveBeenCalledWith({ startDate: '2026-11-13', endDate: '2026-11-22', expectedWeeklyDraftVersion: 4, reuseFreedSlots: true, engineVersion: 2, partial: true }, undefined);
    expect(engine.create).toHaveBeenCalledWith(expect.objectContaining({ fingerprint: 'p'.repeat(64) }), undefined);
    expect(planning.publish).toHaveBeenCalledWith('next-plan', 1, null);
    expect(extended).toHaveBeenCalledWith('next-plan');
  });

  it('leaves untouched past work out when asked, and works for a teacher\'s student', () => {
    const { fixture, engine, planning, el } = mount({ staff: 'student-1' });
    click(el, 'plan-extend-open'); fixture.detectChanges();
    fixture.componentInstance.leave.set(true);
    click(el, 'plan-extend-confirm');
    expect(engine.preview).toHaveBeenCalledWith(expect.objectContaining({ leaveUnfinished: true, partial: true }), 'student-1');
    expect(planning.publish).toHaveBeenCalledWith('next-plan', 1, 'student-1');
  });

  it('a plan that ended some time ago gets a week from today', () => {
    const { fixture, engine, el } = mount({ source: source({ endDate: '2026-11-01' }) });
    click(el, 'plan-extend-open'); fixture.detectChanges(); click(el, 'plan-extend-confirm');
    expect(engine.preview).toHaveBeenCalledWith(expect.objectContaining({ startDate: '2026-11-13', endDate: '2026-11-19' }), undefined);
  });

  it('publishes nothing and says why when the week cannot be made or the server refuses', () => {
    const blocked = mount({ preview: { canCreate: false } });
    click(blocked.el, 'plan-extend-open'); blocked.fixture.detectChanges(); click(blocked.el, 'plan-extend-confirm'); blocked.fixture.detectChanges();
    expect(blocked.engine.create).not.toHaveBeenCalled();
    expect(blocked.el.querySelector('[data-testid="plan-extend-error"]')?.textContent).toContain('hazırlanamadı');

    TestBed.resetTestingModule();
    const refused = mount({ create: throwError(() => ({ error: { message: 'Çalışma planını öğretmenin hazırlıyor.' } })) });
    click(refused.el, 'plan-extend-open'); refused.fixture.detectChanges(); click(refused.el, 'plan-extend-confirm'); refused.fixture.detectChanges();
    expect(refused.planning.publish).not.toHaveBeenCalled();
    expect(refused.el.querySelector('[data-testid="plan-extend-error"]')?.textContent).toContain('öğretmenin hazırlıyor');
  });

  it('"this week" runs through the coming Sunday, and from a weekend through the Sunday after', () => {
    expect([weekSpanEnd('2026-11-09'), weekSpanEnd('2026-11-13'), weekSpanEnd('2026-11-14'), weekSpanEnd('2026-11-15')])
      .toEqual(['2026-11-15', '2026-11-15', '2026-11-22', '2026-11-22']);
  });
});
