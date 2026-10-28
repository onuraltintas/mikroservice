import { ChangeDetectorRef, Component, Input, inject } from '@angular/core';
import { finalize } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { RegistrationLegalConsentComponent } from './registration-legal-consent.component';
import { RegistrationLegalAcceptance } from '../../core/services/identity.service';
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
            <input matInput type="email" autocomplete="username" formControlName="email" [readonly]="loading">
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Mevcut şifre</mat-label>
            <input matInput type="password" autocomplete="current-password" formControlName="password" [readonly]="loading">
          </mat-form-field>
          @if (account === 'institution' && institutions.length) {
            <mat-form-field appearance="outline">
              <mat-label>Katılınacak kurum</mat-label>
              <select matNativeControl aria-label="Katılınacak kurum" [value]="selectedInstitutionId"
                (change)="selectedInstitutionId = $any($event.target).value" [disabled]="loading">
                <option value="" disabled>Kurum seçin</option>
                @for (institution of institutions; track institution.id) {
                  <option [value]="institution.id">{{ institution.name }} · {{ institution.role === 'Owner' ? 'Kurum sahibi' : institution.role === 'Admin' ? 'Yönetici' : 'Yetkili' }}</option>
                }
              </select>
              <mat-hint>Yalnız seçilen kurum bu servise bağlanır. Diğer bağlantılarınız korunur.</mat-hint>
            </mat-form-field>
          }
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
  @Input() product: 'coaching' | 'speed-reading' = 'coaching';
  @Input() account: 'student' | 'teacher' | 'institution' = 'student';
  private readonly http = inject(HttpClient);
  private readonly changeDetector = inject(ChangeDetectorRef);
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
  institutions: { id: string; name: string; role: string }[] = [];
  selectedInstitutionId = '';

  constructor() {
    for (const control of [this.form.controls.email, this.form.controls.password]) {
      control.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.clearInstitutions());
    }
  }

  private clearInstitutions(): void {
    this.institutions = [];
    this.selectedInstitutionId = '';
  }

  get loginUrl(): string {
    if (this.account !== 'student') return `${environment.staffPortalUrl}?product=${this.product}`;
    return this.product === 'coaching' ? 'https://eduivme.com/auth/login' : 'https://masterhizliokuma.com/auth/login';
  }

  submit(): void {
    if (this.loading || this.joined || this.form.invalid || !this.legalReady || this.legalAcceptances.length !== 3) return;
    if (this.account === 'institution' && this.institutions.length && !this.selectedInstitutionId) {
      this.error = 'Listeden katılacağınız kurumu seçin.';
      return;
    }
    this.loading = true;
    this.error = '';
    this.http.post(`${environment.apiUrl}/auth/${this.product}/register/join`, {
      ...this.form.getRawValue(), account: this.account, legalAcceptances: this.legalAcceptances,
      ...(this.account === 'institution' && this.selectedInstitutionId ? { institutionId: this.selectedInstitutionId } : {})
    }).pipe(finalize(() => this.changeDetector.markForCheck())).subscribe({
      next: () => { this.joined = true; this.loading = false; },
      error: (err) => {
        this.loading = false;
        if (this.account === 'institution' && err.error?.code === 'Auth.InstitutionSelectionRequired'
            && Array.isArray(err.error?.institutions)) {
          this.institutions = err.error.institutions.filter((item: any) =>
            typeof item?.id === 'string' && typeof item?.name === 'string' && typeof item?.role === 'string');
          this.selectedInstitutionId = '';
          this.error = this.institutions.length ? 'Katılacağınız kurumu listeden seçin.' : 'Kurum listesi yüklenemedi. Tekrar deneyin.';
          return;
        }
        if (err.error?.code === 'Auth.InvalidInstitutionSelection') this.clearInstitutions();
        const detail = err.error?.description ?? err.error?.message ?? err.error?.Error?.description;
        this.error = typeof detail === 'string' ? detail : 'Katılım tamamlanamadı. Bilgilerini kontrol edip tekrar dene.';
      }
    });
  }
}
