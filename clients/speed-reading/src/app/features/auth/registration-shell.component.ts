import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

type RegistrationAccount = 'student' | 'teacher' | 'institution';

@Component({
  selector: 'app-registration-shell',
  standalone: true,
  imports: [RouterLink, MatIconModule],
  templateUrl: './registration-shell.component.html',
  styleUrl: './registration-shell.component.scss'
})
export class RegistrationShellComponent {
  @Input() account: RegistrationAccount = 'student';
  readonly accounts = [
    { value: 'student', label: 'Öğrenci', icon: 'menu_book', route: '/auth/register' },
    { value: 'teacher', label: 'Öğretmen', icon: 'school', route: '/auth/register-teacher' },
    { value: 'institution', label: 'Kurum', icon: 'domain', route: '/auth/register-school' }
  ];
  readonly content = {
    student: {
      title: 'Öğrenci hesabını oluştur', description: 'Okuma yolculuğun için ilk adımı at.',
      headline: 'Okurken daha fazlasını keşfet.',
      introduction: 'Okuma hızını, odağını ve anlama becerini adım adım geliştir.',
      benefits: ['Seviyene uygun çalışma programı', 'Okuma ve odak egzersizleri', 'Kendi gelişimini takip etme']
    },
    teacher: {
      title: 'Öğretmen hesabını oluştur', description: 'Öğrencilerinin gelişimine eşlik et.',
      headline: 'Her öğrencinin ilerleyişini gör.',
      introduction: 'Çalışmaları düzenle, öğrencilerinin okuma gelişimini tek yerden takip et.',
      benefits: ['Öğrenci çalışmalarını takip etme', 'Gelişim raporlarını inceleme', 'Bağımsız veya kurumla çalışma']
    },
    institution: {
      title: 'Kurum hesabını oluştur', description: 'Kurum bilgilerini ve yönetici hesabını birlikte hazırlayalım.',
      headline: 'Gelişimi birlikte yönet.',
      introduction: 'Öğretmenlerini, öğrencilerini ve kurumunun okuma çalışmalarını bir araya getir.',
      benefits: ['Kurum üyelerini yönetme', 'Öğrenci gelişimini takip etme', 'Öğretmenlerle birlikte çalışma']
    }
  };
}
