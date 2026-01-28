import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { IdentityService, UserDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
    selector: 'app-change-password-modal',
    standalone: true,
    imports: [CommonModule, ReactiveFormsModule],
    template: `
    <div class="fixed inset-0 z-50 overflow-y-auto" aria-labelledby="modal-title" role="dialog" aria-modal="true">
      <!-- Backdrop (Modern Glass Effect) -->
      <div class="fixed inset-0 bg-gray-900/40 backdrop-blur-sm transition-opacity" (click)="closeModal()"></div>

      <div class="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
        <div class="relative transform overflow-hidden rounded-lg bg-white dark:bg-gray-800 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-lg">
          
          <div class="bg-white dark:bg-gray-800 px-4 pb-4 pt-5 sm:p-6 sm:pb-4">
            <div class="flex items-center space-x-3 mb-4">
                <div class="p-2 bg-yellow-100 dark:bg-yellow-900/30 rounded-lg text-yellow-600">
                    <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z"/>
                    </svg>
                </div>
                <h3 class="text-lg font-medium leading-6 text-gray-900 dark:text-white" id="modal-title">
                    Şifre Değiştir
                </h3>
            </div>
            
            <p class="text-sm text-gray-500 dark:text-gray-400 mb-6">
                <span class="font-semibold text-gray-700 dark:text-gray-200">{{ userData()?.fullName }}</span> kullanıcısı için yeni bir şifre belirleyin.
            </p>

            <form [formGroup]="form" class="space-y-4">
              <div class="relative">
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">Yeni Şifre</label>
                <div class="mt-1 relative rounded-md shadow-sm">
                    <input [type]="showPassword() ? 'text' : 'password'" formControlName="password" 
                        class="block w-full rounded-md border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2 bg-gray-50 dark:placeholder-gray-400 pr-10">
                    <button type="button" (click)="togglePassword()" class="absolute inset-y-0 right-0 pr-3 flex items-center text-gray-400 hover:text-gray-600 dark:hover:text-gray-300">
                        @if (showPassword()) {
                            <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" /></svg>
                        } @else {
                            <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 10.025 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" /></svg>
                        }
                    </button>
                </div>
              </div>

              <div class="relative">
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">Yeni Şifre (Tekrar)</label>
                <div class="mt-1 relative rounded-md shadow-sm">
                    <input [type]="showPassword() ? 'text' : 'password'" formControlName="confirmPassword" 
                        class="block w-full rounded-md border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2 bg-gray-50 dark:placeholder-gray-400 pr-10">
                    <button type="button" (click)="togglePassword()" class="absolute inset-y-0 right-0 pr-3 flex items-center text-gray-400 hover:text-gray-600 dark:hover:text-gray-300">
                        @if (showPassword()) {
                            <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" /></svg>
                        } @else {
                            <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 10.025 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" /></svg>
                        }
                    </button>
                </div>
              </div>

              @if (form.errors?.['mismatch'] && cp?.touched) {
                <p class="text-xs text-red-500">Şifreler birbiriyle eşleşmiyor.</p>
              }

              <!-- Detaylı Validasyon Uyarıları -->
              @if (p?.invalid && (p?.dirty || p?.touched)) {
                <div class="mt-2 space-y-1">
                    @if (p?.errors?.['required']) { <p class="text-xs text-red-500">• Şifre alanı zorunludur.</p> }
                    @if (p?.errors?.['minlength']) { <p class="text-xs text-red-500">• Şifre en az 6 karakter olmalıdır.</p> }
                    @if (p?.errors?.['pattern']) { <p class="text-xs text-red-500">• En az bir büyük harf, bir küçük harf ve bir rakam içermelidir.</p> }
                </div>
              }
            </form>

            @if (error()) {
                <div class="mt-4 p-3 bg-red-100 text-red-700 rounded-md text-sm border border-red-200 fade-in">
                    <div class="font-bold mb-1">Hata</div>
                    {{ error() }}
                </div>
            }
          </div>

          <div class="bg-gray-50 dark:bg-gray-700 px-4 py-3 sm:flex sm:flex-row-reverse sm:px-6">
            <button type="button" (click)="save()" [disabled]="form.invalid || loading()" 
                class="inline-flex w-full justify-center rounded-md border border-transparent bg-blue-600 px-4 py-2 text-base font-medium text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 sm:ml-3 sm:w-auto sm:text-sm disabled:opacity-50 transition-colors">
                {{ loading() ? 'Güncelleniyor...' : 'Şifreyi Güncelle' }}
            </button>
            <button type="button" (click)="closeModal()" class="mt-3 inline-flex w-full justify-center rounded-md border border-gray-300 bg-white dark:bg-gray-600 dark:text-gray-200 px-4 py-2 text-base font-medium text-gray-700 shadow-sm hover:bg-gray-50 dark:hover:bg-gray-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 sm:mt-0 sm:ml-3 sm:w-auto sm:text-sm transition-colors">İptal</button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class ChangePasswordModalComponent {
    @Input({ required: true }) userData = signal<UserDto | null>(null);
    @Output() close = new EventEmitter<boolean>();

    private fb = inject(FormBuilder);
    private identityService = inject(IdentityService);
    private toaster = inject(ToasterService);

    loading = signal(false);
    error = signal<string | null>(null);
    showPassword = signal(false);

    form = this.fb.group({
        password: ['', [
            Validators.required,
            Validators.minLength(6),
            Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/) // En az 1 büyük, 1 küçük harf ve 1 rakam
        ]],
        confirmPassword: ['', Validators.required]
    }, { validators: this.passwordMatchValidator });

    passwordMatchValidator(g: any) {
        const password = g.get('password').value;
        const confirm = g.get('confirmPassword').value;
        return password === confirm ? null : { mismatch: true };
    }

    // Template yardımcıları
    get p() { return this.form.get('password'); }
    get cp() { return this.form.get('confirmPassword'); }

    togglePassword() {
        this.showPassword.update(v => !v);
    }

    closeModal(saved: boolean = false) {
        this.close.emit(saved);
    }

    save() {
        const user = this.userData();
        if (!user || this.form.invalid) return;

        this.loading.set(true);
        this.error.set(null);

        this.identityService.changePassword(user.userId, this.form.value.password!).subscribe({
            next: () => {
                this.loading.set(false);
                this.toaster.success('Şifre başarıyla güncellendi.');
                this.closeModal(true);
            },
            error: (err) => {
                console.error('Password Change API Error:', err);
                this.loading.set(false);

                let msg = 'Şifre güncellenirken bir hata oluştu.';
                const errObj = err.error?.error || err.error?.Error;
                if (errObj) msg = errObj.message || errObj.description || msg;

                this.error.set(msg);
                this.toaster.error(msg);
            }
        });
    }
}
