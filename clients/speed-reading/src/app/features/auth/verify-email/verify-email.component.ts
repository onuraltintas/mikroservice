import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-verify-email',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule
  ],
  templateUrl: './verify-email.component.html',
  styleUrl: './verify-email.component.scss'
})
export class VerifyEmailComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  loading = true;
  error = '';
  success = false;

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const email = params['email'];
      const token = params['token'];

      if (!email || !token) {
        this.error = 'Geçersiz doğrulama bağlantısı';
        this.loading = false;
        return;
      }

      this.verifyEmail(email, token);
    });
  }

  verifyEmail(email: string, token: string): void {
    this.authService.verifyEmail({ email, token }).subscribe({
      next: () => {
        this.success = true;
        this.loading = false;
        setTimeout(() => {
          this.router.navigate(['/auth/login']);
        }, 3000);
      },
      error: (err) => {
        this.error = 'E-posta doğrulama başarısız. Bağlantı geçersiz veya süresi dolmuş olabilir.';
        this.loading = false;
      }
    });
  }

  resendVerification(): void {
    // This would need to be implemented if we want to allow resending from this page
    // For now, user can go back to login and request a new link
  }
}
