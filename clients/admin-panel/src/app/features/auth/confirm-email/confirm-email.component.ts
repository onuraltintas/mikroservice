import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/auth/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';

@Component({
    selector: 'app-confirm-email',
    standalone: true,
    imports: [
        CommonModule,
        RouterLink,
        MatCardModule,
        MatButtonModule,
        MatIconModule,
        MatProgressSpinnerModule
    ],
    template: `
    <div class="min-h-screen w-full flex items-center justify-center bg-gray-50 dark:bg-gray-900 p-4 relative overflow-hidden">
      <!-- Background Decoration -->
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-[-10%] left-[-10%] w-[50%] h-[50%] bg-blue-400/10 rounded-full blur-[120px]"></div>
        <div class="absolute bottom-[-10%] right-[-10%] w-[50%] h-[50%] bg-purple-400/10 rounded-full blur-[120px]"></div>
      </div>

      <div class="w-full max-w-md bg-white dark:bg-gray-800 rounded-[2rem] shadow-2xl p-8 text-center relative z-10 border border-gray-100 dark:border-gray-700">
        
        <div class="mb-8 flex justify-center">
          <div class="w-20 h-20 bg-blue-100 dark:bg-blue-900/30 rounded-full flex items-center justify-center">
            <mat-icon class="!text-blue-600 dark:!text-blue-400 !text-4xl !w-10 !h-10">mark_email_read</mat-icon>
          </div>
        </div>

        <h2 class="text-3xl font-bold text-gray-900 dark:text-white mb-4">E-posta Doğrulama</h2>
        
        <div *ngIf="status() === 'verifying'" class="flex flex-col items-center gap-4 py-8">
          <mat-spinner diameter="48"></mat-spinner>
          <p class="text-gray-600 dark:text-gray-400">E-posta adresiniz doğrulanıyor, lütfen bekleyin...</p>
        </div>

        <div *ngIf="status() === 'success'" class="animate-in fade-in slide-in-from-bottom-4 duration-500">
          <div class="p-4 bg-green-50 dark:bg-green-900/20 rounded-xl border border-green-100 dark:border-green-800 text-green-700 dark:text-green-400 mb-8">
            <p class="font-medium text-lg">Tebrikler!</p>
            <p>E-posta adresiniz başarıyla doğrulandı. Artık giriş yapabilirsiniz.</p>
          </div>
          <button mat-flat-button color="primary" routerLink="/auth/login" class="!py-6 !rounded-xl !text-lg !font-bold w-full shadow-lg shadow-blue-500/20 transition-all">
            Giriş Yap
          </button>
        </div>

        <div *ngIf="status() === 'error'" class="animate-in fade-in slide-in-from-bottom-4 duration-500">
          <div class="p-4 bg-red-50 dark:bg-red-900/20 rounded-xl border border-red-100 dark:border-red-800 text-red-700 dark:text-red-400 mb-8">
            <p class="font-medium text-lg">Doğrulama Hatası</p>
            <p>{{ error() }}</p>
          </div>
          <button mat-stroked-button color="primary" routerLink="/auth/login" class="!py-6 !rounded-xl !text-lg !font-bold w-full transition-all">
            Giriş Sayfasına Dön
          </button>
        </div>

      </div>
    </div>
  `
})
export class ConfirmEmailComponent implements OnInit {
    private route = inject(ActivatedRoute);
    private authService = inject(AuthService);
    private toaster = inject(ToasterService);

    status = signal<'verifying' | 'success' | 'error'>('verifying');
    error = signal<string | null>(null);

    ngOnInit() {
        const userId = this.route.snapshot.queryParamMap.get('userId');
        const token = this.route.snapshot.queryParamMap.get('token');

        if (!userId || !token) {
            this.status.set('error');
            this.error.set('Geçersiz doğrulama bağlantısı.');
            return;
        }

        this.verifyEmail(userId, token);
    }

    async verifyEmail(userId: string, token: string) {
        try {
            await this.authService.confirmEmail(userId, token);
            this.status.set('success');
            this.toaster.success('E-posta adresiniz doğrulandı!');
        } catch (err: any) {
            this.status.set('error');
            this.error.set(err.error?.message || 'E-posta doğrulanırken bir hata oluştu.');
            this.toaster.error('E-posta doğrulaması başarısız.');
        }
    }
}
