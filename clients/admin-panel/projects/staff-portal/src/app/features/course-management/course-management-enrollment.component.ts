import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { StaffAuthService } from '../../auth/staff-auth.service';

@Component({
  selector: 'staff-course-management-enrollment',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section aria-labelledby="course-join-title">
      <h2 id="course-join-title">Kurs Yönetimi'ne katıl</h2>
      <p>Mevcut hesabınızla yeni bir çalışma alanı açın.</p>
      <label for="course-join-role">Katılım türü</label>
      <select id="course-join-role" [(ngModel)]="role" [disabled]="busy">
        <option value="Teacher">Özel ders öğretmeni</option>
        <option value="InstitutionOwner">Yeni kurs kurumu</option>
      </select>
      <p>Kurum ve öğrencilerin aktarımı ayrıca yapılır.</p>
      <button type="button" (click)="join()" [disabled]="busy">{{ busy ? 'Katılım tamamlanıyor…' : 'Katıl ve kurs alanını aç' }}</button>
      @if (error) { <p role="alert">{{ error }}</p> }
    </section>
  `,
  styles: `section{margin:20px 12px;padding:16px;border:1px solid #d7dfd6;border-radius:12px}h2{font-size:16px}p,label{font-size:12px;line-height:1.6}select,button{width:100%;padding:10px;margin-top:8px;border-radius:8px;border:1px solid #b9c5b9;background:white;color:#24392e}`
})
export class CourseManagementEnrollmentComponent {
  private readonly auth = inject(StaffAuthService);
  role: 'Teacher' | 'InstitutionOwner' = 'Teacher';
  busy = false;
  error = '';
  async join(): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    this.error = '';
    try { await this.auth.joinCourseManagement(this.role); }
    catch { this.error = 'Katılım tamamlanamadı. E-posta doğrulamanızı kontrol edip yeniden deneyin.'; }
    finally { this.busy = false; }
  }
}
