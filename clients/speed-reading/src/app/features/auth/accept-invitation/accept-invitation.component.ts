import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

import { RegistrationShellComponent } from '../registration-shell.component';

@Component({
  selector: 'app-accept-invitation',
  standalone: true,
  imports: [CommonModule, RegistrationShellComponent],
  template: `
    <app-registration-shell mode="page">
      <section class="ds-auth__form invitation">
        <ng-container *ngIf="!accepted && !error">
          <div class="ds-auth__heading">
            <h1 class="ds-auth__title">Daveti kabul et</h1>
            <p class="ds-auth__lead history-notice">
              Kabul edersen seni davet eden kurumun yöneticileri ve öğretmenleri, önceki kurum ve öğretmenlerindeki
              Hızlı Okuma çalışma geçmişini (alıştırmalar, okuma ve anlama sonuçları, değerlendirmeler, ödevler) görebilir.
            </p>
          </div>
          <div class="actions">
            <button class="ds-btn ds-btn--primary ds-btn--large" type="button" data-testid="accept-invitation"
                    [disabled]="loading" (click)="accept()">
              {{ loading ? 'Kabul ediliyor…' : 'Kabul et' }}
            </button>
            <button class="ds-btn ds-btn--secondary ds-btn--large" type="button" [disabled]="loading" (click)="continue()">Vazgeç</button>
          </div>
        </ng-container>
        <ng-container *ngIf="accepted || error">
          <div class="ds-auth__heading">
            <h1 class="ds-auth__title">{{ error ? 'Davet kabul edilemedi' : 'Davet kabul edildi' }}</h1>
          </div>
          <p [class.ds-alert]="!!error" [class.invitation-ok]="!error" [attr.role]="error ? 'alert' : 'status'">{{ error || 'Bağlantın oluşturuldu. Çalışma alanına dönebilirsin.' }}</p>
          <button class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" type="button" (click)="continue()">Devam et</button>
        </ng-container>
      </section>
    </app-registration-shell>
  `,
  styles: [`
    .actions { display: flex; flex-wrap: wrap; gap: 12px; }
    .actions .ds-btn--primary { flex: 1 1 200px; }
    p { margin: 0; }
    .invitation-ok { padding: 14px 16px; border: 1px solid currentColor; border-radius: var(--ds-radius-control); background: var(--ds-success-soft); color: var(--ds-success); line-height: 1.5; }
  `]
})
export class AcceptInvitationComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);

  loading = false;
  accepted = false;
  error = '';
  private invitationId: string | null = null;

  ngOnInit(): void {
    this.invitationId = this.route.snapshot.queryParamMap.get('id');
    if (!this.invitationId) this.error = 'Davet bağlantısı geçersiz.';
  }

  accept(): void {
    if (!this.invitationId || this.loading) return;
    this.loading = true;
    this.http.post(`${environment.apiUrl}/speed-reading/invitations/${this.invitationId}/accept`, {}).subscribe({
      next: () => {
        this.loading = false;
        this.accepted = true;
      },
      error: error => {
        this.loading = false;
        this.error = error.error?.message || error.error?.Message || 'Davet süresi dolmuş veya size ait değil.';
      }
    });
  }

  continue(): void {
    this.router.navigate(['/']);
  }
}
