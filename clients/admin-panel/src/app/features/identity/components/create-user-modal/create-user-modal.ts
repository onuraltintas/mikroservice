import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { IdentityService } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
  selector: 'app-create-user-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="fixed inset-0 z-[100]" aria-labelledby="modal-title" role="dialog" aria-modal="true">
      <!-- Backdrop (Static) -->
      <div class="fixed inset-0 bg-gray-900/40 backdrop-blur-sm transition-opacity" (click)="closeModal()"></div>

      <!-- Modal Position Container -->
      <div class="fixed inset-0 z-[101] overflow-y-auto">
        <div class="flex min-h-full items-start justify-center p-4 text-center sm:p-12">
            
            <!-- Modal Panel -->
            <div class="relative w-full max-w-lg transform overflow-hidden rounded-2xl bg-white dark:bg-gray-900 text-left shadow-2xl transition-all border border-gray-100 dark:border-gray-700 bg-opacity-100 dark:bg-opacity-100">
                
                <!-- Header -->
                <div class="bg-gradient-to-r from-blue-50 to-white dark:from-blue-900/20 dark:to-gray-800 p-6 border-b border-gray-100 dark:border-gray-700">
                    <div class="flex items-start justify-between">
                        <div class="flex items-center gap-4">
                            <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-100 text-blue-600 dark:bg-blue-900/50 dark:text-blue-400">
                                <svg class="h-6 w-6" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z" />
                                </svg>
                            </div>
                            <div>
                                <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
                                    {{ createdUser() ? 'Kullanıcı Oluşturuldu' : 'Yeni Kullanıcı' }}
                                </h3>
                                @if (!createdUser()) {
                                    <p class="text-sm text-gray-500 dark:text-gray-400">Sisteme yeni bir kullanıcı ekleyin.</p>
                                }
                            </div>
                        </div>
                        <button (click)="closeModal()" class="text-gray-400 hover:text-gray-500 dark:hover:text-gray-300">
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
                                <div class="grid grid-cols-2 gap-4">
                                    <div class="group">
                                        <label class="block text-sm font-medium text-gray-700 dark:text-gray-200 mb-1.5 ml-0.5">Ad</label>
                                        <input type="text" formControlName="firstName" 
                                            [class.border-red-500]="f['firstName'].invalid && f['firstName'].touched"
                                            class="block w-full rounded-lg border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm dark:bg-gray-800 dark:border-gray-600 dark:text-white p-2.5 transition-colors" placeholder="Ahmet">
                                        @if (f['firstName'].invalid && f['firstName'].touched) {
                                            <p class="mt-1 text-xs text-red-500 ml-0.5">En az 2 karakter gereklidir.</p>
                                        }
                                    </div>
                                    <div class="group">
                                        <label class="block text-sm font-medium text-gray-700 dark:text-gray-200 mb-1.5 ml-0.5">Soyad</label>
                                        <input type="text" formControlName="lastName" 
                                            [class.border-red-500]="f['lastName'].invalid && f['lastName'].touched"
                                            class="block w-full rounded-lg border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm dark:bg-gray-800 dark:border-gray-600 dark:text-white p-2.5 transition-colors" placeholder="Yılmaz">
                                        @if (f['lastName'].invalid && f['lastName'].touched) {
                                            <p class="mt-1 text-xs text-red-500 ml-0.5">En az 2 karakter gereklidir.</p>
                                        }
                                    </div>
                                </div>

                                <div class="group">
                                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-200 mb-1.5 ml-0.5">E-Posta Adresi</label>
                                    <div class="relative">
                                        <div class="pointer-events-none absolute inset-y-0 left-0 pl-3 flex items-center">
                                            <svg class="h-5 w-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                                                <path stroke-linecap="round" stroke-linejoin="round" d="M16.5 12a4.5 4.5 0 1 1-9 0 4.5 4.5 0 0 1 9 0Zm0 0c0 1.657 1.007 3 2.25 3S21 13.657 21 12a9 9 0 1 0-2.636 6.364M16.5 12V8.25" />
                                            </svg>
                                        </div>
                                        <input type="email" formControlName="email" 
                                            [class.border-red-500]="f['email'].invalid && f['email'].touched"
                                            class="block w-full rounded-lg border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2.5 pl-10 dark:bg-gray-800 dark:border-gray-600 dark:text-white transition-colors" placeholder="ornek@edu.com">
                                    </div>
                                    @if (f['email'].invalid && f['email'].touched) {
                                        <p class="mt-1 text-xs text-red-500 ml-0.5">Geçerli bir e-posta adresi girin.</p>
                                    }
                                </div>

                                <div class="group">
                                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-200 mb-1.5 ml-0.5">Telefon Numarası</label>
                                    <div class="relative">
                                        <div class="pointer-events-none absolute inset-y-0 left-0 pl-3 flex items-center">
                                            <svg class="h-5 w-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                                                <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 1.5H8.25A2.25 2.25 0 0 0 6 3.75v16.5a2.25 2.25 0 0 0 2.25 2.25h7.5A2.25 2.25 0 0 0 18 20.25V3.75a2.25 2.25 0 0 0-2.25-2.25H13.5m-3 0V3h3V1.5m-3 0h3m-3 18.75h3" />
                                            </svg>
                                        </div>
                                        <input type="text" formControlName="phoneNumber" 
                                            [class.border-red-500]="f['phoneNumber'].invalid && f['phoneNumber'].touched"
                                            class="block w-full rounded-lg border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2.5 pl-10 dark:bg-gray-800 dark:border-gray-600 dark:text-white transition-colors" placeholder="+90 5xx xxx xx xx">
                                    </div>
                                    @if (f['phoneNumber'].invalid && f['phoneNumber'].touched) {
                                        <p class="mt-1 text-xs text-red-500 ml-0.5">Geçerli bir telefon numarası girin.</p>
                                    }
                                </div>

                                <div class="group">
                                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-200 mb-1.5 ml-0.5">Kullanıcı Rolü</label>
                                    <div class="relative">
                                        <select formControlName="role" 
                                            [class.border-red-500]="f['role'].invalid && f['role'].touched"
                                            class="block w-full appearance-none rounded-lg border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2.5 pl-4 dark:bg-gray-800 dark:border-gray-600 dark:text-white cursor-pointer transition-colors">
                                            <option value="Student">🎓 Öğrenci</option>
                                            <option value="Teacher">👨‍🏫 Öğretmen</option>
                                            <option value="InstitutionAdmin">🏢 Kurum Yöneticisi</option>
                                            <option value="Parent">👨‍👩‍👦 Veli</option>
                                        </select>
                                        <div class="pointer-events-none absolute inset-y-0 right-0 flex items-center px-4 text-gray-500">
                                            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path></svg>
                                        </div>
                                    </div>
                                    @if (f['role'].invalid && f['role'].touched) {
                                        <p class="mt-1 text-xs text-red-500 ml-0.5">Lütfen bir rol seçin.</p>
                                    }
                                </div>
                            </div>
                        </form>
                    } @else {
                        <div class="p-6 bg-green-50 dark:bg-green-900/10 rounded-xl border border-green-100 dark:border-green-800 text-center">
                            <div class="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-green-100 dark:bg-green-800 mb-4 ring-4 ring-green-50 dark:ring-green-900/30">
                                <svg class="h-8 w-8 text-green-600 dark:text-green-300" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
                                </svg>
                            </div>
                            <h4 class="text-lg font-bold text-gray-900 dark:text-white mb-2">İşlem Başarılı!</h4>
                            <p class="text-sm text-gray-600 dark:text-gray-300 mb-6">Kullanıcı hesabı oluşturuldu ve giriş bilgileri <strong>{{ form.value.email }}</strong> adresine gönderildi.</p>
                            
                            <div class="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
                                <div class="px-4 py-2 border-b border-gray-100 dark:border-gray-700 bg-gray-50 dark:bg-gray-900/50">
                                    <span class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Geçici Şifre</span>
                                </div>
                                <div class="p-4 flex items-center justify-between">
                                    <span class="font-mono text-xl font-bold text-gray-800 dark:text-white tracking-widest">{{ createdUser()?.temporaryPassword }}</span>
                                    <button (click)="copyPassword()" class="p-2 text-gray-400 hover:text-blue-600 dark:hover:text-blue-400 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded-lg transition-all" title="Kopyala">
                                        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"></path></svg>
                                    </button>
                                </div>
                            </div>
                        </div>
                    }

                    @if (error()) {
                        <div class="p-4 bg-red-50 dark:bg-red-900/20 text-red-700 dark:text-red-300 rounded-xl border border-red-100 dark:border-red-800 flex items-start gap-3">
                            <svg class="w-5 h-5 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
                            <span class="text-sm font-medium">{{ error() }}</span>
                        </div>
                    }
                </div>

                <!-- Footer -->
                <div class="bg-gray-50 dark:bg-gray-800/50 px-6 py-4 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3">
                    @if (!createdUser()) {
                        <button type="button" (click)="closeModal()" class="rounded-lg px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 dark:bg-gray-800 dark:text-gray-300 dark:border-gray-600 dark:hover:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 transition-colors">
                            İptal
                        </button>
                        <button type="button" (click)="save()" [disabled]="loading()" 
                            class="rounded-lg px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors inline-flex items-center">
                            @if(loading()) {
                                <svg class="animate-spin -ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                                Kaydediliyor...
                            } @else {
                                Kullanıcı Oluştur
                            }
                        </button>
                    } @else {
                        <button type="button" (click)="closeModal(true)" 
                            class="rounded-lg px-6 py-2.5 text-sm font-medium text-white bg-green-600 hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 transition-colors shadow-sm">
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
export class CreateUserModalComponent {
  @Output() close = new EventEmitter<boolean>(); // true if saved

  private fb = inject(FormBuilder);
  private identityService = inject(IdentityService);
  private toaster = inject(ToasterService);

  form = this.fb.group({
    firstName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    lastName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', [Validators.pattern('^[+]*[(]{0,1}[0-9]{1,4}[)]{0,1}[-\\s\\./0-9]*$')]],
    role: ['Student', Validators.required]
  });

  // Getter for easy template access
  get f() { return this.form.controls; }

  loading = signal(false);
  error = signal<string | null>(null);
  createdUser = signal<{ email: string, temporaryPassword: string } | null>(null);

  closeModal(saved: boolean = false) {
    this.close.emit(saved || !!this.createdUser());
  }

  copyPassword() {
    if (this.createdUser()) {
      navigator.clipboard.writeText(this.createdUser()!.temporaryPassword);
      this.toaster.success('Şifre kopyalandı.');
    }
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    const val = this.form.value;

    this.identityService.createUser({
      firstName: val.firstName!,
      lastName: val.lastName!,
      email: val.email!,
      phoneNumber: val.phoneNumber!,
      role: val.role!
    }).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.createdUser.set({
          email: val.email!,
          temporaryPassword: res.temporaryPassword
        });
        this.toaster.success('Kullanıcı başarıyla oluşturuldu.');
      },
      error: (err) => {
        console.error('API Error:', err);
        this.loading.set(false);

        let msg = 'Kayıt sırasında bir hata oluştu.';

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

        this.error.set(msg);
        this.toaster.error(msg);
      }
    });
  }
}
