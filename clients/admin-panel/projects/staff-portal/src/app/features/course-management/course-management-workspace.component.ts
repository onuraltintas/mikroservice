import { Component, Input } from '@angular/core';

@Component({
  selector: 'staff-course-management-workspace',
  standalone: true,
  template: `
    <section aria-labelledby="course-workspace-title">
      <h2 id="course-workspace-title">Kurs yönetimi · {{ role === 'institution' ? 'Kurum' : 'Özel ders' }}</h2>
      <p>Kurs çalışma alanı hazırlanıyor. Öğrenci kayıtları, ders planlama, ödeme takibi ve ödev yönetimi sonraki aşamalarda açılacak.</p>
      <p>Bu ürüne katılım davetle ve ayrı üyelik doğrulamasıyla yapılacak.</p>
    </section>
  `,
  styles: `section { padding: 24px; background: white; border: 1px solid #e4e7ec; border-radius: 16px; } p { line-height: 1.65; }`
})
export class CourseManagementWorkspaceComponent {
  @Input({ required: true }) role!: 'teacher' | 'institution';
}
