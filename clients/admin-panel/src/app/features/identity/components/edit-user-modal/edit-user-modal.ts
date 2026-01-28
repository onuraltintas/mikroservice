import { Component, EventEmitter, Input, Output, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { IdentityService, UserDto, UpdateUserRequest } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
  selector: 'app-edit-user-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="fixed inset-0 z-50 overflow-y-auto" aria-labelledby="modal-title" role="dialog" aria-modal="true">
      <!-- Backdrop (Modern Glass Effect) -->
      <div class="fixed inset-0 bg-gray-900/40 backdrop-blur-sm transition-opacity" (click)="closeModal()"></div>

      <div class="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
        <div class="relative transform overflow-hidden rounded-lg bg-white dark:bg-gray-800 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-lg">
          
          <div class="bg-white dark:bg-gray-800 px-4 pb-4 pt-5 sm:p-6 sm:pb-4">
            <h3 class="text-lg font-medium leading-6 text-gray-900 dark:text-white" id="modal-title">
                Kullanıcı Düzenle
            </h3>
            
            <form [formGroup]="form" class="mt-4 space-y-4">
              <div class="grid grid-cols-2 gap-4">
                <div>
                  <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">Ad</label>
                  <input type="text" formControlName="firstName" 
                    [class.border-red-500]="f['firstName'].invalid && f['firstName'].touched"
                    class="mt-1 block w-full rounded-md border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-white shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2 bg-gray-50 dark:placeholder-gray-400">
                  @if (f['firstName'].invalid && f['firstName'].touched) {
                    <div class="mt-1 space-y-1">
                        @if (f['firstName'].errors?.['required']) { <p class="text-[11px] text-red-500">• Ad alanı zorunludur.</p> }
                        @if (f['firstName'].errors?.['minlength']) { <p class="text-[11px] text-red-500">• En az 2 karakter olmalıdır.</p> }
                    </div>
                  }
                </div>

                <div>
                  <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">Soyad</label>
                  <input type="text" formControlName="lastName" 
                    [class.border-red-500]="f['lastName'].invalid && f['lastName'].touched"
                    class="mt-1 block w-full rounded-md border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-white shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2 bg-gray-50 dark:placeholder-gray-400">
                  @if (f['lastName'].invalid && f['lastName'].touched) {
                    <div class="mt-1 space-y-1">
                        @if (f['lastName'].errors?.['required']) { <p class="text-[11px] text-red-500">• Soyad alanı zorunludur.</p> }
                        @if (f['lastName'].errors?.['minlength']) { <p class="text-[11px] text-red-500">• En az 2 karakter olmalıdır.</p> }
                    </div>
                  }
                </div>
              </div>

              <div>
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 opacity-60">E-Posta (Değiştirilemez)</label>
                <input type="email" [value]="user()?.email" disabled class="mt-1 block w-full rounded-md border-gray-200 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-400 shadow-sm sm:text-sm p-2 bg-gray-100 cursor-not-allowed">
              </div>

              <div>
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">Telefon Numarası</label>
                <input type="text" formControlName="phoneNumber" placeholder="05XX XXX XX XX" 
                  [class.border-red-500]="f['phoneNumber'].invalid && f['phoneNumber'].touched"
                  class="mt-1 block w-full rounded-md border-gray-300 dark:border-gray-600 dark:bg-gray-700 dark:text-white shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2 bg-gray-50 dark:placeholder-gray-400">
                @if (f['phoneNumber'].invalid && f['phoneNumber'].touched) {
                  <p class="mt-1 text-[11px] text-red-500">• Geçerli bir telefon numarası girin.</p>
                }
              </div>
            </form>

            @if (error()) {
                <div class="mt-3 p-3 bg-red-100 text-red-700 rounded-md text-sm border border-red-200 fade-in">
                    <div class="font-bold mb-1">Hata</div>
                    {{ error() }}
                </div>
            }
          </div>

          <div class="bg-gray-50 dark:bg-gray-700 px-4 py-3 sm:flex sm:flex-row-reverse sm:px-6">
            <button type="button" (click)="save()" [disabled]="loading() || !form.dirty" 
                class="inline-flex w-full justify-center rounded-md border border-transparent bg-blue-600 px-4 py-2 text-base font-medium text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 sm:ml-3 sm:w-auto sm:text-sm disabled:opacity-50 transition-all active:scale-95">
                {{ loading() ? 'Kaydediliyor...' : 'Güncelle' }}
            </button>
            <button type="button" (click)="closeModal()" class="mt-3 inline-flex w-full justify-center rounded-md border border-gray-300 bg-white dark:bg-gray-600 dark:text-gray-200 px-4 py-2 text-base font-medium text-gray-700 shadow-sm hover:bg-gray-50 dark:hover:bg-gray-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 sm:mt-0 sm:ml-3 sm:w-auto sm:text-sm transition-colors">İptal</button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class EditUserModalComponent implements OnInit {
  @Input({ required: true }) set userData(value: UserDto) {
    this.user.set(value);
    this.initForm();
  }
  @Output() close = new EventEmitter<boolean>(); // true if saved

  private fb = inject(FormBuilder);
  private identityService = inject(IdentityService);
  private toaster = inject(ToasterService);

  user = signal<UserDto | null>(null);
  loading = signal(false);
  error = signal<string | null>(null);

  form = this.fb.group({
    firstName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    lastName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    phoneNumber: ['', [Validators.pattern('^[+]*[(]{0,1}[0-9]{1,4}[)]{0,1}[-\\s\\./0-9]*$')]]
  });

  get f() { return this.form.controls; }

  ngOnInit() {
    this.initForm();
  }

  private initForm() {
    const currentUser = this.user();
    if (currentUser) {
      // Split fullName into first and last
      // Note: This is a fallback if backend doesn't provide them separately
      const names = currentUser.fullName.trim().split(' ');
      const firstName = names.slice(0, names.length - 1).join(' ') || names[0];
      const lastName = names.length > 1 ? names[names.length - 1] : '';

      this.form.patchValue({
        firstName: firstName,
        lastName: lastName,
        phoneNumber: currentUser.phoneNumber || ''
      });
    }
  }

  closeModal(saved: boolean = false) {
    this.close.emit(saved);
  }

  save() {
    const currentUser = this.user();
    if (!currentUser) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    const val = this.form.value;
    const request: UpdateUserRequest = {
      firstName: val.firstName!,
      lastName: val.lastName!,
      phoneNumber: val.phoneNumber || undefined
    };

    this.identityService.updateUser(currentUser.userId, request).subscribe({
      next: () => {
        this.loading.set(false);
        this.toaster.success('Kullanıcı başarıyla güncellendi.');
        this.closeModal(true);
      },
      error: (err) => {
        console.error('Update API Error:', err);
        this.loading.set(false);

        let msg = 'Güncelleme sırasında bir hata oluştu.';
        const errObj = err.error?.error || err.error?.Error;

        if (errObj) {
          msg = errObj.message || errObj.description || msg;
        } else if (err.error?.detail) {
          msg = err.error.detail;
        }

        this.error.set(msg);
        this.toaster.error(msg);
      }
    });
  }
}
