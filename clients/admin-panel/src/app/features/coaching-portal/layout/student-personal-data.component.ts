import { ChangeDetectionStrategy, Component } from '@angular/core';
import { StudentDataExportComponent } from '../pages/student-data-export.component';

/** T2 (31.10.2026): Kişisel verilerim, opened from the account menu (moved out of Ayarlar). */
@Component({
  selector: 'app-student-personal-data',
  standalone: true,
  imports: [StudentDataExportComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="personal-data" aria-labelledby="personal-data-title">
    <h1 id="personal-data-title">Kişisel verilerim</h1>
    <p>Koçlukta senin hakkında tutulan bilgilerin bir kopyasını istediğin zaman indirebilirsin.</p>
    <app-student-data-export />
  </section>`,
  styles: [`.personal-data{max-width:720px}
    h1{margin:0 0 var(--coaching-space-1);font-size:var(--coaching-text-xl);font-weight:800;color:var(--ui-text)}
    p{margin:0 0 var(--coaching-space-5);color:var(--ui-text-muted);font-size:var(--coaching-text-body)}`]
})
export class StudentPersonalDataComponent {}
