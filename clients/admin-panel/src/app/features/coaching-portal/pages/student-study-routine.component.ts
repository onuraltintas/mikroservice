import { Component, ViewChild, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { StudentWeeklyCalendarComponent } from './student-weekly-calendar.component';
import { StudentStudyAvailabilityComponent } from './student-study-availability.component';

@Component({
  selector: 'app-student-study-routine', standalone: true,
  imports: [StudentWeeklyCalendarComponent, StudentStudyAvailabilityComponent],
  template: `
    <nav class="routine-tabs actions" aria-label="Haftalık düzen bölümleri">
      <button type="button" [attr.aria-pressed]="tab === 'hours'" (click)="tab = 'hours'">Müsait saatlerim</button>
      <button type="button" [attr.aria-pressed]="tab === 'calendar'" (click)="tab = 'calendar'">Haftalık düzenim</button>
    </nav>
    <p>Müsait saatlerin ne zaman çalışabileceğini, haftalık düzenin bu saatlerde hangi derse çalışacağını belirler.</p>
    <div [hidden]="tab !== 'calendar'"><app-student-weekly-calendar #weekly /></div>
    <!-- Paket H5: new study times mark the weekly cards outside them again. -->
    <div [hidden]="tab !== 'hours'"><app-student-study-availability (saved)="weekly.loadAvailability()" /></div>
  `,
  styles: [`.routine-tabs{display:flex;flex-wrap:wrap;gap:.5rem;margin-bottom:1rem}.routine-tabs button{font:inherit;padding:.7rem 1rem;border:1px solid var(--ui-border);border-radius:.75rem;background:transparent;color:inherit;cursor:pointer}.routine-tabs button[aria-pressed=true]{background:var(--ui-brand);color:var(--ui-brand-contrast)}button:focus-visible{outline:3px solid var(--ui-border-strong);outline-offset:3px}p{margin:1rem 0}`]
})
export class StudentStudyRoutineComponent {
  tab: 'calendar' | 'hours' = inject(ActivatedRoute).snapshot.data['initialTab'] === 'hours' ? 'hours' : 'calendar';
  @ViewChild(StudentWeeklyCalendarComponent) calendar?: StudentWeeklyCalendarComponent;
  @ViewChild(StudentStudyAvailabilityComponent) hours?: StudentStudyAvailabilityComponent;
  async canLeavePage() {
    return (await this.calendar?.canLeavePage() ?? true) && (await this.hours?.canLeavePage() ?? true);
  }
}
