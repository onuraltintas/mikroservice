import { ChangeDetectorRef, Component, ElementRef, HostListener, ViewChild, inject, signal } from '@angular/core';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { DarkModeService } from '../../../../src/app/core/services/dark-mode.service';
import { CoachingIconComponent } from '../../../../src/app/features/coaching-portal/coaching-icon.component';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { StaffAuthService } from './auth/staff-auth.service';
import { StaffProduct, StaffProductAccess } from './auth/staff-auth.models';
import { CoachingTeacherWorkspaceComponent } from './features/coaching/coaching-teacher-workspace.component';
import { CoachingInstitutionWorkspaceComponent } from './features/coaching/coaching-institution-workspace.component';
import { SpeedReadingTeacherWorkspaceComponent } from './features/speed-reading/speed-reading-teacher-workspace.component';
import { SpeedReadingInstitutionWorkspaceComponent } from './features/speed-reading/speed-reading-institution-workspace.component';
import { StaffAccountSettingsComponent } from './account/staff-account-settings.component';
import { StaffNotificationCenterComponent } from './notifications/staff-notification-center.component';
import type { WeeklyDestination } from '../../../../shared/coaching-notification-target';
import { CourseManagementWorkspaceComponent } from './features/course-management/course-management-workspace.component';
import { CourseManagementEnrollmentComponent } from './features/course-management/course-management-enrollment.component';

type StaffWorkspaceRole = 'teacher' | 'institution';

@Component({
  selector: 'staff-root',
  standalone: true,
  imports: [FormsModule, CdkTrapFocus, CoachingIconComponent, CoachingTeacherWorkspaceComponent, CoachingInstitutionWorkspaceComponent, SpeedReadingTeacherWorkspaceComponent, SpeedReadingInstitutionWorkspaceComponent, StaffAccountSettingsComponent, StaffNotificationCenterComponent, CourseManagementWorkspaceComponent, CourseManagementEnrollmentComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  @ViewChild('coachingTeacherWorkspace') coachingTeacherWorkspace?: CoachingTeacherWorkspaceComponent;
  @ViewChild(CoachingInstitutionWorkspaceComponent) coachingInstitutionWorkspace?: Pick<CoachingInstitutionWorkspaceComponent, 'canLeavePage'>;
  private async canLeaveCoachingWorkspace(): Promise<boolean> {
    return (await this.coachingTeacherWorkspace?.canLeavePage() ?? true) && (await this.coachingInstitutionWorkspace?.canLeavePage() ?? true);
  }
  readonly auth = inject(StaffAuthService);
  readonly theme = inject(DarkModeService);
  readonly menuOpen = signal(false);
  @ViewChild('teacherMenuToggle') private teacherMenuToggle?: ElementRef<HTMLButtonElement>;
  closeTeacherMenu(): void { this.menuOpen.set(false); this.teacherMenuToggle?.nativeElement.focus(); }
  @HostListener('document:keydown.escape') closeTeacherMenuOnEscape(): void { if (this.menuOpen()) this.closeTeacherMenu(); }
  async chooseProductFromMenu(product: StaffProduct): Promise<void> { this.closeTeacherMenu(); await this.switchProduct(product); }
  private readonly document = inject(DOCUMENT);
  private readonly changeDetector = inject(ChangeDetectorRef);
  readonly currentYear = new Date().getFullYear();
  readonly hasCourseAccess = (access: StaffProductAccess): boolean => access.product === 'course-management';
  selectedProduct: StaffProduct = this.initialProduct();

  private initialProduct(): StaffProduct {
    const product = new URLSearchParams(this.document.defaultView?.location.search ?? '').get('product');
    return product === 'speed-reading' || product === 'course-management' ? product : 'coaching';
  }
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
  accountSettingsVisible = false;
  notificationsVisible = false;
  async openCoachingAssignment(id: string): Promise<void> {
    if (this.isTeacherWorkspaceSelected && await this.coachingTeacherWorkspace?.openAssignmentFromNotification(id)) {
      this.notificationsVisible = false;
    }
  }
  async openCoachingWeekly(target: WeeklyDestination): Promise<void> {
    if (this.isTeacherWorkspaceSelected && await this.coachingTeacherWorkspace?.openWeeklyFromNotification(target)) this.notificationsVisible = false;
  }
  errorMessage = '';
  switchError = '';
  private readonly selectedWorkspaceRoles: Partial<Record<StaffProduct, StaffWorkspaceRole>> = {};

  get activeAccess() {
    return this.auth.products().find(access => access.product === this.auth.activeProduct()) ?? null;
  }

  get isInstitutionManager(): boolean {
    return this.activeAccess?.roles.some(role =>
      role === 'InstitutionAdmin' || role === 'InstitutionOwner'
        || this.auth.activeProduct() === 'course-management' && role === 'CourseInstitutionOwner') ?? false;
  }

  get hasTeacherRole(): boolean {
    return this.activeAccess?.roles.some(role => role === 'Teacher'
      || this.auth.activeProduct() === 'course-management' && role === 'CourseTeacher') ?? false;
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

  async selectWorkspaceRole(role: StaffWorkspaceRole): Promise<void> {
    if (role === 'teacher' && !this.hasTeacherRole) return;
    if (role === 'institution' && !this.isInstitutionManager) return;
    if (role !== this.activeWorkspaceRole && !await this.canLeaveCoachingWorkspace()) return;
    const product = this.auth.activeProduct();
    if (product) this.selectedWorkspaceRoles[product] = role;
  }

  get currentProductLabel(): string {
    return this.productLabel(this.auth.activeProduct());
  }

  productLabel(product: StaffProduct | null): string {
    if (product === 'course-management') return 'Kurs Yönetimi';
    return product === 'speed-reading' ? 'Hızlı Okuma' : 'Koçluk';
  }

  roleLabel(roles: string[]): string {
    if (roles.some(role => role === 'InstitutionAdmin' || role === 'InstitutionOwner' || role === 'CourseInstitutionOwner')) {
      return 'Kurum yöneticisi';
    }
    return roles.some(role => role === 'Teacher' || role === 'CourseTeacher') ? 'Öğretmen' : 'Personel';
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
          const setup = await this.auth.startMfaSetup(this.mfaChallengeToken, this.selectedProduct);
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
      this.changeDetector.markForCheck();
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
      this.changeDetector.markForCheck();
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
      this.changeDetector.markForCheck();
    }
  }

  finishMfaEnrollment(): void {
    this.clearMfaState();
  }

  async switchProduct(product: StaffProduct): Promise<void> {
    if (product === this.auth.activeProduct()) return;
    if (!await this.canLeaveCoachingWorkspace()) return;
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
      this.changeDetector.markForCheck();
    }
  }

  async logout(): Promise<void> {
    if (!await this.canLeaveCoachingWorkspace()) return;
    await this.auth.logout();
    this.accountSettingsVisible = false;
    this.notificationsVisible = false;
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
