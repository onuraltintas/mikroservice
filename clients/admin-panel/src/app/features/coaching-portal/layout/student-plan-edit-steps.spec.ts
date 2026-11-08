import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Component } from '@angular/core';
import { of, throwError } from 'rxjs';
import { CoachingWeeklyDraftService } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { StudentPlanEditStepsComponent } from './student-plan-edit-steps.component';
import { STUDENT_HUBS } from './student-hubs';

@Component({ standalone: true, template: '' }) class Blank {}

// T4 (31.10.2026): "Planı düzenle" is one stepwise flow over the weekly lessons, the free hours and the plan wizard.
describe('Planı düzenle steps (T4)', () => {
  const slot = { id: 's1', lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', day: 'Monday', startMinute: 1080, durationMinutes: 60 };
  const window = { day: 'Monday', startMinute: 1020, endMinute: 1140 };
  async function at(url: string, data: { slots?: object[] | 'error'; windows?: object[] | null } = {}) {
    const weekly = { get: vi.fn(() => data.slots === 'error' ? throwError(() => new Error('x'))
      : of({ draft: data.slots ? { version: 1, timeZoneId: 'Europe/Istanbul', slots: data.slots } : null, lessons: [] })) };
    const planning = { getAvailability: vi.fn(() => of(data.windows ? { version: 1, timeZoneId: 'Europe/Istanbul', windows: data.windows } : null)) };
    TestBed.configureTestingModule({ imports: [StudentPlanEditStepsComponent], providers: [provideRouter([{ path: '**', component: Blank }]),
      { provide: CoachingWeeklyDraftService, useValue: weekly }, { provide: CoachingStudyPlanningService, useValue: planning }] });
    await TestBed.inject(Router).navigateByUrl(url);
    const fixture = TestBed.createComponent(StudentPlanEditStepsComponent);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  // Paket H1 (08.11.2026): the free hours come first; the weekly routine places lessons into them.
  it('shows three ordered steps and marks the current one', async () => {
    const el = await at('/coaching-portal/plan/weekly');
    const steps = [...el.querySelectorAll('ol.steps a')];
    expect(steps.map(a => a.textContent?.replace(/\s+/g, ' ').trim())).toEqual(['1 Boş saatlerin', '2 Haftalık düzenin', '3 Planını oluştur']);
    expect(steps.map(a => a.getAttribute('href'))).toEqual(['/coaching-portal/plan/hours', '/coaching-portal/plan/weekly', '/coaching-portal/plan/wizard']);
    expect(steps[1].getAttribute('aria-current')).toBe('step');
    expect(el.querySelector('a.prev')?.getAttribute('href')).toBe('/coaching-portal/plan/hours');
    expect(el.querySelector('a.next')?.getAttribute('href')).toBe('/coaching-portal/plan/wizard');
    expect(el.querySelector('.hint')?.textContent).toBe('Bu saatlerde hangi derse çalışacağını yerleştir. Okul derslerini değil, kendi çalışma saatlerini gir.');
  });

  it('starts with the free hours', async () => {
    const el = await at('/coaching-portal/plan/hours');
    expect(el.querySelector('ol.steps [aria-current=step]')?.textContent).toContain('Boş saatlerin');
    expect(el.querySelector('.hint')?.textContent).toBe('Hangi gün hangi saatlerde çalışabileceğini işaretle.');
    expect(el.querySelector('a.prev')).toBeNull();
    expect(el.querySelector('a.next')?.getAttribute('href')).toBe('/coaching-portal/plan/weekly');
    expect(el.textContent).not.toContain('Okul ve kurs derslerini');
  });

  it('treats the plan preview as the last step and offers the way back to the plan', async () => {
    const el = await at('/coaching-portal/plan/preview');
    expect(el.querySelector('ol.steps [aria-current=step]')?.textContent).toContain('Planını oluştur');
    expect(el.querySelector('a.next')?.getAttribute('href')).toBe('/coaching-portal/plan');
  });

  // Paket F #37: steps 1–2 showed as done on the wizard while the weekly draft was empty.
  it('marks a step done only when its data exists, not because a later step is open', async () => {
    const done = (el: HTMLElement) => [...el.querySelectorAll('ol.steps li')].map(li => li.classList.contains('done'));
    expect(done(await at('/coaching-portal/plan/wizard'))).toEqual([false, false, false]);
    TestBed.resetTestingModule();
    expect(done(await at('/coaching-portal/plan/wizard', { windows: [window] }))).toEqual([true, false, false]);
    TestBed.resetTestingModule();
    expect(done(await at('/coaching-portal/plan/wizard', { slots: [slot] }))).toEqual([false, true, false]);
    TestBed.resetTestingModule();
    const both = await at('/coaching-portal/plan/weekly', { slots: [slot], windows: [window] });
    expect(done(both)).toEqual([true, true, false]);
    expect(both.querySelector('ol.steps li.done a')?.getAttribute('aria-label')).toContain('tamamlandı');
    TestBed.resetTestingModule();
    expect(done(await at('/coaching-portal/plan/wizard', { slots: 'error', windows: [] }))).toEqual([false, false, false]);
  });

  it('stays hidden on the plan itself', async () => {
    const el = await at('/coaching-portal/plan');
    expect(el.querySelector('ol.steps')).toBeNull();
  });

  it('gives the plan hub two tabs: the plan and its editing flow', () => {
    expect(STUDENT_HUBS.plan.tabs.map(tab => tab.label)).toEqual(['Planım', 'Planı düzenle']);
    const edit = STUDENT_HUBS.plan.tabs[1];
    // Paket H1: "Planı düzenle" opens the first step, the free hours.
    expect(edit.path).toBe('/coaching-portal/plan/hours');
    expect(edit.match).toEqual(['/coaching-portal/plan/weekly', '/coaching-portal/plan/wizard', '/coaching-portal/plan/preview']);
  });
});
