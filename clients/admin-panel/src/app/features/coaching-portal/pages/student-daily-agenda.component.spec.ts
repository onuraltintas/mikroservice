import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { StudentDailyAgendaComponent } from './student-daily-agenda.component';
import { CoachingDailyAgendaService, DailyAgendaPage } from '../../../core/services/coaching-daily-agenda.service';
import { ToasterService } from '../../../core/services/toaster.service';

const sample: DailyAgendaPage = { mode: 'today', todayCount: 3, upcomingCount: 5, overdueCount: 2,
  totalCount: 3, pageNumber: 1, pageSize: 20, generatedAt: '2026-10-21T08:00:00Z', items: [
    { kind: 'study', recordId: 'task-1', title: 'Paragraf çalışması', date: '2026-10-21', startMinute: 720, minutes: 30, timeZoneId: 'Europe/Istanbul', planId: 'plan-1', weekStart: null },
    { kind: 'assignment', recordId: 'assignment-2', title: 'Fen ödevi', date: '2026-10-21', startMinute: 960, minutes: null, timeZoneId: 'Europe/Istanbul', planId: null, weekStart: null },
    { kind: 'decision', recordId: 'decision-3', title: 'Haftalık tekrar kararı', date: '2026-10-21', startMinute: null, minutes: null, timeZoneId: 'Europe/Istanbul', planId: null, weekStart: '2026-10-12' }
  ] };

describe('StudentDailyAgendaComponent', () => {
  async function setup() {
    const api = { get: vi.fn(() => of(sample)) };
    TestBed.configureTestingModule({ imports: [StudentDailyAgendaComponent], providers: [provideRouter([]),
      { provide: CoachingDailyAgendaService, useValue: api },
      { provide: ToasterService, useValue: { error: vi.fn(), success: vi.fn() } }] });
    await TestBed.compileComponents(); const f = TestBed.createComponent(StudentDailyAgendaComponent); f.detectChanges();
    return { f, c: f.componentInstance, api };
  }
  it('combines work kinds, exact destinations, counts and time information', async () => {
    const { f } = await setup(); const links = [...f.nativeElement.querySelectorAll('a.agenda-item')] as HTMLAnchorElement[];
    expect(links).toHaveLength(3);
    expect(links[0].getAttribute('href')).toContain('plan=plan-1&task=task-1');
    expect(links[1].getAttribute('href')).toBe('/coaching-portal/assignments/assignment-2');
    expect(links[2].getAttribute('href')).toContain('week=2026-10-12&decision=decision-3');
    expect(f.nativeElement.textContent).toContain('12:00');
    expect(f.nativeElement.textContent).toContain('Gün içinde');
    expect(f.nativeElement.querySelectorAll('[aria-pressed]')).toHaveLength(3);
  });
  it('switches filters, cancels late responses and keeps exact global counts', async () => {
    const { f, c, api } = await setup(); const late = new Subject<DailyAgendaPage>(); const current = new Subject<DailyAgendaPage>();
    api.get.mockReturnValueOnce(late).mockReturnValueOnce(current);
    c.select('upcoming'); c.select('overdue');
    late.next(sample); expect(c.data()).toBeNull();
    current.next({ ...sample, mode: 'overdue', items: [sample.items[1]], totalCount: 1 }); current.complete(); f.detectChanges();
    expect(api.get).toHaveBeenLastCalledWith('overdue', 1);
    expect(c.data()?.upcomingCount).toBe(5); expect(c.data()?.items[0].kind).toBe('assignment');
  });
  it('refreshes pending work after completion and removes stale content on failure', async () => {
    const { f, c, api } = await setup();
    api.get.mockReturnValueOnce(of({ ...sample, items: [], totalCount: 0, todayCount: 0 })); c.refresh(); f.detectChanges();
    expect(f.nativeElement.querySelectorAll('a.agenda-item')).toHaveLength(0);
    expect(f.nativeElement.textContent).toContain('Bekleyen işin yok');
    api.get.mockReturnValueOnce(throwError(() => ({ status: 403 }))); c.refresh(); f.detectChanges();
    expect(c.data()).toBeNull(); expect(f.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
    expect(f.nativeElement.querySelectorAll('a.agenda-item')).toHaveLength(0);
    c.refresh(); expect(c.data()?.totalCount).toBe(3);
  });
  it('preserves the selected filter on paging and refresh', async () => {
    const { c, api } = await setup(); c.select('upcoming'); c.load(2); c.refresh();
    expect(api.get).toHaveBeenLastCalledWith('upcoming', 1);
  });
});
