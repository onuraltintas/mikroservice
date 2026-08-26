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
    loading = true;
    success = false;
    message = '';
    token: string | null = null;

    constructor(
        private route: ActivatedRoute,
        private cmsService: PublicCmsService
    ) { }

    ngOnInit(): void {
        this.route.queryParams.subscribe(params => {
            this.token = params['token'];
            if (this.token) {
                this.unsubscribe();
            } else {
                this.loading = false;
                this.success = false;
                this.message = 'Geçersiz bağlantı. Token bulunamadı.';
            }
        });
    }

    unsubscribe(): void {
        this.cmsService.unsubscribeNewsletter(this.token!).subscribe({
            next: () => {
                this.loading = false;
                this.success = true;
                this.message = 'Bülten aboneliğinden başarıyla ayrıldınız. Sizi özleyeceğiz!';
            },
            error: (err) => {
                this.loading = false;
                this.success = false;
                this.message = 'Abonelikten ayrılma işlemi sırasında bir hata oluştu. Lütfen daha sonra tekrar deneyin veya bizimle iletişime geçin.';
                console.error('Unsubscribe error:', err);
            }
        });
    }
}
