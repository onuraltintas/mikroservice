import { ChangeDetectionStrategy, Component, ViewChild } from '@angular/core';
import { StudentLearningSetupComponent } from './student-learning-setup.component';
import { StudentSettingsComponent } from './student-settings.component';

/** T5 (31.10.2026): Profil ve ayarlar › Çalışma tercihlerim — what you study and when, plus your time zone. */
@Component({
  selector: 'app-student-study-preferences',
  standalone: true,
  imports: [StudentLearningSetupComponent, StudentSettingsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="cs-account" aria-labelledby="preferences-title">
    <header>
      <h1 id="preferences-title">Çalışma tercihlerim</h1>
      <p>Neye çalıştığını ve ne zaman müsait olduğunu söyle; planın buna göre kurulur.</p>
    </header>
    <app-student-learning-setup />
    <app-student-settings />
  </div>`
})
export class StudentStudyPreferencesComponent {
  @ViewChild(StudentLearningSetupComponent) setup?: Pick<StudentLearningSetupComponent, 'canLeavePage'>;
  @ViewChild(StudentSettingsComponent) timeZone?: Pick<StudentSettingsComponent, 'canLeavePage'>;
  async canLeavePage() {
    return (await this.setup?.canLeavePage() ?? true) && (await this.timeZone?.canLeavePage() ?? true);
  }
}
