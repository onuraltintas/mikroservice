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
    buttons[1].click(); fixture.detectChanges();
    expect(input.closest('[hidden]')).not.toBeNull();
    buttons[0].click(); fixture.detectChanges();
    expect(input.value).toBe('Kaydedilmemiş ders');
    expect(input.closest('[hidden]')).toBeNull();
  });
  it('does not leave while either child rejects leaving', async () => {
    const fixture = TestBed.createComponent(StudentStudyRoutineComponent); fixture.detectChanges();
    fixture.componentInstance.calendar = {canLeavePage:vi.fn().mockResolvedValue(true)} as unknown as StudentWeeklyCalendarComponent;
    fixture.componentInstance.hours = {canLeavePage:vi.fn().mockResolvedValue(false)} as unknown as StudentStudyAvailabilityComponent;
    expect(await fixture.componentInstance.canLeavePage()).toBe(false);
  });
});
