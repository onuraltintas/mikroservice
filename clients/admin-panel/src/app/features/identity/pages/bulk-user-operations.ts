import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import {
  BulkUserOperationResult,
  IdentityService,
  UserDto
} from '../../../core/services/identity.service';
import { ToasterService } from '../../../core/services/toaster.service';

const MAX_IMPORT_BYTES = 5 * 1024 * 1024;

@Component({
  selector: 'app-bulk-user-operations',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="space-y-6" aria-labelledby="bulk-users-title">
      <header>
        <p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Kimlik yönetimi</p>
        <h1 id="bulk-users-title" class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Toplu kullanıcı işlemleri</h1>
        <p class="mt-2 max-w-3xl text-sm text-gray-600 dark:text-gray-300">Kullanıcıları UTF-8 CSV ile güvenli biçimde içe veya dışa aktarın; seçili kullanıcılara tek seferde rol atayın.</p>
      </header>

      @if (error()) {
        <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300">{{ error() }}</div>
      }

      <div class="grid grid-cols-1 gap-6 xl:grid-cols-2">
        @if (canImport()) {
          <section class="data-card space-y-4" aria-labelledby="bulk-import-title">
            <div><h2 id="bulk-import-title" class="text-lg font-semibold text-gray-900 dark:text-white">CSV içe aktar</h2><p class="muted">Her satır yeni bir kullanıcı daveti oluşturur. Parola panelde tutulmaz.</p></div>
            <div class="flex flex-wrap gap-3">
              <button type="button" class="secondary" (click)="downloadTemplate()" [disabled]="busy()">Şablonu indir</button>
              <label class="secondary cursor-pointer">CSV seç<input #fileInput type="file" accept=".csv,text/csv" class="sr-only" (change)="selectFile($event)" /></label>
              @if (selectedFile()) { <span class="muted self-center">{{ selectedFile()?.name }}</span> }
            </div>
            <p class="text-xs text-gray-500 dark:text-gray-400">UTF-8, en fazla 5 MB ve 1.000 satır. Sütunlar: firstName, lastName, email, phoneNumber, role.</p>
            <button type="button" class="primary" (click)="importUsers()" [disabled]="busy() || !selectedFile()">{{ importing() ? 'İçe aktarılıyor…' : 'Kullanıcıları içe aktar' }}</button>
            @if (importResult(); as result) { <div class="result-card"><strong>İçe aktarma sonucu</strong><p class="muted">Başarılı: {{ result.succeeded }} · Başarısız: {{ result.failed }}</p><ul class="error-list">@for (item of result.errors; track item) { <li>{{ item }}</li> }</ul></div> }
          </section>
        }

        @if (canView()) {
          <section class="data-card space-y-4" aria-labelledby="bulk-export-title">
            <div><h2 id="bulk-export-title" class="text-lg font-semibold text-gray-900 dark:text-white">CSV dışa aktar</h2><p class="muted">Yetkiniz dahilindeki kullanıcı alanları UTF-8 CSV olarak indirilir.</p></div>
            <div class="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <label>Arama<input [(ngModel)]="exportSearch" name="exportSearch" maxlength="100" placeholder="Ad veya e-posta" /></label>
              <label>Rol<select [(ngModel)]="exportRole" name="exportRole"><option value="">Tüm roller</option>@for (role of roles(); track role) { <option [value]="role">{{ role }}</option> }</select></label>
              <label>Durum<select [(ngModel)]="exportStatus" name="exportStatus"><option value="">Tümü</option><option value="true">Aktif</option><option value="false">Pasif</option></select></label>
            </div>
            <button type="button" class="primary" (click)="exportUsers()" [disabled]="busy()">{{ exporting() ? 'Dışa aktarılıyor…' : 'CSV indir' }}</button>
          </section>
        }
      </div>

      @if (canEdit()) {
        <section class="data-card space-y-4" aria-labelledby="bulk-role-title">
          <div><h2 id="bulk-role-title" class="text-lg font-semibold text-gray-900 dark:text-white">Toplu rol ataması</h2><p class="muted">Rol ataması sunucuda MFA ve SystemAdmin politikalarıyla doğrulanır. Liste seçimleri en fazla 100 kullanıcıyla sınırlıdır.</p></div>
          <form (ngSubmit)="loadUsers()" class="grid grid-cols-1 gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,14rem)_auto] md:items-end">
            <label>Arama<input [(ngModel)]="userSearch" name="userSearch" maxlength="100" placeholder="Ad veya e-posta" /></label>
            <label>Rol<select [(ngModel)]="userRoleFilter" name="userRoleFilter"><option value="">Tüm roller</option>@for (role of roles(); track role) { <option [value]="role">{{ role }}</option> }</select></label>
            <button type="submit" class="secondary" [disabled]="loadingUsers()">{{ loadingUsers() ? 'Yükleniyor…' : 'Kullanıcıları yükle' }}</button>
          </form>
          <div class="flex flex-wrap items-center gap-3 text-sm"><button type="button" class="secondary" (click)="toggleAll()" [disabled]="users().length === 0">{{ allSelected() ? 'Tümünü kaldır' : 'Tümünü seç' }}</button><span class="muted">{{ selectedUserIds().length }} kullanıcı seçildi</span></div>
          <div class="overflow-x-auto"><table class="data-table"><thead><tr><th><span class="sr-only">Seç</span></th><th>E-posta</th><th>Ad soyad</th><th>Roller</th></tr></thead><tbody>@for (user of users(); track user.userId) { <tr><td><input type="checkbox" [checked]="isSelected(user.userId)" (change)="toggleUser(user.userId)" [attr.aria-label]="user.email + ' seç'" /></td><td>{{ user.email }}</td><td>{{ user.fullName }}</td><td>{{ user.roles.join(', ') || 'Rol atanmadı' }}</td></tr> } @empty { <tr><td colspan="4" class="empty">Kullanıcı listesi boş.</td></tr> }</tbody></table></div>
          <div class="grid grid-cols-1 gap-3 md:grid-cols-[minmax(0,14rem)_auto_auto] md:items-end"><label>Atanacak rol<select [(ngModel)]="assignmentRole" name="assignmentRole"><option value="">Rol seçin</option>@for (role of roles(); track role) { <option [value]="role">{{ role }}</option> }</select></label><label class="check"><input type="checkbox" [(ngModel)]="removeExistingRoles" name="removeExistingRoles" /> Mevcut rolleri kaldır</label><button type="button" class="primary" (click)="assignRole()" [disabled]="busy() || selectedUserIds().length === 0 || !assignmentRole">{{ assigning() ? 'Atanıyor…' : 'Seçili kullanıcılara ata' }}</button></div>
          @if (assignmentResult(); as result) { <div class="result-card"><strong>Rol atama sonucu</strong><p class="muted">Başarılı: {{ result.succeeded }} · Başarısız: {{ result.failed }}</p><ul class="error-list">@for (item of result.errors; track item) { <li>{{ item }}</li> }</ul></div> }
        </section>
      }
    </section>
  `,
  styles: [`
    .data-card, .result-card { border: 1px solid rgb(229 231 235); border-radius: .75rem; background: white; padding: 1rem; }
    .result-card { background: rgb(249 250 251); }
    .muted { font-size: .875rem; color: rgb(107 114 128); }
    label { display: grid; gap: .35rem; font-size: .875rem; font-weight: 500; color: rgb(55 65 81); }
    input:not([type="checkbox"]), select { width: 100%; border: 1px solid rgb(209 213 219); border-radius: .5rem; padding: .5rem .75rem; background: transparent; font-weight: 400; }
    .primary, .secondary { border-radius: .5rem; padding: .55rem .85rem; font-size: .875rem; font-weight: 600; }
    .primary { background: rgb(79 70 229); color: white; } .primary:disabled, .secondary:disabled { opacity: .5; }
    .secondary { border: 1px solid rgb(209 213 219); color: rgb(55 65 81); background: white; }
    .check { display: flex; align-items: center; gap: .5rem; white-space: nowrap; } .check input { width: 1rem; height: 1rem; }
    .data-table { width: 100%; text-align: left; font-size: .875rem; } .data-table th { border-bottom: 1px solid rgb(229 231 235); padding: .5rem .75rem; font-size: .75rem; text-transform: uppercase; color: rgb(107 114 128); } .data-table td { border-bottom: 1px solid rgb(243 244 246); padding: .5rem .75rem; }
    .empty { padding: 1.25rem 0; text-align: center; color: rgb(107 114 128); } .error-list { margin-top: .5rem; max-height: 10rem; overflow-y: auto; color: rgb(185 28 28); font-size: .8rem; }
    @media (prefers-color-scheme: dark) { .data-card { border-color: rgb(55 65 81); background: rgb(31 41 55); } .result-card { border-color: rgb(55 65 81); background: rgb(17 24 39); } label, .data-table td { color: rgb(209 213 219); } .muted, .data-table th, .empty { color: rgb(156 163 175); } input:not([type="checkbox"]), select, .secondary { border-color: rgb(75 85 99); color: rgb(229 231 235); background: rgb(31 41 55); } }
  `]
})
export class BulkUserOperationsComponent {
  private readonly identityService = inject(IdentityService);
  private readonly authService = inject(AuthService);
  private readonly toaster = inject(ToasterService);
  private readonly platformId = inject(PLATFORM_ID);

  readonly canView = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.usersView));
  readonly canImport = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.usersCreate));
  readonly canEdit = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.usersEdit));
  readonly roles = signal<string[]>([]);
  readonly users = signal<UserDto[]>([]);
  readonly selectedUserIds = signal<string[]>([]);
  readonly selectedFile = signal<File | null>(null);
  readonly importResult = signal<BulkUserOperationResult | null>(null);
  readonly assignmentResult = signal<BulkUserOperationResult | null>(null);
  readonly loadingUsers = signal(false);
  readonly importing = signal(false);
  readonly exporting = signal(false);
  readonly assigning = signal(false);
  readonly error = signal('');

  exportSearch = '';
  exportRole = '';
  exportStatus = '';
  userSearch = '';
  userRoleFilter = '';
  assignmentRole = '';
  removeExistingRoles = false;

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.loadRoles();
      if (this.canEdit()) this.loadUsers();
    }
  }

  busy(): boolean {
    return this.importing() || this.exporting() || this.assigning();
  }

  allSelected(): boolean {
    const users = this.users();
    return users.length > 0 && users.every(user => this.isSelected(user.userId));
  }

  loadRoles(): void {
    this.identityService.getRoles().subscribe({
      next: roles => this.roles.set(roles.filter(role => role.trim().length > 0)),
      error: () => this.error.set('Roller yüklenemedi.')
    });
  }

  loadUsers(): void {
    this.loadingUsers.set(true);
    this.assignmentResult.set(null);
    this.identityService.getAllUsers(1, 100, this.userSearch.trim(), this.userRoleFilter || undefined).pipe(
      finalize(() => this.loadingUsers.set(false))
    ).subscribe({
      next: page => {
        this.users.set(page.items);
        this.selectedUserIds.set([]);
      },
      error: () => this.error.set('Kullanıcılar yüklenemedi.')
    });
  }

  selectFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    if (!file) return;
    if (!file.name.toLowerCase().endsWith('.csv') || file.size > MAX_IMPORT_BYTES) {
      this.selectedFile.set(null);
      this.error.set('Yalnızca 5 MB boyutuna kadar CSV dosyası seçilebilir.');
      return;
    }
    this.error.set('');
    this.importResult.set(null);
    this.selectedFile.set(file);
  }

  downloadTemplate(): void {
    this.identityService.getBulkUserImportTemplate().subscribe({
      next: blob => this.download(blob, 'users-import-template.csv'),
      error: () => this.error.set('CSV şablonu indirilemedi.')
    });
  }

  async importUsers(): Promise<void> {
    const file = this.selectedFile();
    if (!file || this.busy()) return;
    if (!await this.toaster.confirm('CSV içindeki kullanıcılar oluşturulsun mu?', { title: 'Toplu içe aktarma' })) return;

    this.importing.set(true);
    this.error.set('');
    this.identityService.importUsers(file).pipe(finalize(() => this.importing.set(false))).subscribe({
      next: result => {
        this.importResult.set(result);
        this.selectedFile.set(null);
        this.toaster.success(`İçe aktarma tamamlandı: ${result.succeeded} başarılı, ${result.failed} başarısız.`);
      },
      error: () => this.error.set('Kullanıcılar içe aktarılamadı; CSV biçimini ve yetkinizi kontrol edin.')
    });
  }

  exportUsers(): void {
    if (this.busy()) return;
    const isActive = this.exportStatus === '' ? undefined : this.exportStatus === 'true';
    this.exporting.set(true);
    this.identityService.exportUsers(this.exportSearch.trim(), this.exportRole, isActive).pipe(
      finalize(() => this.exporting.set(false))
    ).subscribe({
      next: blob => this.download(blob, `users-${new Date().toISOString().slice(0, 10)}.csv`),
      error: () => this.error.set('Kullanıcılar dışa aktarılamadı; yetkinizi kontrol edin.')
    });
  }

  toggleUser(userId: string): void {
    this.selectedUserIds.update(ids => ids.includes(userId) ? ids.filter(id => id !== userId) : [...ids, userId]);
  }

  toggleAll(): void {
    this.selectedUserIds.set(this.allSelected() ? [] : this.users().map(user => user.userId));
  }

  isSelected(userId: string): boolean {
    return this.selectedUserIds().includes(userId);
  }

  async assignRole(): Promise<void> {
    const userIds = this.selectedUserIds();
    if (!this.assignmentRole || userIds.length === 0 || this.busy()) return;
    if (!await this.toaster.confirm(`${userIds.length} kullanıcı için ${this.assignmentRole} rolü uygulansın mı?`, { title: 'Toplu rol ataması' })) return;

    this.assigning.set(true);
    this.error.set('');
    this.identityService.assignBulkRole(userIds, this.assignmentRole, this.removeExistingRoles).pipe(
      finalize(() => this.assigning.set(false))
    ).subscribe({
      next: result => {
        this.loadUsers();
        this.assignmentResult.set(result);
        this.toaster.success(`Rol ataması tamamlandı: ${result.succeeded} başarılı, ${result.failed} başarısız.`);
      },
      error: () => this.error.set('Toplu rol ataması yapılamadı; yetkinizi ve seçilen rolü kontrol edin.')
    });
  }

  private download(blob: Blob, filename: string): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 0);
  }
}
