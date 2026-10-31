import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { CoachingCalendarSubscriptionApi, CoachingCalendarSubscriptionComponent } from '../../../../../shared/coaching-calendar-subscription.component';
import { NotificationPreferencesApi, NotificationPreferencesComponent } from '../../../../../shared/notification-preferences.component';

/** T5 (31.10.2026): Profil ve ayarlar › Bildirimler — e-mail notifications (roadmap O8) and the calendar link (O6). */
@Component({
  selector: 'app-student-notification-settings',
  standalone: true,
  imports: [CoachingCalendarSubscriptionComponent, NotificationPreferencesComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="cs-account" aria-labelledby="notification-settings-title">
    <header>
      <h1 id="notification-settings-title">Bildirimler</h1>
      <p>Hangi haberlerin e-postayla da geleceğini seç.</p>
    </header>
    <section aria-label="E-posta bildirimleri"><edu-notification-preferences [api]="notifications" /></section>
    <section aria-label="Takvim bağlantısı"><edu-coaching-calendar-subscription [api]="calendar" role="student" /></section>
  </div>`
})
export class StudentNotificationSettingsComponent {
  readonly notifications = new NotificationPreferencesApi(inject(HttpClient), environment.apiUrl);
  readonly calendar = new CoachingCalendarSubscriptionApi(inject(HttpClient), environment.apiUrl);
}
