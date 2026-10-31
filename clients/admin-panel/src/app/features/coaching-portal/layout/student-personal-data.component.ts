import { ChangeDetectionStrategy, Component } from '@angular/core';
import { StudentDataExportComponent } from '../pages/student-data-export.component';

/** T2 (31.10.2026): Kişisel verilerim, opened from the account menu (moved out of Ayarlar). */
@Component({
  selector: 'app-student-personal-data',
  standalone: true,
  imports: [StudentDataExportComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="cs-account" aria-labelledby="personal-data-title">
    <header>
      <h1 id="personal-data-title">Kişisel verilerim</h1>
      <p>Hakkında tuttuğumuz bilgilerin bir kopyasını istediğin zaman indirebilirsin.</p>
    </header>
    <section aria-label="Verilerimi indir"><app-student-data-export /></section>
  </div>`
})
export class StudentPersonalDataComponent {}
