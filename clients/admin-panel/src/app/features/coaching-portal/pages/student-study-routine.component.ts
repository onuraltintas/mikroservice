import { Component, ViewChild, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { StudentWeeklyCalendarComponent } from './student-weekly-calendar.component';
import { StudentStudyAvailabilityComponent } from './student-study-availability.component';

@Component({
  selector: 'app-student-study-routine', standalone: true,
  imports: [StudentWeeklyCalendarComponent, StudentStudyAvailabilityComponent],
  template: `
    <nav class="routine-tabs actions" aria-label="Haftalık düzen bölümleri">
      <button type="button" [attr.aria-pressed]="tab === 'calendar'" (click)="tab = 'calendar'">Haftalık ders düzenim</button>
      <button type="button" [attr.aria-pressed]="tab === 'hours'" (click)="tab = 'hours'">Müsait saatlerim</button>
    </nav>
    <p>Müsait saatlerin ne zaman çalışabileceğini, haftalık ders düzenin bu saatlerde hangi derslere çalışacağını belirler.</p>
    <div [hidden]="tab !== 'calendar'"><app-student-weekly-calendar /></div>
    <div [hidden]="tab !== 'hours'"><app-student-study-availability /></div>
  `,
  styles: [`.routine-tabs{display:flex;flex-wrap:wrap;gap:.5rem;margin-bottom:1rem}.routine-tabs button{font:inherit;padding:.7rem 1rem;border:1px solid #a4b9b0;border-radius:.75rem;background:transparent;color:inherit;cursor:pointer}.routine-tabs button[aria-pressed=true]{background:#315d50;color:white}button:focus-visible{outline:3px solid #76bfa8;outline-offset:3px}p{margin:1rem 0}`]
})
export class StudentStudyRoutineComponent {
  tab: 'calendar' | 'hours' = inject(ActivatedRoute).snapshot.data['initialTab'] === 'hours' ? 'hours' : 'calendar';
  @ViewChild(StudentWeeklyCalendarComponent) calendar?: StudentWeeklyCalendarComponent;
  @ViewChild(StudentStudyAvailabilityComponent) hours?: StudentStudyAvailabilityComponent;
  async canLeavePage() {
    return (await this.calendar?.canLeavePage() ?? true) && (await this.hours?.canLeavePage() ?? true);
  }
}
