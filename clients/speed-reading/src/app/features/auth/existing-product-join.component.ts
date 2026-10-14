import { Component, Input, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { RegistrationLegalConsentComponent } from './registration-legal-consent.component';
import { RegistrationLegalAcceptance } from '../../core/models/user.model';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-existing-product-join',
  standalone: true,
  imports: [ReactiveFormsModule, MatInputModule, MatButtonModule, RegistrationLegalConsentComponent],
  template: `
    <section aria-label="Mevcut hesapla servise katılım">
      <h3>Mevcut hesabınla katıl</h3>
      <p>Aynı e-posta ve mevcut şifreni kullan. Hesabın ve kurum bağlantın korunur.
        Bu işlem abonelik satın almaz; servis yetkileri ve abonelikleri ayrı kalır.</p>
      @if (joined) {
        <p role="status">Katılım tamamlandı. Mevcut şifrenle giriş yapabilirsin.</p>
        <a [href]="loginUrl">Panele giriş yap</a>
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()">
          <mat-form-field appearance="outline">
            <mat-label>Mevcut e-posta adresi</mat-label>
            <input matInput type="email" autocomplete="username" formControlName="email">
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Mevcut şifre</mat-label>
            <input matInput type="password" autocomplete="current-password" formControlName="password">
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>İki adımlı doğrulama kodu (varsa)</mat-label>
            <input matInput inputmode="numeric" autocomplete="one-time-code" maxlength="6" formControlName="mfaCode">
            <mat-hint>Hesabında MFA açıksa doğrulama uygulamandaki yeni kodu gir.</mat-hint>
          </mat-form-field>
          <app-registration-legal-consent [product]="product"
            (acceptancesChange)="legalAcceptances = $event"
            (readinessChange)="legalReady = $event" />
          @if (error) { <p role="alert">{{ error }}</p> }
          <button mat-flat-button type="submit" [disabled]="loading || form.invalid || !legalReady || legalAcceptances.length !== 3">
            {{ loading ? 'Katılım tamamlanıyor…' : 'Bu servise katıl' }}
          </button>
        </form>
      }
    </section>
  `,
  styles: [`
    :host { display:block; margin:1rem 0; }
    section { padding:1rem; border:1px solid #94a3b8; border-radius:12px; }
    h3 { margin:0 0 .75rem; font-size:1.2rem; }
    p { line-height:1.5; margin-bottom:1rem; }
    form { display:flex; flex-direction:column; gap:1rem; }
    [role="alert"] { color:#b91c1c; }
  `]
})
export class ExistingProductJoinComponent {
  @Input() product: 'coaching' | 'speed-reading' = 'speed-reading';
  @Input() account: 'student' | 'teacher' | 'institution' = 'student';
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.maxLength(1024)]],
    mfaCode: ['', Validators.pattern(/^\d{6}$/)]
  });
  loading = false;
  joined = false;
  error = '';
  legalReady = false;
  legalAcceptances: RegistrationLegalAcceptance[] = [];

  get loginUrl(): string {
    if (this.account !== 'student') return 'https://onuraltintas.net/staff/?product=' + this.product;
    return this.product === 'coaching' ? 'https://eduivme.com/auth/login' : 'https://masterhizliokuma.com/auth/login';
  }

  submit(): void {
    if (this.loading || this.joined || this.form.invalid || !this.legalReady || this.legalAcceptances.length !== 3) return;
    this.loading = true;
    this.error = '';
    this.http.post(`${environment.apiUrl}/auth/${this.product}/register/join`, {
      ...this.form.getRawValue(), account: this.account, legalAcceptances: this.legalAcceptances
    }).subscribe({
      next: () => { this.joined = true; this.loading = false; },
      error: (err) => {
        this.loading = false;
        const detail = err.error?.description ?? err.error?.message ?? err.error?.Error?.description;
        this.error = typeof detail === 'string' ? detail : 'Katılım tamamlanamadı. Bilgilerini kontrol edip tekrar dene.';
      }
    });
  }
}
