import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PaymentService } from '../../../core/services/payment.service';

@Component({
    selector: 'app-payment-success',
    standalone: true,
    imports: [CommonModule, RouterModule, MatProgressSpinnerModule, MatCardModule, MatButtonModule, MatIconModule],
    template: `
    <div class="container mx-auto px-4 py-12 flex justify-center">
      <mat-card class="max-w-md w-full p-6 text-center">
        @if (loading) {
          <div class="flex flex-col items-center">
            <mat-spinner diameter="48"></mat-spinner>
            <h2 class="mt-4 text-xl font-medium">Ödeme Onaylanıyor...</h2>
            <p class="text-gray-500 mt-2">Lütfen bekleyin, işleminiz kontrol ediliyor.</p>
          </div>
        } @else if (success) {
          <div class="flex flex-col items-center">
            <div class="bg-green-100 rounded-full p-4 mb-4">
              <mat-icon class="text-green-600 text-6xl !w-16 !h-16 flex items-center justify-center">check_circle</mat-icon>
            </div>
            <h2 class="text-2xl font-bold text-gray-800">Ödeme Başarılı!</h2>
            <p class="text-gray-600 mt-2 mb-6">Aboneliğiniz başarıyla başlatıldı. Hemen öğrenmeye başlayabilirsiniz.</p>
            
            <a mat-raised-button color="primary" routerLink="/dashboard" class="w-full">
              Kursa Başla
            </a>
          </div>
        } @else {
          <div class="flex flex-col items-center">
            <div class="bg-red-100 rounded-full p-4 mb-4">
              <mat-icon class="text-red-600 text-6xl !w-16 !h-16 flex items-center justify-center">error</mat-icon>
            </div>
            <h2 class="text-2xl font-bold text-gray-800">Ödeme Doğrulanamadı</h2>
            <p class="text-gray-600 mt-2 mb-6">Ödeme işlemi sırasında bir sorun oluştu veya doğrulama başarısız oldu.</p>
            
            <div class="flex gap-4 w-full">
                <a mat-stroked-button color="warn" routerLink="/odeme" class="flex-1">
                Tekrar Dene
                </a>
                <a mat-raised-button color="primary" routerLink="/iletisim" class="flex-1">
                Destek
                </a>
            </div>
          </div>
        }
      </mat-card>
    </div>
  `
})
export class PaymentSuccessComponent implements OnInit {
    private route          = inject(ActivatedRoute);
    private paymentService = inject(PaymentService);

    loading = true;
    success = false;
    token: string | null = null;

    ngOnInit() {
        this.route.queryParams.subscribe(params => {
            // Iyzico callback sonrası: ?success=true/false&token=xxx
            const successParam = params['success'];
            this.token = params['token'] ?? null;

            // Eğer backend'den success=false ile gelindiyse direkt başarısız say
            if (successParam === 'false') {
                this.loading = false;
                this.success = false;
                return;
            }

            if (this.token) {
                this.verifyPayment(this.token);
            } else {
                this.loading = false;
                this.success = false;
            }
        });
    }

    private verifyPayment(token: string) {
        this.paymentService.verifyPayment(token).subscribe({
            next: (res) => {
                this.success = res.success;
                this.loading = false;
            },
            error: () => {
                this.success = false;
                this.loading = false;
            }
        });
    }
}
