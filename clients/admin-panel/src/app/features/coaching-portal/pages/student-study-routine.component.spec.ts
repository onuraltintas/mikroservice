import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { vi } from 'vitest';
import { StudentStudyRoutineComponent } from './student-study-routine.component';
import { StudentWeeklyCalendarComponent } from './student-weekly-calendar.component';
import { StudentStudyAvailabilityComponent } from './student-study-availability.component';

@Component({selector:'app-student-weekly-calendar',standalone:true,template:'<input aria-label="Ders notu">'})
class CalendarStub {}
@Component({selector:'app-student-study-availability',standalone:true,template:'<input aria-label="Saat notu">'})
class HoursStub {}

describe('weekly routine tabs', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({imports:[StudentStudyRoutineComponent],providers:[{provide:ActivatedRoute,useValue:{snapshot:{data:{}}}}]});
    TestBed.overrideComponent(StudentStudyRoutineComponent, {set:{imports:[CalendarStub,HoursStub]}});
  });
  it('keeps unsaved inputs mounted when moving between hours and calendar', () => {
    const fixture = TestBed.createComponent(StudentStudyRoutineComponent); fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input[aria-label="Ders notu"]') as HTMLInputElement;
    input.value = 'Kaydedilmemiş ders';
    const buttons = fixture.nativeElement.querySelectorAll('button');
    buttons[0].click(); fixture.detectChanges();
    expect(input.closest('[hidden]')).not.toBeNull();
    buttons[1].click(); fixture.detectChanges();
    expect(input.value).toBe('Kaydedilmemiş ders');
    expect(input.closest('[hidden]')).toBeNull();
  });
  // Paket H1 (08.11.2026): the free hours come first, as in the steps of "Planı düzenle".
  it('puts Müsait saatlerim first and explains the two in that order, keeping the tab each route opens', () => {
    const fixture = TestBed.createComponent(StudentStudyRoutineComponent); fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(Array.from(el.querySelectorAll('nav button')).map(b => b.textContent?.trim())).toEqual(['Müsait saatlerim', 'Haftalık ders düzenim']);
    expect(el.querySelector('p')?.textContent?.trim()).toBe('Müsait saatlerin ne zaman çalışabileceğini, haftalık düzenin bu saatlerde hangi derse çalışacağını belirler.');
    expect(fixture.componentInstance.tab).toBe('calendar');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({imports:[StudentStudyRoutineComponent],providers:[{provide:ActivatedRoute,useValue:{snapshot:{data:{initialTab:'hours'}}}}]});
    TestBed.overrideComponent(StudentStudyRoutineComponent, {set:{imports:[CalendarStub,HoursStub]}});
    expect(TestBed.createComponent(StudentStudyRoutineComponent).componentInstance.tab).toBe('hours');
  });
  it('does not leave while either child rejects leaving', async () => {
    const fixture = TestBed.createComponent(StudentStudyRoutineComponent); fixture.detectChanges();
    fixture.componentInstance.calendar = {canLeavePage:vi.fn().mockResolvedValue(true)} as unknown as StudentWeeklyCalendarComponent;
    fixture.componentInstance.hours = {canLeavePage:vi.fn().mockResolvedValue(false)} as unknown as StudentStudyAvailabilityComponent;
    expect(await fixture.componentInstance.canLeavePage()).toBe(false);
  });
});
