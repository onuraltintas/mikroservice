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
    <div class="cs-messages-page">
      <p class="intro">{{ intro }}</p>
      <edu-coaching-messages [apiUrl]="apiUrl" [viewerRole]="role" [selectedId]="selectedId()" (selectedIdChange)="go($event)" />
    </div>`,
  styles: [`.cs-messages-page{max-width:1100px}.intro{margin:0 0 var(--coaching-space-4);color:var(--ui-text-muted);font-size:var(--coaching-text-sm)}`]
})
export class CoachingMessagesPageComponent {
  readonly apiUrl = environment.apiUrl;
  private readonly router = inject(Router);
  private readonly profile = inject(AuthService).userProfile();
  readonly role: 'Student' | 'Parent' = hasRole(this.profile, 'Parent') && !hasRole(this.profile, 'Student') ? 'Parent' : 'Student';
  readonly intro = this.role === 'Parent'
    ? 'Çocuğunun öğretmenlerine buradan yaz. Mesajlar 2 yıl saklanır, kurumun gerekirse okuyabilir.'
    : 'Öğretmenlerine buradan yaz. Mesajlar 2 yıl saklanır, kurumun gerekirse okuyabilir.';
  readonly selectedId = toSignal(inject(ActivatedRoute).paramMap.pipe(map(params => params.get('conversationId'))), { initialValue: null });

  go(id: string | null) {
    void this.router.navigate(id ? ['/coaching-portal/messages', id] : ['/coaching-portal/messages']);
  }
}
