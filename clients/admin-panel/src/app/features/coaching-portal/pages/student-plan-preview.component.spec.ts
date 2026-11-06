import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CoachingStudyPlanningService, StudyAvailability } from '../../../core/services/coaching-study-planning.service';
import { CoachingWeeklyDraftService } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingWeeklyOutcomePlanService } from '../../../core/services/coaching-weekly-outcome-plan.service';
import { StudentPlanPreviewComponent } from './student-plan-preview.component';
import { StudentWeeklyOutcomePlanComponent } from './student-weekly-outcome-plan.component';
import { WeeklySlot } from './weekly-calendar';

// E6 #29 (live test 06.11): "Plan önizlemesi" in Çalışma tercihlerim shows the draft itself, built from the saved availability
// with the plan wizard's rules and its annual preview, instead of a bare link to the wizard.
describe('StudentPlanPreviewComponent', () => {
  const lessons = [{ id: 'math', name: 'Matematik', examCode: 'LGS', gradeNumber: 8 }, { id: 'science', name: 'Fen Bilimleri', examCode: 'LGS', gradeNumber: 8 }];
  const mondayEvening: StudyAvailability = { version: 5, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1140 }] };
  const saved: WeeklySlot[] = [{ id: 'old', lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', day: 'Friday', startMinute: 1080, durationMinutes: 60 }];
  const request = { startDate: '2026-11-09', endDate: '2027-06-01', expectedWeeklyDraftVersion: 1, reuseFreedSlots: false };
  async function setup(availability: StudyAvailability | null, slots: WeeklySlot[] = []) {
    const weekly = { get: vi.fn(() => of({ draft: slots.length ? { version: 1, timeZoneId: 'Europe/Istanbul', slots } : null, lessons })),
      save: vi.fn((_version: number | null, zone: string, next: WeeklySlot[]) => of({ version: 1, timeZoneId: zone, slots: next })) };
    const planning = { getAvailability: vi.fn(() => of(availability)) };
    const engine = { options: vi.fn(() => of({ startDate: '2026-11-09', endDate: '2027-06-01', weeklyDraftVersion: 1, timeZoneId: 'Europe/Istanbul', exams: [] })),
      preview: vi.fn(() => of({ request, timeZoneId: 'Europe/Istanbul', fingerprint: 'f', canCreate: true, protectedTaskCount: 0, issues: [],
        schedule: { tasks: [], freeMinutes: 0, recommendedEndDate: '2027-05-31', lessons: [{ lessonId: 'math', lessonName: 'Matematik', outcomeCount: 3,
          weeklyMinutes: 60, requiredMinutes: 60, scheduledMinutes: 60, remainingMinutes: 0, finishDate: '2027-05-31', extraWeeklyMinutes: 0 }] } })),
      create: vi.fn(() => of({ id: 'draft-1', version: 1 })) };
    TestBed.configureTestingModule({ imports: [StudentPlanPreviewComponent], providers: [provideRouter([]),
      { provide: CoachingWeeklyDraftService, useValue: weekly }, { provide: CoachingStudyPlanningService, useValue: planning },
      { provide: CoachingWeeklyOutcomePlanService, useValue: engine }] });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(StudentPlanPreviewComponent);
    fixture.componentRef.setInput('lessonIds', ['MATH', 'science']);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, weekly, planning, engine, navigate, el: fixture.nativeElement as HTMLElement };
  }
  const button = (el: HTMLElement, name: string) => Array.from(el.querySelectorAll('button')).find(x => x.textContent?.trim() === name) as HTMLButtonElement | undefined;

  it('previews the weekly lessons from the saved availability with the wizard rules', async () => {
    const { el, weekly } = await setup(mondayEvening);
    expect(el.textContent).toContain('Pazartesi 17:00–19:00');
    const lines = Array.from(el.querySelectorAll('[data-testid="preview-slot"]')).map(x => x.textContent?.replace(/\s+/g, ' ').trim());
    expect(lines).toEqual(['Pazartesi · 17:00 · Matematik (60 dk)', 'Pazartesi · 18:00 · Fen Bilimleri (60 dk)']);
    expect(el.querySelector('a[href="/coaching-portal/plan/weekly"]')?.textContent).toContain('Takvimde düzenle');
    expect(weekly.save).not.toHaveBeenCalled();
  });

  it('saves the previewed weekly lessons only on request and then shows the plan draft preview', async () => {
    const { fixture, el, weekly, engine } = await setup(mondayEvening);
    button(el, 'Bu düzeni kaydet ve planı önizle')!.click();
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(weekly.save).toHaveBeenCalledWith(null, 'Europe/Istanbul', [
      expect.objectContaining({ lessonId: 'math', day: 'Monday', startMinute: 1020, durationMinutes: 60 }),
      expect.objectContaining({ lessonId: 'science', day: 'Monday', startMinute: 1080, durationMinutes: 60 })]);
    expect(fixture.debugElement.query(By.directive(StudentWeeklyOutcomePlanComponent))).toBeTruthy();
    expect(engine.preview).toHaveBeenCalledOnce();
    expect(el.textContent).toContain('Dersler ne zaman bitecek?');
  });

  it('shows the plan draft preview directly when weekly lessons are saved, with the way to publish', async () => {
    const { fixture, el, engine, navigate } = await setup(mondayEvening, saved);
    expect(engine.options).toHaveBeenCalledOnce();
    expect(engine.preview).toHaveBeenCalledOnce();
    expect(el.textContent).toContain('Cuma · 18:00 · Matematik (60 dk)');
    expect(el.textContent).toContain('Planı yayımla');
    expect(el.querySelector('a[href="/coaching-portal/plan/weekly"]')?.textContent).toContain('Takvimde düzenle');
    const page = fixture.debugElement.query(By.directive(StudentWeeklyOutcomePlanComponent)).componentInstance as StudentWeeklyOutcomePlanComponent;
    page.confirmed = true; page.save();
    expect(navigate).toHaveBeenCalledWith(['/coaching-portal/plan'], { queryParams: { plan: 'draft-1' } });
  });

  it('asks for the availability in Çalışma tercihlerim when none is saved', async () => {
    const { component, el, engine } = await setup(null);
    const edit = vi.fn(); component.editAvailability.subscribe(edit);
    expect(el.textContent).toContain('müsaitliğini kaydet');
    button(el, 'Müsaitliğimi gir')!.click();
    expect(edit).toHaveBeenCalledOnce();
    expect(engine.options).not.toHaveBeenCalled();
  });

  it('keeps the proposal and shows the error when the weekly lessons cannot be saved', async () => {
    const { fixture, el, weekly } = await setup(mondayEvening);
    weekly.save.mockReturnValueOnce(throwError(() => ({ status: 409, error: { message: 'Haftalık düzenin başka bir oturumda değişti.' } })));
    button(el, 'Bu düzeni kaydet ve planı önizle')!.click();
    fixture.detectChanges(); await fixture.whenStable();
    expect(el.textContent).toContain('başka bir oturumda');
    expect(el.querySelectorAll('[data-testid="preview-slot"]')).toHaveLength(2);
    expect(fixture.debugElement.query(By.directive(StudentWeeklyOutcomePlanComponent))).toBeNull();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
