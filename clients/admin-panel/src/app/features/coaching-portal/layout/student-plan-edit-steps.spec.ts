import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Component } from '@angular/core';
import { StudentPlanEditStepsComponent } from './student-plan-edit-steps.component';
import { STUDENT_HUBS } from './student-hubs';

@Component({ standalone: true, template: '' }) class Blank {}

// T4 (31.10.2026): "Planı düzenle" is one stepwise flow over the weekly lessons, the free hours and the plan wizard.
describe('Planı düzenle steps (T4)', () => {
  async function at(url: string) {
    TestBed.configureTestingModule({ imports: [StudentPlanEditStepsComponent], providers: [provideRouter([{ path: '**', component: Blank }])] });
    await TestBed.inject(Router).navigateByUrl(url);
    const fixture = TestBed.createComponent(StudentPlanEditStepsComponent);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows three ordered steps and marks the current one', async () => {
    const el = await at('/coaching-portal/plan/hours');
    const steps = [...el.querySelectorAll('ol.steps a')];
    expect(steps.map(a => a.textContent?.replace(/\s+/g, ' ').trim())).toEqual(['1 Ders düzenin', '2 Boş saatlerin', '3 Planını oluştur']);
    expect(steps[1].getAttribute('aria-current')).toBe('step');
    expect(el.querySelector('a.prev')?.getAttribute('href')).toBe('/coaching-portal/plan/weekly');
    expect(el.querySelector('a.next')?.getAttribute('href')).toBe('/coaching-portal/plan/wizard');
  });

  it('treats the plan preview as the last step and offers the way back to the plan', async () => {
    const el = await at('/coaching-portal/plan/preview');
    expect(el.querySelector('ol.steps [aria-current=step]')?.textContent).toContain('Planını oluştur');
    expect(el.querySelector('a.next')?.getAttribute('href')).toBe('/coaching-portal/plan');
  });

  it('stays hidden on the plan itself', async () => {
    const el = await at('/coaching-portal/plan');
    expect(el.querySelector('ol.steps')).toBeNull();
  });

  it('gives the plan hub two tabs: the plan and its editing flow', () => {
    expect(STUDENT_HUBS.plan.tabs.map(tab => tab.label)).toEqual(['Planım', 'Planı düzenle']);
    const edit = STUDENT_HUBS.plan.tabs[1];
    expect(edit.path).toBe('/coaching-portal/plan/weekly');
    expect(edit.match).toEqual(['/coaching-portal/plan/hours', '/coaching-portal/plan/wizard', '/coaching-portal/plan/preview']);
  });
});
