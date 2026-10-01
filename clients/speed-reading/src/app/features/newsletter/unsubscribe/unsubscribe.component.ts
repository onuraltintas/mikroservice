import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { PublicCmsService } from '../../../core/services/public-cms.service';

@Component({
    selector: 'app-newsletter-unsubscribe',
    standalone: true,
    imports: [
        CommonModule,
        RouterModule,
        MatCardModule,
        MatButtonModule,
        MatProgressSpinnerModule,
        MatIconModule
    ],
    templateUrl: './unsubscribe.component.html',
    styles: [`
    .unsubscribe-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
      background-color: #f3f4f6;
      padding: 20px;
    }
    .unsubscribe-card {
      max-width: 500px;
      width: 100%;
      text-align: center;
      padding: 40px 20px;
    }
    .status-icon {
      font-size: 64px;
      height: 64px;
      width: 64px;
      margin-bottom: 20px;
    }
    .success { color: #10b981; }
    .error { color: #ef4444; }
    .spinner-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 20px;
    }
  `]
})
export class UnsubscribeComponent implements OnInit {
    loading = false;
    complete = false;
    success = false;
    message = '';
    token: string | null = null;
    action: 'confirm' | 'unsubscribe' = 'unsubscribe';

    constructor(
        private route: ActivatedRoute,
        private cmsService: PublicCmsService
    ) { }

    ngOnInit(): void {
        this.route.queryParams.subscribe(params => {
            this.token = params['token'];
            this.action = this.route.snapshot.routeConfig?.path === 'confirm' ? 'confirm' : 'unsubscribe';
            this.message = this.action === 'confirm'
                ? 'Devam etmek için e-posta aboneliğinizi açıkça onaylayın.'
                : 'Devam etmek için bülten aboneliğinizi iptal etmeyi onaylayın.';
            if (!this.token) {
                this.complete = true;
                this.message = 'Geçersiz bağlantı. Token bulunamadı.';
            }
        });
    }

    processRequest(): void {
        if (!this.token || this.loading) return;
        this.loading = true;
        const request = this.action === 'confirm'
            ? this.cmsService.confirmNewsletterSubscription(this.token)
            : this.cmsService.unsubscribeNewsletter(this.token);
        request.subscribe({
            next: () => {
                this.loading = false;
                this.complete = true;
                this.success = true;
                this.message = this.action === 'confirm'
                    ? 'E-posta adresiniz doğrulandı ve bülten aboneliğiniz etkinleştirildi.'
                    : 'Bülten aboneliğiniz iptal edildi.';
            },
            error: () => {
                this.loading = false;
                this.complete = true;
                this.success = false;
                this.message = this.action === 'confirm'
                    ? 'Onay bağlantısı geçersiz veya süresi dolmuş. Yeni bir abonelik isteği oluşturabilirsiniz.'
                    : 'Abonelik iptal bağlantısı geçersiz. Lütfen e-postadaki bağlantıyı yeniden kontrol edin.';
            }
        });
    }
}
