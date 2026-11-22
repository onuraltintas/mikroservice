import { Component, EventEmitter, Input, Output, OnInit, inject, signal } from '@angular/core';
import { roleName } from '../../../../core/services/user-product-roles';
import { CommonModule } from '@angular/common';
import { IdentityService, UserProfileDto, UserSessionDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { AuthService, hasRequiredAccess } from '../../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../../core/auth/permissions';
import { getAdminErrorMessage } from '../../../../core/auth/admin-error-message';

@Component({
  selector: 'app-user-details-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="fixed inset-0 z-50 overflow-y-auto" aria-labelledby="modal-title" role="dialog" aria-modal="true">
      <!-- Backdrop (Modern Glass Effect) -->
      <div class="fixed inset-0 bg-(--ui-text) backdrop-blur-sm transition-opacity" (click)="closeModal()"></div>

      <div class="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
        <div class="relative transform overflow-hidden rounded-2xl bg-(--ui-surface) text-left shadow-2xl transition-all sm:my-8 sm:w-full sm:max-w-2xl">
          
          <!-- Header -->
          <div class="px-6 py-4 border-b border-(--ui-border) flex justify-between items-center bg-(--ui-surface-muted)">
            <h3 class="text-lg font-bold text-(--ui-text)">Kullanıcı detayları</h3>
            <button (click)="closeModal()" class="text-(--ui-text-muted) hover:text-(--ui-text-muted) transition-colors">
              <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div class="px-6 py-6">
            @if (loading()) {
              <div class="flex flex-col items-center justify-center py-12">
                <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-(--ui-brand)"></div>
                <p class="mt-4 text-(--ui-text-muted)">Bilgiler yükleniyor...</p>
              </div>
            } @else if (user()) {
              <div class="space-y-8">
                <!-- Profile Header -->
                <div class="flex items-center space-x-6">
                  <div class="relative">
                    <div class="h-24 w-24 rounded-full bg-(--ui-brand-soft) flex items-center justify-center text-(--ui-brand-text) text-3xl font-bold border-4 border-white shadow-md">
                      @if (user()?.avatarUrl) {
                        <img [src]="user()?.avatarUrl" [alt]="user()?.fullName || 'Kullanıcı avatarı'" class="h-full w-full rounded-full object-cover">
                      } @else {
                        {{ user()?.firstName?.charAt(0) }}{{ user()?.lastName?.charAt(0) }}
                      }
                    </div>
                    <span [class]="user()?.isActive ? 'bg-(--ui-success)' : 'bg-(--ui-danger)'" 
                      class="absolute bottom-1 right-1 block h-5 w-5 rounded-full border-2 border-white shadow-sm"
                      [title]="user()?.isActive ? 'Aktif' : 'Pasif'">
                    </span>
                  </div>
                  <div>
                    <h4 class="text-2xl font-bold text-(--ui-text)">{{ user()?.fullName }}</h4>
                    <div class="flex items-center mt-1 space-x-2">
                      <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-(--ui-brand-soft) text-(--ui-brand-text) shadow-sm">
                        {{ user()?.role }}
                      </span>
                      @if (user()?.emailConfirmed) {
                        <span class="inline-flex items-center text-xs text-(--ui-success) font-medium">
                          <svg class="w-4 h-4 mr-1" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/></svg>
                          E-posta doğrulandı
                        </span>
                      }
                    </div>
                  </div>
                </div>

                <!-- Info Grid -->
                <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div class="p-4 rounded-xl bg-(--ui-surface-muted) border border-(--ui-border)">
                    <p class="text-sm font-semibold text-(--ui-text-muted) mb-1">E-posta</p>
                    <p class="text-(--ui-text) font-medium break-all">{{ user()?.email }}</p>
                  </div>
                  <div class="p-4 rounded-xl bg-(--ui-surface-muted) border border-(--ui-border)">
                    <p class="text-sm font-semibold text-(--ui-text-muted) mb-1">Telefon</p>
                    <p class="text-(--ui-text) font-medium">{{ user()?.phoneNumber || '-' }}</p>
                  </div>
                  <div class="p-4 rounded-xl bg-(--ui-surface-muted) border border-(--ui-border)">
                    <p class="text-sm font-semibold text-(--ui-text-muted) mb-1">Son giriş</p>
                    <p class="text-(--ui-text) font-medium">{{ user()?.lastLoginAt ? (user()?.lastLoginAt | date:'dd.MM.yyyy HH:mm') : 'Henüz giriş yapmadı' }}</p>
                  </div>
                  <div class="p-4 rounded-xl bg-(--ui-surface-muted) border border-(--ui-border)">
                    <p class="text-sm font-semibold text-(--ui-text-muted) mb-1">Kimlik numarası</p>
                    <p class="flex items-center gap-2 text-(--ui-text) font-mono text-xs break-all" data-testid="user-id">{{ userId }}
                      <button type="button" class="shrink-0 rounded-md border border-(--ui-border) px-2 py-0.5 font-sans text-xs" (click)="copyId()">{{ idCopied() ? 'Kopyalandı' : 'Kopyala' }}</button>
                    </p>
                  </div>
                </div>

                <div class="pt-6 border-t border-(--ui-border)">
                  <h5 class="text-sm font-bold text-(--ui-text) mb-3">Platform erişimleri</h5>
                  <div class="flex flex-wrap gap-2">
                    @for (access of user()?.productAccesses; track access.product) {
                      <span class="inline-flex items-center gap-2 rounded-lg border px-3 py-1.5 text-xs font-semibold"
                        [ngClass]="access.isActive ? 'border-(--ui-success-soft) bg-(--ui-success-soft) text-(--ui-success)' : 'border-(--ui-border) bg-(--ui-surface-muted) text-(--ui-text-muted)'">
                        {{ access.product === 'coaching' ? 'Koçluk' : access.product === 'speed-reading' ? 'Hızlı Okuma' : access.product }}
                        <span>{{ access.isActive ? 'Aktif' : 'Pasif' }}</span>
                      </span>
                    } @empty {
                      <span class="text-sm text-(--ui-text-muted)">Platform erişimi tanımlanmamış.</span>
                    }
                  </div>
                </div>

                @if (hasAccessPermission() && !isMfaVerified()) {
                <div class="rounded-xl border border-(--ui-warning-soft) bg-(--ui-warning-soft) px-4 py-3 text-sm text-(--ui-warning)">
                  MFA doğrulaması gerekiyor. Bu oturumda erişim yönetimi işlemleri kullanılamaz.
                </div>
                }

                @if (canManageAccess()) {
                <!-- Access Management -->
                <div class="pt-6 border-t border-(--ui-border)">
                  <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                      <h5 class="text-sm font-bold text-(--ui-text)">Erişim yönetimi</h5>
                      <p class="mt-1 text-xs text-(--ui-text-muted)">Refresh token değerleri gösterilmez; yalnızca oturum metadata'sı görüntülenir.</p>
                    </div>
                    <div class="flex flex-wrap gap-2">
                      <button type="button" (click)="revokeAllSessions()" [disabled]="sessions().length === 0"
                        class="rounded-lg border border-(--ui-warning) px-3 py-2 text-xs font-semibold text-(--ui-warning) hover:bg-(--ui-warning-soft) disabled:cursor-not-allowed disabled:opacity-50">
                        Tüm oturumları sonlandır
                      </button>
                      <button type="button" (click)="resetMfa()"
                        class="rounded-lg border border-(--ui-danger) px-3 py-2 text-xs font-semibold text-(--ui-danger) hover:bg-(--ui-danger-soft)">
                        MFA'yı Sıfırla
                      </button>
                    </div>
                  </div>

                  @if (sessionsLoading()) {
                    <p class="mt-4 text-sm text-(--ui-text-muted)">Oturumlar yükleniyor...</p>
                  } @else if (sessions().length === 0) {
                    <p class="mt-4 rounded-lg bg-(--ui-surface-muted) px-4 py-3 text-sm text-(--ui-text-muted)">Aktif oturum bulunmuyor.</p>
                  } @else {
                    <div class="mt-4 space-y-2">
                      @for (session of sessions(); track session.id) {
                        <div class="flex flex-col gap-2 rounded-lg border border-(--ui-border) px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
                          <div class="text-xs text-(--ui-text-muted)">
                            <span class="font-semibold">{{ session.createdByIp || 'IP bilinmiyor' }}</span>
                            <span class="mx-2 text-(--ui-text-muted)">•</span>
                            Başlangıç: {{ session.createdAt | date:'dd.MM.yyyy HH:mm' }}
                            <span class="mx-2 text-(--ui-text-muted)">•</span>
                            Bitiş: {{ session.expiresAt | date:'dd.MM.yyyy HH:mm' }}
                            @if (session.mfaVerifiedAt) { <span class="ml-2 text-(--ui-success)">MFA doğrulandı</span> }
                          </div>
                          <button type="button" (click)="revokeSession(session)" class="self-start rounded-md px-2 py-1 text-xs font-semibold text-(--ui-danger) hover:bg-(--ui-danger-soft) sm:self-auto">
                            Sonlandır
                          </button>
                        </div>
                      }
                      <span class="inline-flex items-center text-xs font-medium" [class.text-(--ui-success)]="user()?.mfaEnabled" [class.text-(--ui-warning)]="!user()?.mfaEnabled">
                        {{ user()?.mfaEnabled ? 'MFA etkin' : 'MFA kurulmamış' }}
                      </span>
                    </div>
                  }
                </div>
                }

                <!-- Roles & Permissions -->
                <div class="grid grid-cols-1 md:grid-cols-2 gap-6 pt-6 border-t border-(--ui-border)">
                  <div>
                    <h5 class="text-sm font-bold text-(--ui-text) mb-3">Roller</h5>
                    <div class="flex flex-wrap gap-2">
                      @for (role of user()?.productRoles; track role.role + ':' + (role.product || 'global')) {
                        <span class="px-3 py-1 bg-(--ui-brand-soft) text-(--ui-brand-text) rounded-lg text-xs font-bold border border-(--ui-brand-soft) shadow-sm">
                          {{ roleText(role.role) }}, {{ productLabel(role.product) }}
                        </span>
                      } @empty {
                        @for (role of user()?.roles; track role) {
                          <span class="px-3 py-1 bg-(--ui-brand-soft) text-(--ui-brand-text) rounded-lg text-xs font-bold border border-(--ui-brand-soft) shadow-sm">
                            {{ role }}
                          </span>
                        }
                      }
                    </div>
                  </div>
                  <div>
                    <h5 class="text-sm font-bold text-(--ui-text) mb-3">İzinler</h5>
                    <div class="flex flex-wrap gap-2 max-h-40 overflow-y-auto pr-2 custom-scrollbar">
                      @for (perm of user()?.permissions; track perm) {
                        <span class="px-2 py-0.5 bg-(--ui-surface-muted) text-(--ui-text-muted) rounded-md text-[10px] border border-(--ui-border)">
                          {{ perm.replace('Permissions.', '') }}
                        </span>
                      }
                    </div>
                  </div>
                </div>

                <!-- Role Specific Details -->
                @if (user()?.teacherDetails) {
                  <div class="space-y-4 pt-4 border-t border-(--ui-border)">
                    <h5 class="text-lg font-bold text-(--ui-text) flex items-center">
                      <svg class="w-5 h-5 mr-2 text-(--ui-brand-text)" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path d="M12 14l9-5-9-5-9 5 9 5z"/><path d="M12 14l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0012 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 14l9-5-9-5-9 5 9 5zm0 0l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0012 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14zm-4 6v-7.5l4-2.222" /></svg>
                      Öğretmen bilgileri
                    </h5>
                    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                      <div>
                        <p class="text-xs font-semibold text-(--ui-text-muted)">Ünvan</p>
                        <p class="text-(--ui-text)">{{ user()?.teacherDetails?.title || '-' }}</p>
                      </div>
                      <div>
                        <p class="text-xs font-semibold text-(--ui-text-muted)">Kurum</p>
                        <p class="text-(--ui-text)">{{ user()?.teacherDetails?.institutionName || 'Serbest Çalışan' }}</p>
                      </div>
                    </div>
                    <div>
                      <p class="text-xs font-semibold text-(--ui-text-muted) mb-2">Uzmanlık alanları</p>
                      <div class="flex flex-wrap gap-2">
                        @for (subject of user()?.teacherDetails?.subjects; track subject) {
                          <span class="px-3 py-1 bg-(--ui-surface-muted) text-(--ui-text) rounded-lg text-sm border border-(--ui-border) shadow-sm">
                            {{ subject }}
                          </span>
                        } @empty {
                          <p class="text-(--ui-text-muted) italic text-sm">Belirtilmemiş</p>
                        }
                      </div>
                    </div>
                  </div>
                }

                @if (user()?.studentDetails) {
                  <div class="space-y-4 pt-4 border-t border-(--ui-border)">
                    <h5 class="text-lg font-bold text-(--ui-text) flex items-center">
                      <svg class="w-5 h-5 mr-2 text-(--ui-brand-text)" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" /></svg>
                      Öğrenci bilgileri
                    </h5>
                    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                      <div>
                        <p class="text-xs font-semibold text-(--ui-text-muted)">Sınıf seviyesi</p>
                        <p class="text-(--ui-text)">
                          @if (user()?.studentDetails?.gradeLevel; as gradeLevel) {
                            {{ gradeLevel }}. Sınıf
                          } @else {
                            Belirtilmemiş
                          }
                        </p>
                      </div>
                      <div>
                        <p class="text-xs font-semibold text-(--ui-text-muted)">Öğrenme stili</p>
                        <p class="text-(--ui-text)">{{ user()?.studentDetails?.learningStyle || '-' }}</p>
                      </div>
                      <div>
                        <p class="text-xs font-semibold text-(--ui-text-muted)">Doğum tarihi</p>
                        <p class="text-(--ui-text)">{{ user()?.studentDetails?.birthDate | date:'dd.MM.yyyy' }}</p>
                      </div>
                      <div>
                        <p class="text-xs font-semibold text-(--ui-text-muted)">Kurum</p>
                        <p class="text-(--ui-text)">{{ user()?.studentDetails?.institutionName || '-' }}</p>
                      </div>
                    </div>
                  </div>
                }
              </div>
            } @else if (error()) {
              <div class="p-4 bg-(--ui-danger-soft) text-(--ui-danger) rounded-xl border border-(--ui-danger-soft) text-center py-12">
                <svg class="h-12 w-12 mx-auto text-(--ui-danger) mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
                <p class="font-bold">Hata oluştu</p>
                <p>{{ error() }}</p>
                <button (click)="loadUserDetails()" class="mt-4 px-4 py-2 bg-(--ui-brand) text-(--ui-brand-contrast) rounded-lg hover:bg-(--ui-brand) transition-colors">Tekrar dene</button>
              </div>
            }
          </div>

          <!-- Footer -->
          <div class="px-6 py-4 bg-(--ui-surface-muted) flex justify-end">
            <button (click)="closeModal()" class="px-6 py-2 bg-(--ui-surface) border border-(--ui-border) text-(--ui-text) rounded-xl hover:bg-(--ui-surface-muted) transition-all shadow-sm">
              Kapat
            </button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class UserDetailsModalComponent implements OnInit {
  @Input({ required: true }) userId!: string;
  @Output() close = new EventEmitter<void>();

  private identityService = inject(IdentityService);
  private toaster = inject(ToasterService);
  private auth = inject(AuthService);

  user = signal<UserProfileDto | null>(null);
  /** The id is what "Abonelik ve Ödeme › Elle abonelik ver" asks for. */
  readonly idCopied = signal(false);
  copyId(): void {
    void navigator.clipboard?.writeText(this.userId).then(() => this.idCopied.set(true), () => undefined);
  }
  sessions = signal<UserSessionDto[]>([]);
  sessionsLoading = signal(false);
  loading = signal(true);
  error = signal<string | null>(null);

  ngOnInit() {
    this.loadUserDetails();
  }

  loadUserDetails() {
    this.loading.set(true);
    this.error.set(null);

    this.identityService.getUserById(this.userId).subscribe({
      next: (res) => {
        this.user.set(res);
        this.loading.set(false);
        if (this.canManageAccess()) this.loadSessions();
      },
      error: (err) => {
        console.error('User detail error:', err);
        this.error.set('Kullanıcı bilgileri yüklenemedi. Sayfayı yenileyip yeniden deneyin.');
        this.loading.set(false);
        this.toaster.error('Bilgiler alınırken bir hata oluştu.');
      }
    });
  }

  loadSessions() {
    if (!this.canManageAccess()) return;
    this.sessionsLoading.set(true);
    this.identityService.getUserSessions(this.userId).subscribe({
      next: (sessions) => {
        this.sessions.set(sessions);
        this.sessionsLoading.set(false);
      },
      error: () => {
        this.sessions.set([]);
        this.sessionsLoading.set(false);
        this.toaster.error('Aktif oturumlar yüklenemedi. Sayfayı yenileyip yeniden deneyin.');
      }
    });
  }

  async revokeSession(session: UserSessionDto) {
    if (!this.canManageAccess()) return;
    const confirmed = await this.toaster.confirm(
      'Bu oturumu sonlandırmak istediğinize emin misiniz?',
      { title: 'Oturumu sonlandır', confirmText: 'Evet, sonlandır' });
    if (!confirmed) return;

    this.identityService.revokeUserSession(this.userId, session.id).subscribe({
      next: () => {
        this.sessions.update(current => current.filter(item => item.id !== session.id));
        this.toaster.success('Oturum sonlandırıldı.');
      },
      error: err => this.toaster.error(getAdminErrorMessage(err, 'Oturum sonlandırılamadı.', true))
    });
  }

  async revokeAllSessions() {
    if (!this.canManageAccess()) return;
    const confirmed = await this.toaster.confirm(
      'Kullanıcının tüm aktif oturumları kapatılacak. Devam etmek istiyor musunuz?',
      { title: 'Tüm oturumları sonlandır', confirmText: 'Evet, hepsini kapat' });
    if (!confirmed) return;

    this.identityService.revokeAllUserSessions(this.userId).subscribe({
      next: () => {
        this.sessions.set([]);
        this.toaster.success('Tüm oturumlar sonlandırıldı.');
      },
      error: err => this.toaster.error(getAdminErrorMessage(err, 'Oturumlar sonlandırılamadı.', true))
    });
  }

  async resetMfa() {
    if (!this.canManageAccess()) return;
    const confirmed = await this.toaster.confirm(
      'MFA kurulumu ve kurtarma kodları silinecek, tüm oturumlar kapatılacak.',
      { title: 'MFA ayarını sıfırla', confirmText: 'Evet, MFA sıfırla' });
    if (!confirmed) return;

    this.identityService.resetUserMfa(this.userId).subscribe({
      next: () => {
        this.sessions.set([]);
        this.toaster.success('MFA sıfırlandı; kullanıcı yeniden kurulum yapmalıdır.');
      },
      error: err => this.toaster.error(getAdminErrorMessage(err, 'MFA sıfırlanamadı.', true))
    });
  }

  hasAccessPermission(): boolean {
    return hasRequiredAccess(
      this.auth.userProfile(),
      ADMIN_PERMISSIONS.usersEdit,
      'SystemAdmin'
    );
  }

  isMfaVerified(): boolean {
    return this.auth.userProfile()?.mfaVerified === true;
  }

  canManageAccess(): boolean {
    return this.hasAccessPermission() && this.isMfaVerified();
  }

  readonly roleText = roleName;

  productLabel(product: string | null | undefined): string {
    return product === 'coaching' ? 'Koçluk' : product === 'speed-reading' ? 'Hızlı Okuma' : 'Genel';
  }

  closeModal() {
    this.close.emit();
  }
}
