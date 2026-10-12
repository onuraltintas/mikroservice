import { Component } from '@angular/core';

@Component({
  selector: 'app-course-management-admin',
  standalone: true,
  template: `
    <section aria-labelledby="course-admin-title">
      <h1 id="course-admin-title">Kurs yönetimi</h1>
      <p>Yeni ürünün yönetim alanı hazırlanıyor.</p>
      <p>Kurumlar, bağımsız öğretmenler, ürün üyelikleri ve destek işlemleri bu bölümden yönetilecek.</p>
      <p>Yönetim işlemleri ve veri erişimi henüz açılmadı.</p>
    </section>
  `,
  styles: `section { padding: 28px; } h1 { font-size: 28px; } p { line-height: 1.7; }`
})
export class CourseManagementAdminComponent {}
