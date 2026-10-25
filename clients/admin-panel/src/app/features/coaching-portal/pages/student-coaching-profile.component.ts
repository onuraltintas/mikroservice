import { Component, ViewChild } from '@angular/core';
import { CoachingProfileComponent } from '../../../../../shared/coaching-profile.component';
import { environment } from '../../../../environments/environment';
import { RouterLink } from '@angular/router';

@Component({ selector: 'app-student-coaching-profile', standalone: true, imports: [CoachingProfileComponent, RouterLink],
  template: '<a routerLink="/coaching-portal/setup">← Profil ve çalışma tercihlerime dön</a><edu-coaching-profile [apiUrl]="apiUrl" role="Student" />' })
export class StudentCoachingProfileComponent {
  readonly apiUrl = environment.apiUrl;
  @ViewChild(CoachingProfileComponent) profile?: CoachingProfileComponent;
  async canLeavePage() { return await this.profile?.canLeavePage() ?? true; }
}
