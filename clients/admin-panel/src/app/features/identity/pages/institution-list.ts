import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { computed } from '@angular/core';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { InstitutionAdminDto, InstitutionDto, InstitutionService } from '../../../core/services/institution.service';

@Component({
  selector: 'app-institution-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="space-y-6">
      <header class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Kurum Yönetimi</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">Tenant yaşam döngüsü, lisans limitleri ve aktiflik durumu.</p>
        </div>
        @if (canCreate()) { <button type="button" class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white" (click)="showCreate.set(!showCreate())">
          {{ showCreate() ? 'Vazgeç' : 'Yeni Kurum' }}
        </button> }
      </header>

      @if (showCreate()) {
        <form class="grid gap-3 rounded-xl border border-gray-200 bg-white p-4 shadow-sm dark:border-gray-700 dark:bg-gray-800 md:grid-cols-4" (ngSubmit)="create()">
          <input class="rounded-lg border p-2 dark:bg-gray-900" name="name" [(ngModel)]="draft.name" placeholder="Kurum adı" required maxlength="200">
          <select class="rounded-lg border p-2 dark:bg-gray-900" name="type" [(ngModel)]="draft.type">
            @for (type of institutionTypes; track type.value) { <option [ngValue]="type.value">{{ type.label }}</option> }
          </select>
          <input class="rounded-lg border p-2 dark:bg-gray-900" name="city" [(ngModel)]="draft.city" placeholder="Şehir" maxlength="100">
          <div class="flex gap-2"><input class="min-w-0 flex-1 rounded-lg border p-2 dark:bg-gray-900" name="email" [(ngModel)]="draft.email" placeholder="E-posta" type="email" maxlength="255"><button class="rounded-lg bg-emerald-600 px-4 py-2 text-white" [disabled]="saving()">Kaydet</button></div>
        </form>
      }

      @if (editing(); as institution) {
        <form class="grid gap-3 rounded-xl border border-indigo-200 bg-indigo-50 p-4 shadow-sm dark:border-indigo-900 dark:bg-indigo-950/30 md:grid-cols-4" (ngSubmit)="saveEdit()">
          <div class="md:col-span-4 font-semibold text-gray-900 dark:text-white">{{ institution.name }} kurumunu düzenle</div>
          <input class="rounded-lg border p-2 dark:bg-gray-900" name="editName" [(ngModel)]="editDraft.name" placeholder="Kurum adı" required maxlength="200">
          <input class="rounded-lg border p-2 dark:bg-gray-900" name="editCity" [(ngModel)]="editDraft.city" placeholder="Şehir" maxlength="100">
          <input class="rounded-lg border p-2 dark:bg-gray-900" name="editDistrict" [(ngModel)]="editDraft.district" placeholder="İlçe" maxlength="100">
          <input class="rounded-lg border p-2 dark:bg-gray-900" name="editPhone" [(ngModel)]="editDraft.phone" placeholder="Telefon" maxlength="50">
          <input class="rounded-lg border p-2 dark:bg-gray-900" name="editEmail" [(ngModel)]="editDraft.email" placeholder="E-posta" type="email" maxlength="255">
          <input class="rounded-lg border p-2 dark:bg-gray-900" name="editWebsite" [(ngModel)]="editDraft.website" placeholder="Web sitesi" maxlength="500">
          @if (canChangeTenantSettings()) { <select class="rounded-lg border p-2 dark:bg-gray-900" name="editLicense" [(ngModel)]="editDraft.licenseType">
            @for (license of licenseTypes; track license.value) { <option [ngValue]="license.value">{{ license.label }}</option> }
          </select>
          <input class="rounded-lg border p-2 dark:bg-gray-900" name="editMaxStudents" [(ngModel)]="editDraft.maxStudents" type="number" min="1" max="10000" placeholder="Azami öğrenci">
          <input class="rounded-lg border p-2 dark:bg-gray-900" name="editMaxTeachers" [(ngModel)]="editDraft.maxTeachers" type="number" min="1" max="1000" placeholder="Azami öğretmen"> }
          <div class="md:col-span-4 flex gap-2"><button class="rounded-lg bg-indigo-600 px-4 py-2 text-white" [disabled]="saving()">Kaydet</button><button type="button" class="rounded-lg border px-4 py-2" (click)="editing.set(null)">Vazgeç</button></div>
        </form>
      }

      @if (adminInstitution(); as institution) {
        <form class="grid gap-3 rounded-xl border border-emerald-200 bg-emerald-50 p-4 shadow-sm dark:border-emerald-900 dark:bg-emerald-950/30 md:grid-cols-4" (ngSubmit)="assignAdmin()">
          <div class="md:col-span-4 font-semibold text-gray-900 dark:text-white">{{ institution.name }} kurumuna yönetici ata</div>
          <input class="rounded-lg border p-2 dark:bg-gray-900 md:col-span-2" name="adminUserId" [(ngModel)]="adminDraft.userId" placeholder="Kullanıcı ID (önce yönetici rolünü atayın)" required>
          <select class="rounded-lg border p-2 dark:bg-gray-900" name="adminRole" [(ngModel)]="adminDraft.role">@for (role of adminRoles; track role.value) { <option [ngValue]="role.value">{{ role.label }}</option> }</select>
          <div class="flex gap-2"><button class="rounded-lg bg-emerald-600 px-4 py-2 text-white" [disabled]="saving()">Ata</button><button type="button" class="rounded-lg border px-4 py-2" (click)="adminInstitution.set(null)">Vazgeç</button></div>
          <div class="md:col-span-4 rounded-lg border bg-white p-3 dark:border-gray-700 dark:bg-gray-900"><div class="mb-2 text-sm font-semibold">Mevcut yöneticiler</div>@for (admin of admins(); track admin.userId) { <div class="flex flex-wrap items-center justify-between gap-2 border-b py-2 text-sm last:border-0 dark:border-gray-700"><span>{{ admin.firstName }} {{ admin.lastName }} · {{ admin.email }} · {{ adminRoleName(admin.role) }}</span><button type="button" class="rounded border px-2 py-1" (click)="toggleAdmin(admin)">{{ admin.isActive ? 'Pasifleştir' : 'Aktifleştir' }}</button></div> } @empty { <span class="text-sm text-gray-500">Henüz yönetici atanmamış.</span> }</div>
        </form>
      }

      <div class="flex flex-wrap gap-3">
        <input class="rounded-lg border p-2 dark:bg-gray-900" [(ngModel)]="search" (keyup.enter)="applyFilters()" placeholder="Kurum, şehir veya e-posta ara" aria-label="Kurum ara">
        <select class="rounded-lg border p-2 dark:bg-gray-900" [(ngModel)]="activeFilter" (change)="applyFilters()" aria-label="Aktiflik filtresi">
          <option [ngValue]="undefined">Tümü</option><option [ngValue]="true">Aktif</option><option [ngValue]="false">Pasif</option>
        </select>
        <button class="rounded-lg border px-4 py-2" type="button" (click)="applyFilters()">Yenile</button>
      </div>

      @if (error()) { <div class="rounded-lg bg-red-50 p-3 text-sm text-red-700" role="alert">{{ error() }}</div> }
      <div class="overflow-x-auto rounded-xl border border-gray-200 bg-white dark:border-gray-700 dark:bg-gray-800">
        <table class="min-w-full text-left text-sm"><thead class="border-b bg-gray-50 text-gray-500 dark:border-gray-700 dark:bg-gray-900"><tr><th class="p-3">Kurum</th><th class="p-3">Tür</th><th class="p-3">Kullanım</th><th class="p-3">Lisans</th><th class="p-3">Durum</th><th class="p-3">İşlem</th></tr></thead>
          <tbody>
            @for (institution of institutions(); track institution.id) {
              <tr class="border-b last:border-0 dark:border-gray-700"><td class="p-3"><div class="font-semibold">{{ institution.name }}</div><div class="text-xs text-gray-500">{{ institution.city || 'Şehir yok' }} · {{ institution.email || 'E-posta yok' }}</div></td><td class="p-3">{{ typeName(institution.type) }}</td><td class="p-3">{{ institution.studentCount }}/{{ institution.maxStudents }} öğrenci<br>{{ institution.teacherCount }}/{{ institution.maxTeachers }} öğretmen</td><td class="p-3">{{ licenseName(institution.licenseType) }}</td><td class="p-3"><span [class]="institution.isActive ? 'text-emerald-600' : 'text-gray-500'">{{ institution.isActive ? 'Aktif' : 'Pasif' }}</span></td><td class="p-3"><div class="flex flex-wrap gap-2">@if (canManage()) { <button class="rounded border px-3 py-1" (click)="edit(institution)">Düzenle</button><button class="rounded border px-3 py-1" (click)="openAdmin(institution)">Yönetici ata</button> } @if (canChangeTenantSettings()) { <button class="rounded border px-3 py-1" (click)="toggle(institution)">{{ institution.isActive ? 'Pasifleştir' : 'Aktifleştir' }}</button> }</div></td></tr>
            } @empty { <tr><td colspan="6" class="p-8 text-center text-gray-500">Kurum bulunamadı.</td></tr> }
          </tbody>
        </table>
      </div>
      <div class="flex items-center justify-between text-sm text-gray-500"><span>Toplam {{ totalCount() }} kurum · Sayfa {{ currentPage() }} / {{ totalPages() }}</span><div class="flex gap-2"><button class="rounded border px-3 py-1 disabled:opacity-40" [disabled]="currentPage() <= 1" (click)="changePage(currentPage() - 1)">Önceki</button><button class="rounded border px-3 py-1 disabled:opacity-40" [disabled]="currentPage() >= totalPages()" (click)="changePage(currentPage() + 1)">Sonraki</button></div></div>
    </section>
  `
})
export class InstitutionListComponent {
  private readonly service = inject(InstitutionService);
  private readonly authService = inject(AuthService);
  private readonly platformId = inject(PLATFORM_ID);
  canManage = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.institutionsManage));
  canCreate = computed(() => this.canManage() && this.authService.userProfile()?.roles.includes('SystemAdmin'));
  canChangeTenantSettings = computed(() => this.authService.userProfile()?.roles.includes('SystemAdmin') ?? false);
  institutions = signal<InstitutionDto[]>([]);
  totalCount = signal(0);
  currentPage = signal(1);
  readonly pageSize = 25;
  totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));
  loading = signal(false);
  saving = signal(false);
  error = signal<string | null>(null);
  showCreate = signal(false);
  editing = signal<InstitutionDto | null>(null);
  adminInstitution = signal<InstitutionDto | null>(null);
  admins = signal<InstitutionAdminDto[]>([]);
  search = '';
  activeFilter: boolean | undefined = true;
  draft = { name: '', type: 1, city: '', email: '' };
  editDraft = { name: '', city: '', district: '', phone: '', email: '', website: '', licenseType: 1, maxStudents: 50, maxTeachers: 5 };
  adminDraft = { userId: '', role: 2 };
  institutionTypes = [{ value: 1, label: 'Okul' }, { value: 2, label: 'Dershane' }, { value: 3, label: 'Etüt Merkezi' }, { value: 4, label: 'Online Platform' }];
  licenseTypes = [{ value: 1, label: 'Deneme' }, { value: 2, label: 'Basic' }, { value: 3, label: 'Premium' }, { value: 4, label: 'Enterprise' }];
  adminRoles = [{ value: 1, label: 'Kurum sahibi' }, { value: 2, label: 'Yönetici' }, { value: 3, label: 'Müdür' }];

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  load() {
    this.loading.set(true); this.error.set(null);
    this.service.getAll(this.currentPage(), this.pageSize, this.search, this.activeFilter).subscribe({
      next: response => { this.institutions.set(response.items); this.totalCount.set(response.totalCount); this.loading.set(false); },
      error: () => { this.error.set('Kurumlar yüklenemedi.'); this.loading.set(false); }
    });
  }

  applyFilters() { this.currentPage.set(1); this.load(); }
  changePage(page: number) {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page); this.load();
  }

  create() {
    if (!this.draft.name.trim()) return;
    this.saving.set(true);
    this.service.create(this.draft).subscribe({
      next: () => { this.showCreate.set(false); this.draft = { name: '', type: 1, city: '', email: '' }; this.saving.set(false); this.load(); },
      error: () => { this.error.set('Kurum oluşturulamadı.'); this.saving.set(false); }
    });
  }

  toggle(institution: InstitutionDto) {
    this.service.setActive(institution.id, !institution.isActive).subscribe({ next: () => this.load(), error: () => this.error.set('Kurum durumu güncellenemedi.') });
  }

  edit(institution: InstitutionDto) {
    this.adminInstitution.set(null);
    this.editing.set(institution);
    this.editDraft = {
      name: institution.name,
      city: institution.city ?? '',
      district: institution.district ?? '',
      phone: institution.phone ?? '',
      email: institution.email ?? '',
      website: institution.website ?? '',
      licenseType: institution.licenseType,
      maxStudents: institution.maxStudents,
      maxTeachers: institution.maxTeachers
    };
  }

  saveEdit() {
    const institution = this.editing();
    if (!institution || !this.editDraft.name.trim()) return;
    this.saving.set(true);
    const payload = this.canChangeTenantSettings()
      ? this.editDraft
      : {
          name: this.editDraft.name,
          city: this.editDraft.city,
          district: this.editDraft.district,
          phone: this.editDraft.phone,
          email: this.editDraft.email,
          website: this.editDraft.website
        };
    this.service.update(institution.id, payload).subscribe({
      next: () => { this.saving.set(false); this.editing.set(null); this.load(); },
      error: () => { this.saving.set(false); this.error.set('Kurum güncellenemedi.'); }
    });
  }

  openAdmin(institution: InstitutionDto) {
    this.editing.set(null);
    this.adminInstitution.set(institution);
    this.adminDraft = { userId: '', role: 2 };
    this.admins.set([]);
    this.service.getAdmins(institution.id).subscribe({
      next: admins => this.admins.set(admins),
      error: () => this.error.set('Kurum yöneticileri yüklenemedi.')
    });
  }

  assignAdmin() {
    const institution = this.adminInstitution();
    if (!institution || !this.adminDraft.userId.trim()) return;
    this.saving.set(true);
    this.service.assignAdmin(institution.id, this.adminDraft.userId.trim(), this.adminDraft.role).subscribe({
      next: () => { this.saving.set(false); this.adminInstitution.set(null); this.load(); },
      error: () => { this.saving.set(false); this.error.set('Kurum yöneticisi atanamadı. Kullanıcı rolünü kontrol edin.'); }
    });
  }

  toggleAdmin(admin: InstitutionAdminDto) {
    const institution = this.adminInstitution();
    if (!institution) return;
    this.service.setAdminActive(institution.id, admin.userId, !admin.isActive).subscribe({
      next: () => this.openAdmin(institution),
      error: () => this.error.set('Kurum yöneticisi durumu güncellenemedi.')
    });
  }

  adminRoleName(role: number) { return ['Bilinmiyor', 'Kurum sahibi', 'Yönetici', 'Müdür'][role] ?? role; }

  typeName(type: number) { return this.institutionTypes.find(item => item.value === type)?.label ?? type; }
  licenseName(type: number) { return ['Bilinmiyor', 'Deneme', 'Basic', 'Premium', 'Enterprise'][type] ?? type; }
}
