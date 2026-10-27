import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { StudentStudyTodayComponent } from './student-study-today.component';

describe('StudentStudyTodayComponent', () => {
  async function setup(empty = false, failure = false) {
    const service = { list: vi.fn(() => failure ? throwError(() => new Error('offline')) : of({ items: empty ? [] : [{ id: 'plan' }] })),
      getAvailability: vi.fn(() => of({ timeZoneId: 'Europe/Istanbul' })),
      skip: vi.fn(() => of({})), reschedule: vi.fn(() => of({})),
      get: vi.fn(() => of({ id: 'plan', version: 4, title: 'My plan', tasks: [
        { id: 'today', title: 'Today work', plannedDate: '2026-10-02', plannedMinutes: 30, isCompleted: false },
        { id: 'old', title: 'Overdue work', plannedDate: '2026-10-01', plannedMinutes: 20, isCompleted: false },
        { id: 'done', title: 'Done work', plannedDate: '2026-10-02', plannedMinutes: 20, isCompleted: true },
        { id: 'skipped', title: 'Skipped work', plannedDate: '2026-10-01', plannedMinutes: 20, isCompleted: false, skippedAt: '2026-10-01T12:00:00Z' },
        { id: 'future', title: 'Future work', plannedDate: '2026-10-03', plannedMinutes: 20, isCompleted: false }] })) };
    TestBed.configureTestingModule({ imports: [StudentStudyTodayComponent], providers: [provideRouter([]), { provide: CoachingStudyPlanningService, useValue: service }] });
    const fixture = TestBed.createComponent(StudentStudyTodayComponent);
    vi.spyOn(Date, 'now').mockReturnValue(Date.parse('2026-10-02T10:00:00Z'));
    fixture.detectChanges(); await fixture.whenStable();
    return { fixture, service, component: fixture.componentInstance };
  }
  afterEach(() => vi.restoreAllMocks());
  it('uses active plan and preference timezone and shows today and overdue separately', async () => {
    const { fixture, service } = await setup();
    expect(service.list).toHaveBeenCalledWith(1, 'Active');
    expect(service.get).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('Today work');
    expect(fixture.nativeElement.textContent).toContain('Overdue work');
    expect(fixture.nativeElement.textContent).not.toContain('Done work');
    expect(fixture.nativeElement.textContent).not.toContain('Future work');
    expect(fixture.nativeElement.querySelector('.today-date').textContent).toContain('02.10.2026');
    expect(fixture.nativeElement.querySelector('.today-catch-up').textContent).toContain('01.10.2026');
  });
  it('lets the student move missed work to today or skip it, and leaves skipped work out', async () => {
    const { fixture, service } = await setup();
    const box = fixture.nativeElement.querySelector('.today-catch-up');
    expect(box.textContent).not.toContain('Skipped work');
    expect(fixture.nativeElement.querySelector('.today-catch-up strong').textContent).toContain('1 çalışma');
    box.querySelector('button[data-action="today"]').click(); await fixture.whenStable();
    expect(service.reschedule).toHaveBeenCalledWith('plan', 'old', 4, '2026-10-02');
    box.querySelector('button[data-action="skip"]').click(); await fixture.whenStable();
    expect(service.skip).toHaveBeenCalledWith('plan', 'old', 4);
    expect(service.get).toHaveBeenCalledTimes(3);
  });
  it('distinguishes no active plan from a failed request', async () => {
    const { fixture, service } = await setup(true);
    expect(fixture.nativeElement.textContent).toContain('Henüz aktif çalışma planın yok');
    expect(service.get).not.toHaveBeenCalled();
  });
  it('offers retry after loading failure without pretending there is no plan', async () => {
    const { fixture, component } = await setup(false, true);
    expect(component.error()).toBeTruthy();
    expect(fixture.nativeElement.textContent).not.toContain('Henüz aktif çalışma planın yok');
  });
  it('does not require saved availability preferences to show a manual plan', async () => {
    const { fixture, component, service } = await setup();
    service.getAvailability.mockReturnValueOnce(throwError(() => ({ status: 404 })) as any);
    await component.load(); fixture.detectChanges(); await fixture.whenStable();
    expect(component.error()).toBeNull();
    expect(component.title()).toBe('My plan');
    expect(service.get).toHaveBeenCalledTimes(2);
  });
  it('shows the manual plan when availability returns an explicit null', async () => {
    const { component, service } = await setup();
    service.getAvailability.mockReturnValueOnce(of(null) as any);
    await component.load();
    expect(component.error()).toBeNull();
    expect(component.title()).toBe('My plan');
    expect(component.timeZone()).toBe(Intl.DateTimeFormat().resolvedOptions().timeZone);
  });
  it('derives daily progress only from scheduled tasks, with no invented duration', async () => {
    const { fixture, component } = await setup();
    expect(component.scheduledToday()).toBe(2);
    expect(component.completedToday()).toBe(1);
    expect(component.dailyPercentage()).toBe(50);
    expect(component.remainingMinutes()).toBe(30);
    expect(component.recordedMinutes()).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="progressbar"]').getAttribute('aria-valuenow')).toBe('50');
  });
  it('does not present an empty day or failed load as 100% complete', async () => {
    const { fixture, component, service } = await setup();
    service.get.mockReturnValueOnce(of({ title: 'Tomorrow', tasks: [] }));
    await component.load(); fixture.detectChanges(); await fixture.whenStable();
    expect(component.dailyPercentage()).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="progressbar"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Bugüne planlanmış çalışma yok');
    service.list.mockReturnValueOnce(throwError(() => new Error('offline')));
    await component.load(); fixture.detectChanges(); await fixture.whenStable();
    expect(component.error()).toBeTruthy();
    expect(component.completedToday()).toBe(0);
    expect(component.dailyPercentage()).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="progressbar"]')).toBeNull();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
