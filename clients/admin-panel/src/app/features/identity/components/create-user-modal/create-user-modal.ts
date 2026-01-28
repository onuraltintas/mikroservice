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
    <div class="fixed inset-0 z-50 overflow-y-auto" aria-labelledby="modal-title" role="dialog" aria-modal="true">
      <!-- Backdrop -->
      <div class="fixed inset-0 bg-gray-900/50 backdrop-blur-sm transition-opacity" (click)="closeModal()"></div>

      <div class="flex min-h-screen items-center justify-center p-4 text-center sm:p-0">
        <div class="relative transform overflow-hidden rounded-2xl bg-white dark:bg-gray-800 text-left shadow-2xl transition-all sm:w-full sm:max-w-lg border border-gray-100 dark:border-gray-700">
          
          <!-- Header -->
          <div class="bg-white dark:bg-gray-800 px-6 pt-6 pb-4">
            <div class="flex items-center justify-between">
                <div class="flex items-center space-x-3">
                    <div class="flex-shrink-0 bg-blue-50 dark:bg-blue-900/30 p-2 rounded-lg">
                        <svg class="w-6 h-6 text-blue-600 dark:text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z"></path>
                        </svg>
                    </div>
                    <h3 class="text-xl font-bold leading-6 text-gray-900 dark:text-white" id="modal-title">
                        {{ createdUser() ? 'Kullanıcı Oluşturuldu' : 'Yeni Kullanıcı' }}
                    </h3>
                </div>
                <button (click)="closeModal()" class="text-gray-400 hover:text-gray-500 transition-colors">
                    <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path></svg>
                </button>
            </div>
            @if (!createdUser()) {
                <p class="mt-2 text-sm text-gray-500 dark:text-gray-400 ml-11">
                    Sisteme yeni bir kullanıcı eklemek için bilgileri doldurun.
                </p>
            }
          </div>

          <!-- Body -->
          <div class="px-6 pb-6">
            @if (!createdUser()) {
                <form [formGroup]="form" class="space-y-4 mt-2">
                  <div class="grid grid-cols-2 gap-4">
                      <div>
                        <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">Ad</label>
                        <input type="text" formControlName="firstName" 
                            [ngClass]="{'border-red-500': f['firstName'].invalid && f['firstName'].touched, 'border-gray-300 dark:border-gray-600': !(f['firstName'].invalid && f['firstName'].touched)}"
                            class="block w-full rounded-md border shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2 bg-gray-50 dark:bg-gray-700 dark:text-white" placeholder="Örn: Ahmet">
                        @if (f['firstName'].invalid && f['firstName'].touched) {
                            <div class="mt-1 space-y-1">
                                @if (f['firstName'].errors?.['required']) { <p class="text-[11px] text-red-500">• Ad alanı zorunludur.</p> }
                                @if (f['firstName'].errors?.['minlength']) { <p class="text-[11px] text-red-500">• En az 2 karakter olmalıdır.</p> }
                            </div>
                        }
                      </div>
                      <div>
                        <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">Soyad</label>
                        <input type="text" formControlName="lastName" 
                            [ngClass]="{'border-red-500': f['lastName'].invalid && f['lastName'].touched, 'border-gray-300 dark:border-gray-600': !(f['lastName'].invalid && f['lastName'].touched)}"
                            class="block w-full rounded-md border shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2 bg-gray-50 dark:bg-gray-700 dark:text-white" placeholder="Örn: Yılmaz">
                        @if (f['lastName'].invalid && f['lastName'].touched) {
                            <div class="mt-1 space-y-1">
                                @if (f['lastName'].errors?.['required']) { <p class="text-[11px] text-red-500">• Soyad alanı zorunludur.</p> }
                                @if (f['lastName'].errors?.['minlength']) { <p class="text-[11px] text-red-500">• En az 2 karakter olmalıdır.</p> }
                            </div>
                        }
                      </div>
                  </div>

                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">E-Posta Adresi</label>
                    <div class="relative">
                        <div class="pointer-events-none absolute inset-y-0 left-0 pl-3 flex items-center">
                            <svg class="h-4 w-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 12a4 4 0 10-8 0 4 4 0 008 0zm0 0v1.5a2.5 2.5 0 005 0V12a9 9 0 10-9 9m4.5-1.206a8.959 8.959 0 01-4.5 1.207" />
                            </svg>
                        </div>
                        <input type="email" formControlName="email" 
                            [ngClass]="{'border-red-500': f['email'].invalid && f['email'].touched, 'border-gray-300 dark:border-gray-600': !(f['email'].invalid && f['email'].touched)}"
                            class="block w-full rounded-md border shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2 pl-10 bg-gray-50 dark:bg-gray-700 dark:text-white" placeholder="ornek@edu.com">
                    </div>
                    @if (f['email'].invalid && f['email'].touched) {
                        <div class="mt-1 space-y-1">
                            @if (f['email'].errors?.['required']) { <p class="text-[11px] text-red-500">• E-posta alanı zorunludur.</p> }
                            @if (f['email'].errors?.['email']) { <p class="text-[11px] text-red-500">• Geçerli bir e-posta adresi girin.</p> }
                        </div>
                    }
                  </div>

                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">Telefon Numarası</label>
                    <div class="relative">
                        <div class="pointer-events-none absolute inset-y-0 left-0 pl-3 flex items-center">
                            <svg class="h-4 w-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                            </svg>
                        </div>
                        <input type="text" formControlName="phoneNumber" 
                            [ngClass]="{'border-red-500': f['phoneNumber'].invalid && f['phoneNumber'].touched, 'border-gray-300 dark:border-gray-600': !(f['phoneNumber'].invalid && f['phoneNumber'].touched)}"
                            class="block w-full rounded-md border shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2 pl-10 bg-gray-50 dark:bg-gray-700 dark:text-white" placeholder="+90 5xx xxx xx xx">
                    </div>
                    @if (f['phoneNumber'].invalid && f['phoneNumber'].touched) {
                        <p class="mt-1 text-[11px] text-red-500">• Geçerli bir telefon numarası girin.</p>
                    }
                  </div>

                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">Kullanıcı Rolü</label>
                    <div class="relative">
                        <select formControlName="role" 
                            [ngClass]="{'border-red-500': f['role'].invalid && f['role'].touched, 'border-gray-300 dark:border-gray-600': !(f['role'].invalid && f['role'].touched)}"
                            class="block w-full appearance-none rounded-md border shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2 pl-4 bg-gray-50 dark:bg-gray-700 dark:text-white cursor-pointer">
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
                        <p class="mt-1 text-[11px] text-red-500">• Lütfen bir rol seçin.</p>
                    }
                  </div>
                </form>
            } @else {
                <div class="mt-2 p-6 bg-green-50 dark:bg-green-900/10 rounded-2xl border border-green-100 dark:border-green-800 text-center animate-fade-in-up">
                    <div class="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-green-100 dark:bg-green-800 mb-4 ring-4 ring-green-50 dark:ring-green-900/30">
                        <svg class="h-8 w-8 text-green-600 dark:text-green-300" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
                        </svg>
                    </div>
                    <h4 class="text-lg font-bold text-gray-900 dark:text-white mb-2">İşlem Başarılı!</h4>
                    <p class="text-sm text-gray-600 dark:text-gray-300 mb-6">Kullanıcı hesabı oluşturuldu ve giriş bilgileri <strong>{{ form.value.email }}</strong> adresine gönderildi.</p>
                    
                    <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
                        <div class="p-4 border-b border-gray-100 dark:border-gray-700 bg-gray-50/50 dark:bg-gray-900/50">
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
                <div class="mt-4 p-4 bg-red-50 dark:bg-red-900/20 text-red-700 dark:text-red-300 rounded-xl border border-red-100 dark:border-red-800 flex items-start space-x-3 animate-shake">
                    <svg class="w-5 h-5 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path></svg>
                    <span class="text-sm font-medium">{{ error() }}</span>
                </div>
            }
          </div>

          <!-- Footer -->
          <div class="bg-gray-50 dark:bg-gray-800/50 px-6 py-4 flex flex-row-reverse border-t border-gray-100 dark:border-gray-700">
            @if (!createdUser()) {
                <button type="button" (click)="save()" [disabled]="loading()" 
                    class="ml-3 inline-flex w-full justify-center rounded-xl bg-blue-600 px-6 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 sm:w-auto disabled:opacity-50 disabled:cursor-not-allowed transition-all active:scale-95">
                    @if(loading()) {
                        <svg class="animate-spin -ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                        Kaydediliyor...
                    } @else {
                        Kullanıcı Oluştur
                    }
                </button>
                <button type="button" (click)="closeModal()" class="mt-3 inline-flex w-full justify-center rounded-xl bg-white dark:bg-gray-700 px-6 py-2.5 text-sm font-semibold text-gray-700 dark:text-gray-200 shadow-sm ring-1 ring-inset ring-gray-300 dark:ring-gray-600 hover:bg-gray-50 dark:hover:bg-gray-600 sm:mt-0 sm:w-auto transition-colors">
                    İptal
                </button>
            } @else {
                <button type="button" (click)="closeModal(true)" 
                    class="inline-flex w-full justify-center rounded-xl bg-green-600 px-8 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-green-500 focus:outline-none focus:ring-2 focus:ring-green-500 focus:ring-offset-2 sm:w-auto transition-all active:scale-95">
                    Tamam, Kapat
                </button>
            }
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
