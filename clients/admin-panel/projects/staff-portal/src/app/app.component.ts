import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { StaffAuthService } from './auth/staff-auth.service';
import { StaffProduct } from './auth/staff-auth.models';
import { CoachingTeacherWorkspaceComponent } from './features/coaching/coaching-teacher-workspace.component';
import { CoachingInstitutionWorkspaceComponent } from './features/coaching/coaching-institution-workspace.component';
import { SpeedReadingTeacherWorkspaceComponent } from './features/speed-reading/speed-reading-teacher-workspace.component';
import { SpeedReadingInstitutionWorkspaceComponent } from './features/speed-reading/speed-reading-institution-workspace.component';

type StaffWorkspaceRole = 'teacher' | 'institution';

@Component({
  selector: 'staff-root',
  standalone: true,
  imports: [FormsModule, CoachingTeacherWorkspaceComponent, CoachingInstitutionWorkspaceComponent, SpeedReadingTeacherWorkspaceComponent, SpeedReadingInstitutionWorkspaceComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  readonly auth = inject(StaffAuthService);
  readonly currentYear = new Date().getFullYear();
  selectedProduct: StaffProduct = 'coaching';
  email = '';
  password = '';
  rememberMe = false;
  mfaCode = '';
  mfaRecoveryCode = '';
  mfaChallengeToken: string | null = null;
  mfaSetupToken = '';
  mfaSecret = '';
  mfaOtpAuthUri = '';
  mfaStage: 'none' | 'setup' | 'verify' | 'recovery' = 'none';
  recoveryCodes: string[] = [];
  isSubmitting = false;
  errorMessage = '';
  switchError = '';
  private readonly selectedWorkspaceRoles: Partial<Record<StaffProduct, StaffWorkspaceRole>> = {};

  get activeAccess() {
    return this.auth.products().find(access => access.product === this.auth.activeProduct()) ?? null;
  }

  get isInstitutionManager(): boolean {
    return this.activeAccess?.roles.some(role =>
      role === 'InstitutionAdmin' || role === 'InstitutionOwner') ?? false;
  }

  get hasTeacherRole(): boolean {
    return this.activeAccess?.roles.includes('Teacher') ?? false;
  }

  get canSwitchWorkspaceRole(): boolean {
    return this.hasTeacherRole && this.isInstitutionManager;
  }

  get activeWorkspaceRole(): StaffWorkspaceRole | null {
    const selectedRole = this.selectedWorkspaceRoles[this.auth.activeProduct() ?? 'coaching'];
    if (selectedRole === 'teacher' && this.hasTeacherRole) return selectedRole;
    if (selectedRole === 'institution' && this.isInstitutionManager) return selectedRole;
    if (this.isInstitutionManager) return 'institution';
    return this.hasTeacherRole ? 'teacher' : null;
  }

  get isTeacherWorkspaceSelected(): boolean {
    return this.auth.activeProduct() === 'coaching' && this.activeWorkspaceRole === 'teacher';
  }

  get isInstitutionWorkspaceSelected(): boolean {
    return this.auth.activeProduct() === 'coaching' && this.activeWorkspaceRole === 'institution';
  }

  get isSpeedReadingTeacherWorkspaceSelected(): boolean {
    return this.auth.activeProduct() === 'speed-reading' && this.activeWorkspaceRole === 'teacher';
  }

  get isSpeedReadingInstitutionWorkspaceSelected(): boolean {
    return this.auth.activeProduct() === 'speed-reading' && this.activeWorkspaceRole === 'institution';
  }

  selectWorkspaceRole(role: StaffWorkspaceRole): void {
    if (role === 'teacher' && !this.hasTeacherRole) return;
    if (role === 'institution' && !this.isInstitutionManager) return;
    const product = this.auth.activeProduct();
    if (product) this.selectedWorkspaceRoles[product] = role;
  }

  get currentProductLabel(): string {
    return this.productLabel(this.auth.activeProduct());
  }

  productLabel(product: StaffProduct | null): string {
    return product === 'speed-reading' ? 'Hızlı Okuma' : 'Koçluk';
  }

  roleLabel(roles: string[]): string {
    if (roles.some(role => role === 'InstitutionAdmin' || role === 'InstitutionOwner')) {
      return 'Kurum yöneticisi';
    }
    return roles.includes('Teacher') ? 'Öğretmen' : 'Personel';
  }

  async submitLogin(): Promise<void> {
    if (this.isSubmitting) return;
    this.isSubmitting = true;
    this.errorMessage = '';
    try {
      const result = await this.auth.login(
        this.selectedProduct,
        this.email.trim(),
        this.password,
        this.rememberMe
      );
      if (result.requiresMfa) {
        this.password = '';
        if (!result.mfaChallengeToken) {
          this.errorMessage = 'İki adımlı doğrulama başlatılamadı. Lütfen yeniden giriş yapın.';
          return;
        }

        this.mfaChallengeToken = result.mfaChallengeToken;
        if (result.mfaEnrollmentRequired) {
          const setup = await this.auth.startMfaSetup(this.mfaChallengeToken);
          this.mfaSecret = setup.secret;
          this.mfaOtpAuthUri = setup.otpAuthUri;
          this.mfaSetupToken = setup.setupToken;
          this.mfaStage = 'setup';
        } else {
          this.mfaStage = 'verify';
        }
      }
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error);
    } finally {
      this.isSubmitting = false;
    }
  }

  async submitMfa(): Promise<void> {
    if (this.isSubmitting || !this.mfaChallengeToken || this.mfaCode.trim().length !== 6) return;
    this.isSubmitting = true;
    this.errorMessage = '';
    try {
      if (this.mfaStage === 'setup') {
        this.recoveryCodes = await this.auth.enableMfa(
          this.selectedProduct,
          this.mfaChallengeToken,
          this.mfaSetupToken,
          this.mfaCode.trim()
        );
        this.mfaCode = '';
        this.mfaStage = 'recovery';
      } else {
        await this.auth.verifyMfa(this.selectedProduct, this.mfaChallengeToken, this.mfaCode.trim());
        this.clearMfaState();
      }
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error);
    } finally {
      this.isSubmitting = false;
    }
  }

  cancelMfa(): void {
    this.clearMfaState();
    this.errorMessage = '';
  }

  async submitRecoveryCode(): Promise<void> {
    if (this.isSubmitting || !this.mfaChallengeToken || !this.mfaRecoveryCode.trim()) return;
    this.isSubmitting = true;
    this.errorMessage = '';
    try {
      await this.auth.verifyMfa(
        this.selectedProduct,
        this.mfaChallengeToken,
        null,
        this.mfaRecoveryCode.trim()
      );
      this.clearMfaState();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error);
    } finally {
      this.isSubmitting = false;
    }
  }

  finishMfaEnrollment(): void {
    this.clearMfaState();
  }

  async switchProduct(product: StaffProduct): Promise<void> {
    if (product === this.auth.activeProduct()) return;
    this.switchError = '';
    try {
      await this.auth.switchProduct(product);
    } catch (error) {
      const message = this.getErrorMessage(error);
      const code = error instanceof HttpErrorResponse
        ? (error.error as { code?: string } | null)?.code
        : undefined;
      this.switchError = code === 'Auth.MfaRequired' || message.toLocaleLowerCase('tr').includes('iki adımlı')
        ? 'Bu kurum hesabı için yeniden giriş yapıp iki adımlı doğrulamayı tamamlayın. Açık oturumunuz korunuyor.'
        : message;
    }
  }

  async logout(): Promise<void> {
    await this.auth.logout();
    this.email = '';
    this.password = '';
    this.rememberMe = false;
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const payload = error.error as { description?: string; message?: string; detail?: string } | null;
      return payload?.description ?? payload?.message ?? payload?.detail
        ?? 'İşlem tamamlanamadı. Bilgilerinizi kontrol edip tekrar deneyin.';
    }
    return error instanceof Error
      ? error.message
      : 'İşlem tamamlanamadı. Bilgilerinizi kontrol edip tekrar deneyin.';
  }

  private clearMfaState(): void {
    this.mfaChallengeToken = null;
    this.mfaSetupToken = '';
    this.mfaSecret = '';
    this.mfaOtpAuthUri = '';
    this.mfaCode = '';
    this.mfaRecoveryCode = '';
    this.recoveryCodes = [];
    this.mfaStage = 'none';
  }
}
