import { Component, ViewChild } from '@angular/core';
import { CoachingProfileComponent } from '../../../../../shared/coaching-profile.component';
import { environment } from '../../../../environments/environment';

@Component({ selector: 'app-student-coaching-profile', standalone: true, imports: [CoachingProfileComponent],
  template: '<edu-coaching-profile [apiUrl]="apiUrl" role="Student" />' })
export class StudentCoachingProfileComponent {
  readonly apiUrl = environment.apiUrl;
  @ViewChild(CoachingProfileComponent) profile?: CoachingProfileComponent;
  canLeavePage() { return this.profile?.canLeavePage() ?? true; }
}
