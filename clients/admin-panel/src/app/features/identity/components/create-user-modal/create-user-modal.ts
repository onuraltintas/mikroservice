import { Component, EventEmitter, OnInit, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { IdentityService } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { getAdminErrorMessage } from '../../../../core/auth/admin-error-message';
import { productRoleLabel } from '../../../../core/services/user-product-roles';

@Component({
  selector: 'app-create-user-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="fixed inset-0 z-[100]" aria-labelledby="modal-title" role="dialog" aria-modal="true">
      <!-- Backdrop (Static) -->
      <div class="fixed inset-0 bg-(--ui-text) backdrop-blur-sm transition-opacity" (click)="closeModal()"></div>

      <!-- Modal Position Container -->
      <div class="fixed inset-0 z-[101] overflow-y-auto">
        <div class="flex min-h-full items-start justify-center p-4 text-center sm:p-12">
            
            <!-- Modal Panel -->
            <div class="relative w-full max-w-lg transform overflow-hidden rounded-2xl bg-(--ui-surface) text-left shadow-2xl transition-all border border-(--ui-border) bg-opacity-100">
                
                <!-- Header -->
                <div class="bg-(--ui-brand-soft) p-6 border-b border-(--ui-border)">
                    <div class="flex items-start justify-between">
                        <div class="flex items-center gap-4">
                            <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-(--ui-brand-soft) text-(--ui-brand-text)">
                                <svg class="h-6 w-6" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z" />
                                </svg>
                            </div>
                            <div>
                                <h3 id="modal-title" class="text-lg font-semibold text-(--ui-text)">
                                    {{ createdUser() ? 'Kullanıcı Oluşturuldu' : 'Yeni Kullanıcı' }}
                                </h3>
                                @if (!createdUser()) {
                                    <p class="text-sm text-(--ui-text-muted)">Sisteme yeni bir kullanıcı ekleyin.</p>
                                }
                            </div>
                        </div>
                        <button (click)="closeModal()" [disabled]="loading()" class="text-(--ui-text-muted) hover:text-(--ui-text-muted)">
                            <span class="sr-only">Kapat</span>
                            <svg class="h-6 w-6" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                            </svg>
                        </button>
                    </div>
                </div>

                <!-- Body -->
                <div class="p-6 space-y-5">
                    @if (!createdUser()) {
                        <form [formGroup]="form">
                            <div class="space-y-4">
                                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                                    <div class="group">
                                        <label class="block text-sm font-medium text-(--ui-text) mb-1.5 ml-0.5">Ad</label>
                                        <input type="text" formControlName="firstName" 
                                            [class.border-(--ui-danger)]="f['firstName'].invalid && f['firstName'].touched"
                                            class="block w-full rounded-lg border-(--ui-border) shadow-sm focus:border-(--ui-brand) focus:ring-(--ui-brand) sm:text-sm p-2.5 transition-colors" placeholder="Ahmet">
                                        @if (f['firstName'].invalid && f['firstName'].touched) {
                                            <p class="mt-1 text-xs text-(--ui-danger) ml-0.5">En az 2 karakter gereklidir.</p>
                                        }
                                    </div>
                                    <div class="group">
                                        <label class="block text-sm font-medium text-(--ui-text) mb-1.5 ml-0.5">Soyad</label>
                                        <input type="text" formControlName="lastName" 
                                            [class.border-(--ui-danger)]="f['lastName'].invalid && f['lastName'].touched"
                                            class="block w-full rounded-lg border-(--ui-border) shadow-sm focus:border-(--ui-brand) focus:ring-(--ui-brand) sm:text-sm p-2.5 transition-colors" placeholder="Yılmaz">
                                        @if (f['lastName'].invalid && f['lastName'].touched) {
                                            <p class="mt-1 text-xs text-(--ui-danger) ml-0.5">En az 2 karakter gereklidir.</p>
                                        }
                                    </div>
                                </div>

                                <div class="group">
                                    <label class="block text-sm font-medium text-(--ui-text) mb-1.5 ml-0.5">E-Posta Adresi</label>
                                    <div class="relative">
                                        <div class="pointer-events-none absolute inset-y-0 left-0 pl-3 flex items-center">
                                            <svg class="h-5 w-5 text-(--ui-text-muted)" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                                                <path stroke-linecap="round" stroke-linejoin="round" d="M16.5 12a4.5 4.5 0 1 1-9 0 4.5 4.5 0 0 1 9 0Zm0 0c0 1.657 1.007 3 2.25 3S21 13.657 21 12a9 9 0 1 0-2.636 6.364M16.5 12V8.25" />
                                            </svg>
                                        </div>
                                        <input type="email" formControlName="email" 
                                            [class.border-(--ui-danger)]="f['email'].invalid && f['email'].touched"
                                            class="block w-full rounded-lg border-(--ui-border) shadow-sm focus:border-(--ui-brand) focus:ring-(--ui-brand) sm:text-sm p-2.5 pl-10 transition-colors" placeholder="ornek@edu.com">
                                    </div>
                                    @if (f['email'].invalid && f['email'].touched) {
                                        <p class="mt-1 text-xs text-(--ui-danger) ml-0.5">Geçerli bir e-posta adresi girin.</p>
                                    }
                                </div>

                                <div class="group">
                                    <label class="block text-sm font-medium text-(--ui-text) mb-1.5 ml-0.5">Telefon Numarası</label>
                                    <div class="relative">
                                        <div class="pointer-events-none absolute inset-y-0 left-0 pl-3 flex items-center">
                                            <svg class="h-5 w-5 text-(--ui-text-muted)" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                                                <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 1.5H8.25A2.25 2.25 0 0 0 6 3.75v16.5a2.25 2.25 0 0 0 2.25 2.25h7.5A2.25 2.25 0 0 0 18 20.25V3.75a2.25 2.25 0 0 0-2.25-2.25H13.5m-3 0V3h3V1.5m-3 0h3m-3 18.75h3" />
                                            </svg>
                                        </div>
                                        <input type="text" formControlName="phoneNumber" 
                                            [class.border-(--ui-danger)]="f['phoneNumber'].invalid && f['phoneNumber'].touched"
                                            class="block w-full rounded-lg border-(--ui-border) shadow-sm focus:border-(--ui-brand) focus:ring-(--ui-brand) sm:text-sm p-2.5 pl-10 transition-colors" placeholder="+90 5xx xxx xx xx">
                                    </div>
                                    @if (f['phoneNumber'].invalid && f['phoneNumber'].touched) {
                                        <p class="mt-1 text-xs text-(--ui-danger) ml-0.5">Geçerli bir telefon numarası girin.</p>
                                    }
                                </div>

                                <div class="group">
                                    <label for="new-user-role" class="block text-sm font-medium text-(--ui-text) mb-1.5 ml-0.5">Kullanıcı Rolü</label>
                                    <div class="relative">
                                        <select id="new-user-role" formControlName="role"
                                            [class.border-(--ui-danger)]="f['role'].invalid && f['role'].touched"
                                            class="block w-full appearance-none rounded-lg border-(--ui-border) shadow-sm focus:border-(--ui-brand) focus:ring-(--ui-brand) sm:text-sm p-2.5 pl-4 cursor-pointer transition-colors">
                                            @for (role of roleOptions(); track role.value) {
                                                <option [value]="role.value">{{ role.label }}</option>
                                            }
                                        </select>
                                        <div class="pointer-events-none absolute inset-y-0 right-0 flex items-center px-4 text-(--ui-text-muted)">
                                            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path></svg>
                                        </div>
                                    </div>
                                    @if (f['role'].invalid && f['role'].touched) {
                                        <p class="mt-1 text-xs text-(--ui-danger) ml-0.5">Lütfen bir rol seçin.</p>
                                    }
                                </div>
                                @if (f.role.value === 'SystemAdmin') {
                                    <p class="text-sm text-(--ui-text-muted)">Sistem yöneticisi tüm servislerde geçerli genel bir roldür. Bu işlem ayrıca servis erişimi veya abonelik oluşturmaz.</p>
                                } @else {
                                    <fieldset class="group">
                                        <legend class="block text-sm font-medium text-(--ui-text) mb-2">Servisler</legend>
                                        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                                            <label class="flex items-center gap-3 rounded-lg border border-(--ui-border) p-3 text-sm text-(--ui-text) cursor-pointer"><input type="checkbox" formControlName="coaching" class="h-4 w-4 rounded accent-(--ui-brand)">Koçluk</label>
                                            <label class="flex items-center gap-3 rounded-lg border border-(--ui-border) p-3 text-sm text-(--ui-text) cursor-pointer"><input type="checkbox" formControlName="speedReading" class="h-4 w-4 rounded accent-(--ui-brand)">Hızlı Okuma</label>
                                        </div>
                                        @if (form.hasError('productRequired') && (f.coaching.touched || f.speedReading.touched)) {
                                            <p role="alert" class="mt-1 text-xs text-(--ui-danger)">En az bir servis seçin. İkisini birlikte seçebilirsiniz.</p>
                                        }
                                        <p class="mt-2 text-xs text-(--ui-text-muted)">Tek hesap ve ortak parola kullanılır. Seçtiğiniz rol her servise ayrı atanır; roller, abonelikler ve kurum bağlantıları servis bazında ayrıca yönetilir.</p>
                                    </fieldset>
                                }
                            </div>
                        </form>
                    } @else {
                        <div class="p-6 bg-(--ui-success-soft) rounded-xl border border-(--ui-success-soft) text-center">
                            <div class="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-(--ui-success-soft) mb-4 ring-4 ring-(--ui-success-soft)">
                                <svg class="h-8 w-8 text-(--ui-success)" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
                                </svg>
                            </div>
                            <h4 class="text-lg font-bold text-(--ui-text) mb-2">İşlem Başarılı!</h4>
                            <p class="text-sm text-(--ui-text-muted) mb-2">{{ createdUser()?.scope }}</p>
                            <p class="text-sm text-(--ui-text-muted) mb-6">Kullanıcı hesabı oluşturuldu ve güvenli parola kurulum daveti <strong>{{ createdUser()?.email }}</strong> adresine gönderildi.</p>
                            
                            <div class="bg-(--ui-surface) rounded-lg border border-(--ui-success-soft) shadow-sm overflow-hidden">
                                <div class="px-4 py-2 border-b border-(--ui-border) bg-(--ui-surface-muted)">
                                    <span class="text-xs font-semibold text-(--ui-text-muted)">Kurulum Daveti</span>
                                </div>
                                <div class="p-4 flex items-center justify-between">
                                    <span class="font-mono text-xl font-bold text-(--ui-text)">{{ createdUser()?.invitationSent ? 'Kurulum daveti gönderildi' : '' }}</span>
                                    <span class="p-2 text-(--ui-text-muted)" aria-hidden="true">
                                        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"></path></svg>
                                    </span>
                                </div>
                            </div>
                        </div>
                    }

                    @if (error()) {
                        <div class="p-4 bg-(--ui-danger-soft) text-(--ui-danger) rounded-xl border border-(--ui-danger-soft) flex items-start gap-3">
                            <svg class="w-5 h-5 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
                            <span class="text-sm font-medium">{{ error() }}</span>
                        </div>
                    }
                </div>

                <!-- Footer -->
                <div class="bg-(--ui-surface-muted) px-6 py-4 border-t border-(--ui-border) flex justify-end gap-3">
                    @if (!createdUser()) {
                        <button type="button" (click)="closeModal()" [disabled]="loading()" class="rounded-lg px-4 py-2 text-sm font-medium text-(--ui-text) bg-(--ui-surface) border border-(--ui-border) hover:bg-(--ui-surface-muted) focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-(--ui-brand) transition-colors">
                            İptal
                        </button>
                        <button type="button" (click)="save()" [disabled]="loading()" 
                            class="rounded-lg px-4 py-2 text-sm font-medium text-(--ui-brand-contrast) bg-(--ui-brand) hover:bg-(--ui-brand) focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-(--ui-brand) disabled:opacity-50 disabled:cursor-not-allowed transition-colors inline-flex items-center">
                            @if(loading()) {
                                <svg class="animate-spin -ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                                Kaydediliyor...
                            } @else {
                                Kullanıcı Oluştur
                            }
                        </button>
                    } @else {
                        <button type="button" (click)="closeModal(true)" 
                            class="rounded-lg px-6 py-2.5 text-sm font-medium text-(--ui-success) bg-(--ui-success-soft) hover:bg-(--ui-success-soft) focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-(--ui-success) transition-colors shadow-sm">
                            Tamam, Kapat
                        </button>
                    }
                </div>
            </div>
        </div>
      </div>
    </div>
  `
})
export class CreateUserModalComponent implements OnInit {
  @Output() close = new EventEmitter<boolean>(); // true if saved

  private fb = inject(FormBuilder);
  private identityService = inject(IdentityService);
  private toaster = inject(ToasterService);

  private readonly builtInRoles = [
    { value: 'Student', label: 'Öğrenci' },
    { value: 'Teacher', label: 'Öğretmen' },
    { value: 'InstitutionAdmin', label: 'Kurum Yöneticisi' },
    { value: 'InstitutionOwner', label: 'Kurum Sahibi' },
    { value: 'Parent', label: 'Veli' },
    { value: 'Editor', label: 'Editör' },
    { value: 'SystemAdmin', label: 'Sistem Yöneticisi' }
  ];

  roleOptions = signal(this.builtInRoles);

  form = this.fb.group({
    firstName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    lastName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', [Validators.pattern('^[+]*[(]{0,1}[0-9]{1,4}[)]{0,1}[-\\s\\./0-9]*$')]],
    role: ['Student', Validators.required],
    coaching: this.fb.nonNullable.control(false),
    speedReading: this.fb.nonNullable.control(false)
  }, { validators: form => form.get('role')?.value === 'SystemAdmin' || form.get('coaching')?.value || form.get('speedReading')?.value
      ? null : { productRequired: true } });

  // Getter for easy template access
  get f() { return this.form.controls; }

  loading = signal(false);
  error = signal<string | null>(null);
  createdUser = signal<{ email: string, invitationSent: boolean, scope: string } | null>(null);

  ngOnInit(): void {
    this.identityService.getRoles().subscribe({
      next: roles => {
        const merged = new Map(this.builtInRoles.map(role => [role.value.toLowerCase(), role]));
        for (const roleName of roles) {
          const value = roleName.trim();
          if (!value) continue;
          const key = value.toLowerCase();
          if (!merged.has(key)) merged.set(key, { value, label: value });
        }
        this.roleOptions.set([...merged.values()]);
      },
      error: () => this.roleOptions.set(this.builtInRoles)
    });
  }

  closeModal(saved: boolean = false) {
    if (this.loading()) return;
    this.close.emit(saved || !!this.createdUser());
  }

  save() {
    if (this.loading() || this.createdUser()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    const val = this.form.value;
    const products: (1 | 2)[] = [];
    if (val.role !== 'SystemAdmin') {
      if (val.coaching) products.push(1);
      if (val.speedReading) products.push(2);
    }
    // Older servers reject dual selection instead of silently creating only one service.
    const product = products.length === 1 ? products[0] : null;
    const scope = products.length ? products.map(product => productRoleLabel({ role: val.role!, product: product === 1 ? 'coaching' : 'speed-reading' })).join(' / ')
      : productRoleLabel({ role: val.role!, product: null });

    this.identityService.createUser({
      firstName: val.firstName!,
      lastName: val.lastName!,
      email: val.email!,
      phoneNumber: val.phoneNumber!,
      role: val.role!,
      product,
      products
    }).subscribe({
      next: () => {
        this.loading.set(false);
        this.createdUser.set({
          email: val.email!,
          invitationSent: true,
          scope
        });
        this.toaster.success('Kullanıcı başarıyla oluşturuldu.');
      },
      error: (err) => {
        console.error('API Error:', err);
        this.loading.set(false);

        const fallback = 'Kayıt sırasında bir hata oluştu.';
        let msg = getAdminErrorMessage(err, fallback, true);

        // MFA responses must keep their actionable message even when the API
        // also returns a generic problem-details payload.
        if (err?.status !== 403) {
          // 1. Result Pattern Support: { error: { code: '...', description: '...' } }
          const resultError = err.error?.error || err.error?.Error;

          if (resultError) {
            if (resultError.code === 'User.Exists') {
              msg = 'Bu e-posta adresi zaten sisteme kayıtlı.';
            } else {
              msg = resultError.description || resultError.message || msg;
            }
          }
          // 2. ProblemDetails Support
          else if (err.error?.detail) {
            msg = err.error.detail;
          }
          // 3. Simple String
          else if (typeof err.error === 'string') {
            msg = err.error;
          }
        }

        this.error.set(msg);
        this.toaster.error(msg);
      }
    });
  }
}
