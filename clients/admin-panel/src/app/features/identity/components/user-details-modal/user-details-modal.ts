import { Component, EventEmitter, Input, Output, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IdentityService, UserProfileDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
  selector: 'app-user-details-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="fixed inset-0 z-50 overflow-y-auto" aria-labelledby="modal-title" role="dialog" aria-modal="true">
      <!-- Backdrop (Modern Glass Effect) -->
      <div class="fixed inset-0 bg-gray-900/40 backdrop-blur-sm transition-opacity" (click)="closeModal()"></div>

      <div class="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
        <div class="relative transform overflow-hidden rounded-2xl bg-white dark:bg-gray-800 text-left shadow-2xl transition-all sm:my-8 sm:w-full sm:max-w-2xl">
          
          <!-- Header -->
          <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-700 flex justify-between items-center bg-gray-50/50 dark:bg-gray-900/20">
            <h3 class="text-lg font-bold text-gray-900 dark:text-white">Kullanıcı Detayları</h3>
            <button (click)="closeModal()" class="text-gray-400 hover:text-gray-500 dark:hover:text-gray-300 transition-colors">
              <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div class="px-6 py-6">
            @if (loading()) {
              <div class="flex flex-col items-center justify-center py-12">
                <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
                <p class="mt-4 text-gray-500 dark:text-gray-400">Bilgiler yükleniyor...</p>
              </div>
            } @else if (user()) {
              <div class="space-y-8">
                <!-- Profile Header -->
                <div class="flex items-center space-x-6">
                  <div class="relative">
                    <div class="h-24 w-24 rounded-full bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center text-blue-600 dark:text-blue-400 text-3xl font-bold border-4 border-white dark:border-gray-800 shadow-md">
                      @if (user()?.avatarUrl) {
                        <img [src]="user()?.avatarUrl" class="h-full w-full rounded-full object-cover">
                      } @else {
                        {{ user()?.firstName?.charAt(0) }}{{ user()?.lastName?.charAt(0) }}
                      }
                    </div>
                    <span [class]="user()?.isActive ? 'bg-green-500' : 'bg-red-500'" 
                      class="absolute bottom-1 right-1 block h-5 w-5 rounded-full border-2 border-white dark:border-gray-800 shadow-sm"
                      [title]="user()?.isActive ? 'Aktif' : 'Pasif'">
                    </span>
                  </div>
                  <div>
                    <h4 class="text-2xl font-bold text-gray-900 dark:text-white">{{ user()?.fullName }}</h4>
                    <div class="flex items-center mt-1 space-x-2">
                      <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300 shadow-sm">
                        {{ user()?.role }}
                      </span>
                      @if (user()?.emailConfirmed) {
                        <span class="inline-flex items-center text-xs text-green-600 dark:text-green-400 font-medium">
                          <svg class="w-4 h-4 mr-1" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/></svg>
                          E-Posta Doğrulandı
                        </span>
                      }
                    </div>
                  </div>
                </div>

                <!-- Info Grid -->
                <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-900/20 border border-gray-100 dark:border-gray-700">
                    <p class="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">E-Posta</p>
                    <p class="text-gray-900 dark:text-white font-medium break-all">{{ user()?.email }}</p>
                  </div>
                  <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-900/20 border border-gray-100 dark:border-gray-700">
                    <p class="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Telefon</p>
                    <p class="text-gray-900 dark:text-white font-medium">{{ user()?.phoneNumber || '-' }}</p>
                  </div>
                  <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-900/20 border border-gray-100 dark:border-gray-700">
                    <p class="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Son Giriş</p>
                    <p class="text-gray-900 dark:text-white font-medium">{{ user()?.lastLoginAt ? (user()?.lastLoginAt | date:'dd.MM.yyyy HH:mm') : 'Henüz giriş yapmadı' }}</p>
                  </div>
                </div>

                <!-- Roles & Permissions -->
                <div class="grid grid-cols-1 md:grid-cols-2 gap-6 pt-6 border-t border-gray-100 dark:border-gray-700">
                  <div>
                    <h5 class="text-sm font-bold text-gray-900 dark:text-white mb-3 uppercase tracking-wider">Roller</h5>
                    <div class="flex flex-wrap gap-2">
                      @for (role of user()?.roles; track role) {
                        <span class="px-3 py-1 bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 rounded-lg text-xs font-bold border border-blue-100 dark:border-blue-800 shadow-sm">
                          {{ role }}
                        </span>
                      }
                    </div>
                  </div>
                  <div>
                    <h5 class="text-sm font-bold text-gray-900 dark:text-white mb-3 uppercase tracking-wider">İzinler</h5>
                    <div class="flex flex-wrap gap-2 max-h-40 overflow-y-auto pr-2 custom-scrollbar">
                      @for (perm of user()?.permissions; track perm) {
                        <span class="px-2 py-0.5 bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-400 rounded-md text-[10px] border border-gray-200 dark:border-gray-600">
                          {{ perm.replace('Permissions.', '') }}
                        </span>
                      }
                    </div>
                  </div>
                </div>

                <!-- Role Specific Details -->
                @if (user()?.teacherDetails) {
                  <div class="space-y-4 pt-4 border-t border-gray-100 dark:border-gray-700">
                    <h5 class="text-lg font-bold text-gray-900 dark:text-white flex items-center">
                      <svg class="w-5 h-5 mr-2 text-blue-500" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path d="M12 14l9-5-9-5-9 5 9 5z"/><path d="M12 14l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0012 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 14l9-5-9-5-9 5 9 5zm0 0l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0012 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14zm-4 6v-7.5l4-2.222" /></svg>
                      Öğretmen Bilgileri
                    </h5>
                    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                      <div>
                        <p class="text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase">Ünvan</p>
                        <p class="text-gray-700 dark:text-gray-200">{{ user()?.teacherDetails?.title || '-' }}</p>
                      </div>
                      <div>
                        <p class="text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase">Kurum</p>
                        <p class="text-gray-700 dark:text-gray-200">{{ user()?.teacherDetails?.institutionName || 'Serbest Çalışan' }}</p>
                      </div>
                    </div>
                    <div>
                      <p class="text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase mb-2">Uzmanlık Alanları</p>
                      <div class="flex flex-wrap gap-2">
                        @for (subject of user()?.teacherDetails?.subjects; track subject) {
                          <span class="px-3 py-1 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg text-sm border border-gray-200 dark:border-gray-600 shadow-sm">
                            {{ subject }}
                          </span>
                        } @empty {
                          <p class="text-gray-400 italic text-sm">Belirtilmemiş</p>
                        }
                      </div>
                    </div>
                  </div>
                }

                @if (user()?.studentDetails) {
                  <div class="space-y-4 pt-4 border-t border-gray-100 dark:border-gray-700">
                    <h5 class="text-lg font-bold text-gray-900 dark:text-white flex items-center">
                      <svg class="w-5 h-5 mr-2 text-blue-500" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" /></svg>
                      Öğrenci Bilgileri
                    </h5>
                    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                      <div>
                        <p class="text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase">Sınıf Seviyesi</p>
                        <p class="text-gray-700 dark:text-gray-200">{{ user()?.studentDetails?.gradeLevel }}. Sınıf</p>
                      </div>
                      <div>
                        <p class="text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase">Öğrenme Stili</p>
                        <p class="text-gray-700 dark:text-gray-200">{{ user()?.studentDetails?.learningStyle || '-' }}</p>
                      </div>
                      <div>
                        <p class="text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase">Doğum Tarihi</p>
                        <p class="text-gray-700 dark:text-gray-200">{{ user()?.studentDetails?.birthDate | date:'dd.MM.yyyy' }}</p>
                      </div>
                      <div>
                        <p class="text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase">Kurum</p>
                        <p class="text-gray-700 dark:text-gray-200">{{ user()?.studentDetails?.institutionName || '-' }}</p>
                      </div>
                    </div>
                  </div>
                }
              </div>
            } @else if (error()) {
              <div class="p-4 bg-red-100 text-red-700 rounded-xl border border-red-200 text-center py-12">
                <svg class="h-12 w-12 mx-auto text-red-500 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
                <p class="font-bold">Hata Oluştu</p>
                <p>{{ error() }}</p>
                <button (click)="loadUserDetails()" class="mt-4 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors">Tekrar Dene</button>
              </div>
            }
          </div>

          <!-- Footer -->
          <div class="px-6 py-4 bg-gray-50 dark:bg-gray-700 flex justify-end">
            <button (click)="closeModal()" class="px-6 py-2 bg-white dark:bg-gray-600 border border-gray-300 dark:border-gray-500 text-gray-700 dark:text-white rounded-xl hover:bg-gray-50 dark:hover:bg-gray-500 transition-all shadow-sm">
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

  user = signal<UserProfileDto | null>(null);
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
      },
      error: (err) => {
        console.error('User detail error:', err);
        this.error.set('Kullanıcı bilgileri yüklenemedi.');
        this.loading.set(false);
        this.toaster.error('Bilgiler alınırken bir hata oluştu.');
      }
    });
  }

  closeModal() {
    this.close.emit();
  }
}
