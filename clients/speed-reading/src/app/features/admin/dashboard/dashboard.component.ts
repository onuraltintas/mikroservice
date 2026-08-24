import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatRippleModule } from '@angular/material/core';

interface DashboardCard {
  title: string;
  description: string;
  icon: string;
  route: string;
  color: string;
  badge?: string;
}

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatRippleModule
  ],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent {
  dashboardCards: DashboardCard[] = [
    {
      title: 'Egzersiz Yönetimi',
      description: 'Egzersizleri görüntüle, ekle, düzenle ve sil',
      icon: 'fitness_center',
      route: '/admin/exercises',
      color: 'primary',
      badge: 'Popüler'
    },
    {
      title: 'Okuma Metinleri',
      description: 'Okuma metinlerini ve sorularını yönet',
      icon: 'menu_book',
      route: '/admin/reading-texts',
      color: 'success'
    },
    {
      title: 'Program Şablonları',
      description: 'Duolingo-tarzı egzersiz programlarını yönet',
      icon: 'playlist_play',
      route: '/admin/program-templates',
      color: 'info',
      badge: 'Yeni'
    },
    {
      title: 'Kullanıcı Yönetimi',
      description: 'Kullanıcıları ve rollerini yönet',
      icon: 'people',
      route: '/admin/users',
      color: 'warning'
    }
  ];

  contentManagement = [
    {
      title: 'Egzersiz Tipleri',
      description: 'Egzersiz tiplerini ve kategorilerini yönet',
      icon: 'category',
      route: '/admin/exercise-types',
      iconColor: '#667eea'
    },
    {
      title: 'Yaş Grupları',
      description: 'Yaş grubu konfigürasyonlarını yönet',
      icon: 'groups',
      route: '/admin/age-groups',
      iconColor: '#11998e'
    },
    {
      title: 'E-posta Kampanyaları',
      description: 'E-posta kampanyalarını oluştur ve yönet',
      icon: 'email',
      route: '/admin/email-campaigns',
      iconColor: '#f093fb'
    }
  ];

  systemManagement = [
    {
      title: 'Kurumlar',
      description: 'Eğitim kurumları',
      icon: 'business',
      route: '/admin/institutions'
    },
    {
      title: 'Öğretmenler',
      description: 'Öğretmen yönetimi',
      icon: 'school',
      route: '/admin/teachers'
    },
    {
      title: 'Öğrenciler',
      description: 'Öğrenci profilleri',
      icon: 'person',
      route: '/admin/students'
    },
    {
      title: 'Raporlar',
      description: 'Platform raporları',
      icon: 'analytics',
      route: '/admin/reports'
    },
    {
      title: 'Platform Metrikleri',
      description: 'Sistem metrikleri',
      icon: 'insights',
      route: '/admin/metrics'
    }
  ];
}