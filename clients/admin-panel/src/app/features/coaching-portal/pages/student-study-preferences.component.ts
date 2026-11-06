import { ChangeDetectionStrategy, Component, ViewChild } from '@angular/core';
import { StudentLearningSetupComponent } from './student-learning-setup.component';

/**
 * T5 (31.10.2026): Profil ve ayarlar › Çalışma tercihlerim — what you study and when. E3 #34 (06.11.2026): no time zone setting;
 * every time is Türkiye time.
 */
@Component({
  selector: 'app-student-study-preferences',
  standalone: true,
  imports: [StudentLearningSetupComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="cs-account" aria-labelledby="preferences-title">
    <header>
      <h1 id="preferences-title">Çalışma tercihlerim</h1>
      <p>Neye çalıştığını ve ne zaman müsait olduğunu söyle; planın buna göre kurulur. Bütün saatler Türkiye saatine göredir.</p>
    </header>
    <app-student-learning-setup />
  </div>`
})
export class StudentStudyPreferencesComponent {
  @ViewChild(StudentLearningSetupComponent) setup?: Pick<StudentLearningSetupComponent, 'canLeavePage'>;
  async canLeavePage() { return await this.setup?.canLeavePage() ?? true; }
}
