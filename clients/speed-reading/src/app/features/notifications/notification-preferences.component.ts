import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import {
  NotificationService,
  NotificationPreference,
  NotificationType
} from '../../core/services/notification.service';
import { ToasterService } from '../../core/services/toaster.service';

@Component({
  selector: 'app-notification-preferences',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatDividerModule
  ],
  templateUrl: './notification-preferences.component.html',
  styleUrls: ['./notification-preferences.component.scss']
})
export class NotificationPreferencesComponent implements OnInit, OnDestroy {
  private notificationService = inject(NotificationService);
  private toaster = inject(ToasterService);
  private router = inject(Router);
  private destroy$ = new Subject<void>();

  preferences: NotificationPreference[] = [];
  loading = true;
  saving = false;

  // Notification type groups for UI organization
  notificationGroups = [
    // Assignment notification group removed - system deprecated
    {
      title: 'Başarı ve Hedef Bildirimleri',
      icon: 'emoji_events',
      description: 'İlerlemeniz ve başarılarınız hakkında bildirimler',
      types: [
        NotificationType.MilestoneAchieved,
        NotificationType.AchievementUnlocked,
        NotificationType.GoalCompleted
      ]
    },
    {
      title: 'Aktivite Bildirimleri',
      icon: 'check_circle',
      description: 'Egzersiz ve okuma aktiviteleriniz hakkında bildirimler',
      types: [
        NotificationType.ExerciseCompleted,
        NotificationType.DailyReminder
      ]
    },
    {
      title: 'Rapor Bildirimleri',
      icon: 'assessment',
      description: 'Haftalık ve aylık performans raporları',
      types: [
        NotificationType.WeeklyProgress,
        NotificationType.MonthlyProgress,
        NotificationType.TeacherFeedback
      ]
    },
    {
      title: 'Sistem Bildirimleri',
      icon: 'campaign',
      description: 'Genel duyurular ve mesajlar',
      types: [
        NotificationType.SystemAnnouncement
      ]
    },
    {
      title: 'Öğretmen Bildirimleri',
      icon: 'school',
      description: 'Öğrencilerinizle ilgili bildirimler',
      types: [
        NotificationType.StudentActivitySummary,
        NotificationType.StudentProgramCompleted
      ]
    },
    {
      title: 'Yönetici Bildirimleri',
      icon: 'admin_panel_settings',
      description: 'Sistem ve kullanıcı yönetimi bildirimleri',
      types: [
        NotificationType.NewUserRegistered,
        NotificationType.SystemError
      ]
    }
  ];

  ngOnInit(): void {
    this.loadPreferences();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadPreferences(): void {
    this.loading = true;
    this.notificationService.getPreferences()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (preferences) => {
          this.preferences = preferences;
          this.loading = false;
        },
        error: (err) => {
          console.error('Error loading preferences:', err);
          this.toaster.error('Bildirim tercihleri yüklenemedi', 3000);
          this.loading = false;
        }
      });
  }

  getPreference(type: NotificationType): NotificationPreference | undefined {
    return this.preferences.find(p => p.notificationType === type);
  }

  getNotificationTypeLabel(type: NotificationType): string {
    return this.notificationService.getNotificationTypeLabel(type);
  }

  getNotificationIcon(type: NotificationType): string {
    return this.notificationService.getNotificationIcon(type);
  }

  getNotificationDescription(type: NotificationType): string {
    const descriptions: { [key in NotificationType]: string } = {
      [NotificationType.NewAssignment]: 'Yeni bir ödev size atandığında bildirim alın',
      [NotificationType.AssignmentDueSoon]: 'Ödevin son teslim tarihi yaklaştığında hatırlatma alın',
      [NotificationType.AssignmentOverdue]: 'Geciken ödevler için bildirim alın',
      [NotificationType.ExerciseCompleted]: 'Bir egzersizi tamamladığınızda bildirim alın',
      [NotificationType.MilestoneAchieved]: 'Seviye atladığınızda bildirim alın',
      [NotificationType.AchievementUnlocked]: 'Yeni bir başarı kazandığınızda bildirim alın',
      [NotificationType.GoalCompleted]: 'Günlük hedefinize ulaştığınızda bildirim alın',
      [NotificationType.DailyReminder]: 'Günlük çalışma hatırlatmaları alın',
      [NotificationType.SystemAnnouncement]: 'Sistem duyuruları için bildirim alın',
      [NotificationType.TeacherFeedback]: 'Öğretmeninizden geri bildirim aldığınızda bildirim alın',
      [NotificationType.WeeklyProgress]: 'Haftalık ilerleme raporları alın',
      [NotificationType.MonthlyProgress]: 'Aylık performans raporları alın',
      [NotificationType.StudentActivitySummary]: 'Öğrencilerinizin günlük aktivite özetini alın',
      [NotificationType.StudentProgramCompleted]: 'Bir öğrenciniz programı tamamladığında bildirim alın',
      [NotificationType.NewUserRegistered]: 'Yeni bir kullanıcı kayıt olduğunda bildirim alın',
      [NotificationType.SystemError]: 'Sistemde kritik bir hata oluştuğunda bildirim alın'
    };
    return descriptions[type] || 'Bildirim alın';
  }

  savePreferences(): void {
    this.saving = true;
    this.notificationService.updatePreferences(this.preferences)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toaster.success('Bildirim tercihleri kaydedildi', 3000);
          this.saving = false;
        },
        error: (err) => {
          console.error('Error saving preferences:', err);
          this.toaster.error('Bildirim tercihleri kaydedilemedi', 3000);
          this.saving = false;
        }
      });
  }

  enableAllInApp(): void {
    this.preferences.forEach(p => p.enableInApp = true);
  }

  disableAllInApp(): void {
    this.preferences.forEach(p => p.enableInApp = false);
  }

  enableAllEmail(): void {
    this.preferences.forEach(p => p.enableEmail = true);
  }

  disableAllEmail(): void {
    this.preferences.forEach(p => p.enableEmail = false);
  }

  goBack(): void {
    this.router.navigate(['/student/settings']);
  }
}
