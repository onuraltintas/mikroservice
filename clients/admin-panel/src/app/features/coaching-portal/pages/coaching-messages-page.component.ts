import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService, hasRole } from '../../../core/auth/auth.service';
import { environment } from '../../../../environments/environment';
import { CoachingMessagesComponent } from '../../../../../shared/coaching-messages.component';

/** Roadmap O3: a student's or parent's messages with teachers; the open conversation is kept in the address. */
@Component({
  selector: 'app-coaching-messages-page', standalone: true, imports: [CoachingMessagesComponent],
  template: `
    <div class="space-y-4 p-4 sm:p-6">
      <p class="text-sm text-slate-600 dark:text-slate-300">{{ intro }}</p>
      <edu-coaching-messages [apiUrl]="apiUrl" [viewerRole]="role" [selectedId]="selectedId()" (selectedIdChange)="go($event)" />
    </div>`
})
export class CoachingMessagesPageComponent {
  readonly apiUrl = environment.apiUrl;
  private readonly router = inject(Router);
  private readonly profile = inject(AuthService).userProfile();
  readonly role: 'Student' | 'Parent' = hasRole(this.profile, 'Parent') && !hasRole(this.profile, 'Student') ? 'Parent' : 'Student';
  readonly intro = this.role === 'Parent'
    ? 'Çocuğunun öğretmenlerine buradan yazabilirsin. Mesajlar 2 yıl saklanır; kurum yönetimi gerekirse gerekçesini kaydederek inceleyebilir.'
    : 'Öğretmenlerine buradan yazabilirsin. Mesajlar 2 yıl saklanır; kurum yönetimi gerekirse gerekçesini kaydederek inceleyebilir.';
  readonly selectedId = toSignal(inject(ActivatedRoute).paramMap.pipe(map(params => params.get('conversationId'))), { initialValue: null });

  go(id: string | null) {
    void this.router.navigate(id ? ['/coaching-portal/messages', id] : ['/coaching-portal/messages']);
  }
}
