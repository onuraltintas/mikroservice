import { ChangeDetectionStrategy, Component, ViewChild } from '@angular/core';
import { CoachingProfileComponent } from '../../../../../shared/coaching-profile.component';
import { environment } from '../../../../environments/environment';
import { StudentParentLinksComponent } from './student-parent-links.component';
import { StudentTeachersComponent } from './student-teachers.component';

/** T5 (31.10.2026): Profil ve ayarlar › Profilim — your details, your teachers and the parents linked to you. */
@Component({
  selector: 'app-student-coaching-profile', standalone: true,
  imports: [CoachingProfileComponent, StudentParentLinksComponent, StudentTeachersComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="cs-account" aria-labelledby="profile-title">
    <header>
      <h1 id="profile-title">Profilim</h1>
      <p>Öğretmenlerin seni bu bilgilerle tanır.</p>
    </header>
    <edu-coaching-profile [apiUrl]="apiUrl" role="Student" />
    <section aria-label="Öğretmenlerim"><app-student-teachers /></section>
    <section aria-label="Veli bağlantıları"><app-student-parent-links /></section>
  </div>`
})
export class StudentCoachingProfileComponent {
  readonly apiUrl = environment.apiUrl;
  @ViewChild(CoachingProfileComponent) profile?: CoachingProfileComponent;
  async canLeavePage() { return await this.profile?.canLeavePage() ?? true; }
}
