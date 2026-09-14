import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-accept-invitation',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatCardModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <main class="invitation-page">
      <mat-card>
        <mat-card-content>
          <mat-spinner *ngIf="loading" diameter="36"></mat-spinner>
          <ng-container *ngIf="!loading">
            <mat-icon [class.error-icon]="!!error">{{ error ? 'error_outline' : 'check_circle' }}</mat-icon>
            <h1>{{ error ? 'Davet kabul edilemedi' : 'Davet kabul edildi' }}</h1>
            <p>{{ error || 'Bağlantınız oluşturuldu. Çalışma alanınıza dönebilirsiniz.' }}</p>
            <button mat-raised-button color="primary" (click)="continue()">Devam et</button>
          </ng-container>
        </mat-card-content>
      </mat-card>
    </main>
  `,
  styles: [`
    .invitation-page { min-height: 100vh; display: grid; place-items: center; padding: 1.5rem; background: #f5f8f8; }
    mat-card { width: min(100%, 28rem); text-align: center; }
    mat-card-content { display: grid; justify-items: center; gap: 1rem; padding: 2rem; }
    mat-icon { width: 2.5rem; height: 2.5rem; font-size: 2.5rem; color: #27766f; }
    .error-icon { color: #b42318; }
    h1, p { margin: 0; }
    p { color: #5f6f6d; line-height: 1.5; }
  `]
})
export class AcceptInvitationComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);

  loading = true;
  error = '';

  ngOnInit(): void {
    const invitationId = this.route.snapshot.queryParamMap.get('id');
    if (!invitationId) {
      this.loading = false;
      this.error = 'Davet bağlantısı geçersiz.';
      return;
    }

    this.http.post(`${environment.apiUrl}/invitations/${invitationId}/accept`, {}).subscribe({
      next: () => this.loading = false,
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
