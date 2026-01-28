import { Component, EventEmitter, Input, Output, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IdentityService, UserDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
    selector: 'app-role-management-modal',
    standalone: true,
    imports: [CommonModule],
    template: `
    <div class="fixed inset-0 z-50 overflow-y-auto" aria-labelledby="modal-title" role="dialog" aria-modal="true">
      <!-- Backdrop -->
      <div class="fixed inset-0 bg-gray-900/40 backdrop-blur-sm transition-opacity" (click)="closeModal()"></div>

      <div class="flex min-h-screen items-center justify-center p-4 text-center sm:p-0">
        <div class="relative transform overflow-hidden rounded-2xl bg-white dark:bg-gray-800 text-left shadow-2xl transition-all sm:w-full sm:max-w-md border border-gray-100 dark:border-gray-700">
          
          <!-- Header -->
          <div class="px-6 pt-6 pb-4">
            <div class="flex items-center justify-between">
              <div class="flex items-center space-x-3">
                <div class="flex-shrink-0 bg-indigo-50 dark:bg-indigo-900/30 p-2 rounded-lg">
                  <svg class="w-6 h-6 text-indigo-600 dark:text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"></path>
                  </svg>
                </div>
                <div>
                  <h3 class="text-lg font-bold text-gray-900 dark:text-white leading-tight">Rol Yönetimi</h3>
                  <p class="text-sm text-gray-500 dark:text-gray-400">{{ user()?.fullName }}</p>
                </div>
              </div>
              <button (click)="closeModal()" class="text-gray-400 hover:text-gray-500 transition-colors">
                <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path></svg>
              </button>
            </div>
          </div>

          <!-- Body -->
          <div class="px-6 pb-6 space-y-6">
            
            <!-- Current Roles -->
            <div>
              <label class="block text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase tracking-wider mb-3">Tanımlı Roller</label>
              <div class="flex flex-wrap gap-2">
                @for (role of user()?.roles; track role) {
                  <span class="inline-flex items-center px-3 py-1 rounded-full text-xs font-medium bg-indigo-50 text-indigo-700 dark:bg-indigo-900/30 dark:text-indigo-300 border border-indigo-100 dark:border-indigo-800 group transition-all">
                    {{ role }}
                    <button (click)="removeRole(role)" [disabled]="actionLoading() === role" class="ml-2 text-indigo-400 hover:text-red-500 dark:hover:text-red-400 focus:outline-none transition-colors">
                      @if (actionLoading() === role) {
                        <svg class="animate-spin h-3 w-3" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                      } @else {
                        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path></svg>
                      }
                    </button>
                  </span>
                } @empty {
                  <div class="w-full py-4 text-center border-2 border-dashed border-gray-100 dark:border-gray-700 rounded-xl">
                    <p class="text-sm text-gray-400">Henüz bir rol atanmamış.</p>
                  </div>
                }
              </div>
            </div>

            <!-- Available Roles -->
            <div>
              <label class="block text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase tracking-wider mb-3">Rol Ekle</label>
              <div class="grid grid-cols-1 gap-2">
                @for (roleName of availableRoles(); track roleName) {
                  @if (!hasRole(roleName)) {
                    <button (click)="assignRole(roleName)" [disabled]="actionLoading() === roleName" class="flex items-center justify-between px-4 py-3 rounded-xl bg-gray-50 dark:bg-gray-700/50 hover:bg-white dark:hover:bg-gray-700 hover:shadow-sm border border-transparent hover:border-indigo-200 dark:hover:border-indigo-800 transition-all group">
                      <div class="flex items-center space-x-3">
                        <div class="w-8 h-8 rounded-lg bg-white dark:bg-gray-800 flex items-center justify-center shadow-sm text-sm group-hover:scale-110 transition-transform">
                          {{ getRoleEmoji(roleName) }}
                        </div>
                        <span class="text-sm font-medium text-gray-700 dark:text-gray-200">{{ roleName }}</span>
                      </div>
                      @if (actionLoading() === roleName) {
                        <svg class="animate-spin h-4 w-4 text-indigo-500" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                      } @else {
                        <svg class="w-5 h-5 text-gray-300 group-hover:text-indigo-500 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"></path></svg>
                      }
                    </button>
                  }
                }
              </div>
            </div>
            
          </div>

          <!-- Footer -->
          <div class="bg-gray-50 dark:bg-gray-800/50 px-6 py-4 flex flex-row-reverse border-t border-gray-100 dark:border-gray-700">
            <button (click)="closeModal()" class="px-6 py-2.5 rounded-xl bg-white dark:bg-gray-700 text-sm font-semibold text-gray-700 dark:text-gray-200 shadow-sm ring-1 ring-inset ring-gray-300 dark:ring-gray-600 hover:bg-gray-50 dark:hover:bg-gray-600 transition-colors">
              Kapat
            </button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class RoleManagementModalComponent implements OnInit {
    @Input({ required: true }) set userData(value: UserDto) {
        this.user.set(value);
    }
    @Output() close = new EventEmitter<boolean>(); // true if changed

    private identityService = inject(IdentityService);
    private toaster = inject(ToasterService);

    user = signal<UserDto | null>(null);
    availableRoles = signal<string[]>([]);
    actionLoading = signal<string | null>(null);
    changed = signal(false);

    ngOnInit() {
        this.loadRoles();
    }

    loadRoles() {
        this.identityService.getRoles().subscribe({
            next: (roles) => this.availableRoles.set(roles),
            error: (err) => this.toaster.error('Roller yüklenemedi.')
        });
    }

    hasRole(roleName: string): boolean {
        return this.user()?.roles.includes(roleName) || false;
    }

    getRoleEmoji(roleName: string): string {
        switch (roleName) {
            case 'SystemAdmin': return '🛡️';
            case 'InstitutionOwner': return '🏢';
            case 'Teacher': return '👨‍🏫';
            case 'Student': return '🎓';
            case 'Parent': return '👨‍👩‍👦';
            default: return '👤';
        }
    }

    assignRole(roleName: string) {
        const userId = this.user()?.userId;
        if (!userId) return;

        this.actionLoading.set(roleName);
        this.identityService.assignRole(userId, roleName).subscribe({
            next: () => {
                this.actionLoading.set(null);
                this.toaster.success(`${roleName} rolü başarıyla eklendi.`);
                this.changed.set(true);
                this.refreshUser();
            },
            error: (err) => {
                this.actionLoading.set(null);
                this.toaster.error('Rol eklenirken bir hata oluştu.');
            }
        });
    }

    removeRole(roleName: string) {
        const userId = this.user()?.userId;
        if (!userId) return;

        this.actionLoading.set(roleName);
        this.identityService.removeRole(userId, roleName).subscribe({
            next: () => {
                this.actionLoading.set(null);
                this.toaster.success(`${roleName} rolü başarıyla kaldırıldı.`);
                this.changed.set(true);
                this.refreshUser();
            },
            error: (err) => {
                this.actionLoading.set(null);
                this.toaster.error('Rol kaldırılırken bir hata oluştu.');
            }
        });
    }

    private refreshUser() {
        const userId = this.user()?.userId;
        if (!userId) return;

        this.identityService.getUserById(userId).subscribe({
            next: (userProfile) => {
                const currentUser = this.user();
                if (currentUser) {
                    this.user.set({
                        ...currentUser,
                        roles: userProfile.roles
                    });
                }
            }
        });
    }

    closeModal() {
        this.close.emit(this.changed());
    }
}
