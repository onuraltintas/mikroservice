import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject, interval, of } from 'rxjs';
import { tap, switchMap, catchError, filter, map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface Notification {
  id: string;
  type: NotificationType;
  priority: NotificationPriority;
  title: string;
  message: string;
  actionUrl?: string;
  isRead: boolean;
  createdAt: string;
  metadata?: any;
}

export enum NotificationType {
  NewAssignment = 1,
  AssignmentDueSoon = 2,
  AssignmentOverdue = 3,
  ExerciseCompleted = 4,
  MilestoneAchieved = 5,
  WeeklyProgress = 6,
  MonthlyProgress = 7,
  DailyReminder = 8,
  SystemAnnouncement = 9,
  TeacherFeedback = 10,
  AchievementUnlocked = 11,
  GoalCompleted = 12,
  StudentActivitySummary = 13,
  StudentProgramCompleted = 14,
  NewUserRegistered = 15,
  SystemError = 16
}

export enum NotificationPriority {
  Low = 1,
  Normal = 2,
  High = 3,
  Urgent = 4
}

export interface NotificationPreference {
  id: string;
  notificationType: NotificationType;
  enableInApp: boolean;
  enableEmail: boolean;
  enablePush: boolean;
  preferredTime?: string;
}

export interface UnreadCount {
  total: number;
  high: number;
  urgent: number;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private http = inject(HttpClient);
  private authService = inject(AuthService);
  private apiUrl = `${environment.speedReadingApiUrl}/notifications`;

  private unreadCountSubject = new BehaviorSubject<UnreadCount>({ total: 0, high: 0, urgent: 0 });
  public unreadCount$ = this.unreadCountSubject.asObservable();

  private notificationsSubject = new BehaviorSubject<Notification[]>([]);
  public notifications$ = this.notificationsSubject.asObservable();

  constructor() {
    // Poll for new notifications every 30 seconds.
    interval(30000)
      .pipe(
        filter(() => this.authService.isAuthenticated),
        switchMap(() => this.getUnreadCount().pipe(
          catchError(error => {
            console.error('Notification polling error:', error);
            // Return default/empty value to keep the stream alive
            return of({ total: 0, high: 0, urgent: 0 });
          })
        ))
      )
      .subscribe();
  }

  getNotifications(isRead?: boolean, pageNumber: number = 1, pageSize: number = 20): Observable<Notification[]> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (isRead !== undefined) {
      params = params.set('isRead', isRead.toString());
    }

    return this.http.get<any>(this.apiUrl, { params }).pipe(
      map(response => Array.isArray(response) ? response : (response?.items ?? [])),
      tap(notifications => this.notificationsSubject.next(notifications))
    );
  }

  getUnreadCount(): Observable<UnreadCount> {
    return this.http.get<UnreadCount>(`${this.apiUrl}/unread-count`).pipe(
      tap(count => this.unreadCountSubject.next(count))
    );
  }

  markAsRead(notificationId: string): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${notificationId}/mark-read`, {}).pipe(
      tap(() => {
        // Update local state
        const current = this.notificationsSubject.value;
        const updated = current.map(n =>
          n.id === notificationId ? { ...n, isRead: true } : n
        );
        this.notificationsSubject.next(updated);
        this.refreshUnreadCount();
      })
    );
  }

  markAllAsRead(): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/mark-all-read`, {}).pipe(
      tap(() => {
        // Update local state
        const current = this.notificationsSubject.value;
        const updated = current.map(n => ({ ...n, isRead: true }));
        this.notificationsSubject.next(updated);
        this.unreadCountSubject.next({ total: 0, high: 0, urgent: 0 });
      })
    );
  }

  deleteNotification(notificationId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${notificationId}`).pipe(
      tap(() => {
        // Update local state
        const current = this.notificationsSubject.value;
        const updated = current.filter(n => n.id !== notificationId);
        this.notificationsSubject.next(updated);
        this.refreshUnreadCount();
      })
    );
  }

  getPreferences(): Observable<NotificationPreference[]> {
    return this.http.get<NotificationPreference[]>(`${this.apiUrl}/preferences`);
  }

  updatePreferences(preferences: NotificationPreference[]): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/preferences`, preferences);
  }

  private refreshUnreadCount(): void {
    this.getUnreadCount().subscribe();
  }

  // Helper method to get notification type label
  getNotificationTypeLabel(type: NotificationType): string {
    switch (type) {
      case NotificationType.NewAssignment:
        return 'Yeni Ödev';
      case NotificationType.AssignmentDueSoon:
        return 'Yaklaşan Ödev';
      case NotificationType.AssignmentOverdue:
        return 'Gecikmiş Ödev';
      case NotificationType.ExerciseCompleted:
        return 'Egzersiz Tamamlandı';
      case NotificationType.MilestoneAchieved:
        return 'Milestone';
      case NotificationType.WeeklyProgress:
        return 'Haftalık İlerleme';
      case NotificationType.MonthlyProgress:
        return 'Aylık İlerleme';
      case NotificationType.DailyReminder:
        return 'Günlük Hatırlatma';
      case NotificationType.SystemAnnouncement:
        return 'Sistem Duyurusu';
      case NotificationType.TeacherFeedback:
        return 'Öğretmen Geri Bildirimi';
      case NotificationType.AchievementUnlocked:
        return 'Başarı Kazanıldı';
      case NotificationType.GoalCompleted:
        return 'Günlük Hedef';
      case NotificationType.StudentActivitySummary:
        return 'Öğrenci Aktivitesi';
      case NotificationType.StudentProgramCompleted:
        return 'Program Tamamlandı';
      case NotificationType.NewUserRegistered:
        return 'Yeni Kayıt';
      case NotificationType.SystemError:
        return 'Sistem Hatası';
      default:
        return 'Bildirim';
    }
  }

  // Helper method to get notification icon
  getNotificationIcon(type: NotificationType): string {
    switch (type) {
      case NotificationType.NewAssignment:
        return 'assignment';
      case NotificationType.AssignmentDueSoon:
        return 'schedule';
      case NotificationType.AssignmentOverdue:
        return 'warning';
      case NotificationType.ExerciseCompleted:
        return 'check_circle';
      case NotificationType.MilestoneAchieved:
        return 'flag';
      case NotificationType.WeeklyProgress:
        return 'trending_up';
      case NotificationType.MonthlyProgress:
        return 'bar_chart';
      case NotificationType.DailyReminder:
        return 'alarm';
      case NotificationType.SystemAnnouncement:
        return 'campaign';
      case NotificationType.TeacherFeedback:
        return 'rate_review';
      case NotificationType.AchievementUnlocked:
        return 'emoji_events';
      case NotificationType.GoalCompleted:
        return 'check_circle';
      case NotificationType.StudentActivitySummary:
        return 'bar_chart';
      case NotificationType.StudentProgramCompleted:
        return 'school';
      case NotificationType.NewUserRegistered:
        return 'person_add';
      case NotificationType.SystemError:
        return 'error';
      default:
        return 'notifications';
    }
  }

  // Helper method to get priority color
  getPriorityColor(priority: NotificationPriority): string {
    const colors: { [key in NotificationPriority]: string } = {
      [NotificationPriority.Low]: 'gray',
      [NotificationPriority.Normal]: 'blue',
      [NotificationPriority.High]: 'orange',
      [NotificationPriority.Urgent]: 'red'
    };
    return colors[priority] || 'gray';
  }
}
