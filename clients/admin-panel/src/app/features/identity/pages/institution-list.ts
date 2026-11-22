import { CommonModule, isPlatformBrowser } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Component, inject, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { computed } from '@angular/core';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { InstitutionAdminDto, InstitutionDto, InstitutionService } from '../../../core/services/institution.service';
import { IdentityService, UserDto } from '../../../core/services/identity.service';
import { DistrictOption, LocationService, ProvinceOption } from '../../../core/services/location.service';

@Component({
  selector: 'app-institution-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <section class="space-y-6">
      <header class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-(--ui-text)">Kurumlar</h1>
          <p class="text-sm text-(--ui-text-muted)">Kurumların açılışı, kapanışı, lisans limitleri ve aktiflik durumu.</p>
        </div>
        @if (canCreate()) { <button type="button" class="rounded-lg bg-(--ui-brand) px-4 py-2 text-sm font-semibold text-(--ui-brand-contrast)" (click)="showCreate.set(!showCreate())">
          {{ showCreate() ? 'Vazgeç' : 'Yeni kurum' }}
        </button> }
      </header>

      @if (showCreate()) {
        <form class="grid gap-3 rounded-xl border border-(--ui-border) bg-(--ui-surface) p-4 shadow-sm md:grid-cols-4" (ngSubmit)="create()">
          <input class="rounded-lg border p-2" name="name" [(ngModel)]="draft.name" placeholder="Kurum adı" required maxlength="200">
          <select class="rounded-lg border p-2" name="type" [(ngModel)]="draft.type">
            @for (type of institutionTypes; track type.value) { <option [ngValue]="type.value">{{ type.label }}</option> }
          </select>
          <select class="rounded-lg border p-2" name="provinceId" [(ngModel)]="draft.provinceId" (ngModelChange)="onCreateProvinceChange()"><option value="">İl seçin</option>@for (province of provinces(); track province.id) { <option [value]="province.id">{{ province.name }}</option> }</select>
          <select class="rounded-lg border p-2" name="districtId" [(ngModel)]="draft.districtId" [disabled]="!draft.provinceId"><option value="">İlçe seçin</option>@for (district of createDistricts(); track district.id) { <option [value]="district.id">{{ district.name }}</option> }</select>
          <div class="flex gap-2"><input class="min-w-0 flex-1 rounded-lg border p-2" name="email" [(ngModel)]="draft.email" placeholder="E-posta" type="email" maxlength="255"><button class="rounded-lg bg-(--ui-success-soft) px-4 py-2 text-(--ui-success)" [disabled]="saving()">Kaydet</button></div>
        </form>
      }

      @if (editing(); as institution) {
        <form class="grid gap-3 rounded-xl border border-(--ui-brand-soft) bg-(--ui-brand-soft) p-4 shadow-sm md:grid-cols-4" (ngSubmit)="saveEdit()">
          <div class="md:col-span-4 font-semibold text-(--ui-text)">{{ institution.name }} kurumunu düzenle</div>
          <input class="rounded-lg border p-2" name="editName" [(ngModel)]="editDraft.name" placeholder="Kurum adı" required maxlength="200">
          <select class="rounded-lg border p-2" name="editProvinceId" [(ngModel)]="editDraft.provinceId" (ngModelChange)="onEditProvinceChange()"><option value="">İl seçin</option>@for (province of provinces(); track province.id) { <option [value]="province.id">{{ province.name }}</option> }</select>
          <select class="rounded-lg border p-2" name="editDistrictId" [(ngModel)]="editDraft.districtId" [disabled]="!editDraft.provinceId"><option value="">İlçe seçin</option>@for (district of editDistricts(); track district.id) { <option [value]="district.id">{{ district.name }}</option> }</select>
          <input class="rounded-lg border p-2" name="editPhone" [(ngModel)]="editDraft.phone" placeholder="Telefon" maxlength="50">
          <input class="rounded-lg border p-2" name="editEmail" [(ngModel)]="editDraft.email" placeholder="E-posta" type="email" maxlength="255">
          <input class="rounded-lg border p-2" name="editWebsite" [(ngModel)]="editDraft.website" placeholder="Web sitesi" maxlength="500">
          <div class="md:col-span-4 flex gap-2"><button class="rounded-lg bg-(--ui-brand) px-4 py-2 text-(--ui-brand-contrast)" [disabled]="saving()">Kaydet</button><button type="button" class="rounded-lg border px-4 py-2" (click)="editing.set(null)">Vazgeç</button></div>
        </form>
      }

      @if (adminInstitution(); as institution) {
        <form class="grid gap-3 rounded-xl border border-(--ui-success-soft) bg-(--ui-success-soft) p-4 shadow-sm md:grid-cols-4" (ngSubmit)="assignAdmin()">
          <div class="md:col-span-4 font-semibold text-(--ui-text)">{{ institution.name }} kurumuna yönetici ata</div>
          <select class="rounded-lg border p-2" name="adminProduct" [(ngModel)]="adminDraft.product" (ngModelChange)="onAdminProductChange()" aria-label="Ürün seçin">@for (product of adminProducts; track product.value) { <option [ngValue]="product.value">{{ product.label }}</option> }</select>
          <div class="relative md:col-span-2"><label class="sr-only" for="institution-admin-search">Kurum yöneticisi ara</label><input id="institution-admin-search" class="w-full rounded-lg border p-2" name="adminUserSearch" [(ngModel)]="adminUserSearch" (ngModelChange)="searchAdminCandidates($event)" placeholder="Yönetici adı veya e-postası ara" autocomplete="off" required>@if (adminCandidates().length > 0) {<div class="absolute z-10 mt-1 w-full overflow-hidden rounded-lg border border-(--ui-border) bg-(--ui-surface) shadow-lg" role="listbox" aria-label="Yönetici adayları">@for (candidate of adminCandidates(); track candidate.userId) {<button type="button" role="option" class="block w-full border-b px-3 py-2 text-left text-sm last:border-0 hover:bg-(--ui-surface-muted)" (click)="selectAdminCandidate(candidate)"><strong>{{ candidate.fullName }}</strong><span class="block text-xs text-(--ui-text-muted)">{{ candidate.email }}, {{ candidate.roles.join(', ') }}</span></button>}</div>}</div>
          <select class="rounded-lg border p-2" name="adminRole" [(ngModel)]="adminDraft.role">@for (role of adminRoles; track role.value) { <option [ngValue]="role.value">{{ role.label }}</option> }</select>
          <div class="flex gap-2"><button class="rounded-lg bg-(--ui-success-soft) px-4 py-2 text-(--ui-success)" [disabled]="saving()">Ata</button><button type="button" class="rounded-lg border px-4 py-2" (click)="adminInstitution.set(null)">Vazgeç</button></div>
          <div class="md:col-span-4 rounded-lg border bg-(--ui-surface) p-3"><div class="mb-2 text-sm font-semibold">Mevcut yöneticiler</div>@for (admin of admins(); track admin.userId) { <div class="flex flex-wrap items-center justify-between gap-2 border-b py-2 text-sm last:border-0"><span>{{ admin.firstName }} {{ admin.lastName }}, {{ admin.email }}, {{ adminRoleName(admin.role) }}</span><button type="button" class="rounded border px-2 py-1" (click)="toggleAdmin(admin)">{{ admin.isActive ? 'Pasifleştir' : 'Aktifleştir' }}</button></div> } @empty { <span class="text-sm text-(--ui-text-muted)">Henüz yönetici atanmamış.</span> }</div>
        </form>
      }

      <div class="flex flex-wrap gap-3">
        <input class="rounded-lg border p-2" [(ngModel)]="search" (keyup.enter)="applyFilters()" placeholder="Kurum, şehir veya e-posta ara" aria-label="Kurum ara">
        <select class="rounded-lg border p-2" [(ngModel)]="activeFilter" (change)="applyFilters()" aria-label="Aktiflik filtresi">
          <option [ngValue]="undefined">Tümü</option><option [ngValue]="true">Aktif</option><option [ngValue]="false">Pasif</option>
        </select>
        <button class="rounded-lg border px-4 py-2" type="button" (click)="applyFilters()">Yenile</button>
      </div>

      @if (error()) { <div class="rounded-lg bg-(--ui-danger-soft) p-3 text-sm text-(--ui-danger)" role="alert">{{ error() }}</div> }
      <div class="overflow-x-auto rounded-xl border border-(--ui-border) bg-(--ui-surface)">
        <table class="min-w-full text-left text-sm"><thead class="border-b bg-(--ui-surface-muted) text-(--ui-text-muted)"><tr><th class="p-3">Kurum</th><th class="p-3">Tür</th><th class="p-3">Kullanım</th><th class="p-3">Durum</th><th class="p-3">İşlem</th></tr></thead>
          <tbody>
            @for (institution of institutions(); track institution.id) {
<tr class="border-b last:border-0"><td class="p-3"><div class="font-semibold">{{ institution.name }}</div><div class="text-xs text-(--ui-text-muted)">{{ institution.city || 'Şehir yok' }}, {{ institution.email || 'E-posta yok' }}</div><div class="mt-1 flex items-center gap-2 text-xs text-(--ui-text-muted)"><span class="font-mono break-all" [attr.data-testid]="'institution-id-' + institution.id">{{ institution.id }}</span><button type="button" class="rounded border px-1.5 py-0.5" (click)="copyId(institution.id)">{{ copiedId() === institution.id ? 'Kopyalandı' : 'Kimliği kopyala' }}</button></div></td><td class="p-3">{{ typeName(institution.type) }}</td><td class="p-3">{{ institution.studentCount }} öğrenci<br>{{ institution.teacherCount }} öğretmen</td><td class="p-3"><span [class]="institution.isActive ? 'text-(--ui-success)' : 'text-(--ui-text-muted)'">{{ institution.isActive ? 'Aktif' : 'Pasif' }}</span></td><td class="p-3"><div class="flex flex-wrap gap-2">@if (canManage()) { <button class="rounded border px-3 py-1" (click)="edit(institution)">Düzenle</button><button class="rounded border px-3 py-1" (click)="openAdmin(institution)">Yönetici ata</button><a class="rounded border px-3 py-1" routerLink="/dashboard/coaching/students" [queryParams]="{ institutionId: institution.id }">Koçluk öğrencileri</a><a class="rounded border px-3 py-1" routerLink="/dashboard/coaching/teachers" [queryParams]="{ institutionId: institution.id }">Koçluk öğretmenleri</a><a class="rounded border px-3 py-1" [routerLink]="['/dashboard/speed-reading/institutions', institution.id]">Hızlı Okuma üyeleri</a> } @if (canChangeTenantSettings()) { <button class="rounded border px-3 py-1" (click)="toggle(institution)">{{ institution.isActive ? 'Pasifleştir' : 'Aktifleştir' }}</button> }</div></td></tr>
            } @empty { <tr><td colspan="6" class="p-8 text-center text-(--ui-text-muted)">Kurum bulunamadı.</td></tr> }
          </tbody>
        </table>
      </div>
      <div class="flex items-center justify-between text-sm text-(--ui-text-muted)"><span>Toplam {{ totalCount() }} kurum, sayfa {{ currentPage() }} / {{ totalPages() }}</span><div class="flex gap-2"><button class="rounded border px-3 py-1 disabled:opacity-40" [disabled]="currentPage() <= 1" (click)="changePage(currentPage() - 1)">Önceki</button><button class="rounded border px-3 py-1 disabled:opacity-40" [disabled]="currentPage() >= totalPages()" (click)="changePage(currentPage() + 1)">Sonraki</button></div></div>
    </section>
  `
})
export class InstitutionListComponent {
  private readonly service = inject(InstitutionService);
  private readonly identityService = inject(IdentityService);
  private readonly authService = inject(AuthService);
  private readonly locations = inject(LocationService);
  private readonly platformId = inject(PLATFORM_ID);
  canChangeTenantSettings = computed(() => this.authService.userProfile()?.roles.includes('SystemAdmin') ?? false);
  canManage = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.institutionsManage));
  canCreate = computed(() => this.canManage() && this.authService.userProfile()?.roles.includes('SystemAdmin'));
  institutions = signal<InstitutionDto[]>([]);
  totalCount = signal(0);
  /** Institution id last copied; "Abonelik ve ödeme › Elle abonelik ver" asks for it. */
  copiedId = signal<string | null>(null);
  copyId(id: string): void {
    void navigator.clipboard?.writeText(id).then(() => this.copiedId.set(id), () => undefined);
  }
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
  adminCandidates = signal<UserDto[]>([]);
  provinces = signal<ProvinceOption[]>([]);
  createDistricts = signal<DistrictOption[]>([]);
  editDistricts = signal<DistrictOption[]>([]);
  search = '';
  activeFilter: boolean | undefined = true;
  draft = { name: '', type: 1, provinceId: '', districtId: '', email: '' };
  editDraft = { name: '', provinceId: '', districtId: '', phone: '', email: '', website: '' };
  adminDraft = { userId: '', role: 2, product: 1 };
  adminUserSearch = '';
  institutionTypes = [{ value: 1, label: 'Okul' }, { value: 2, label: 'Dershane' }, { value: 3, label: 'Etüt Merkezi' }, { value: 4, label: 'Online Platform' }];
  adminRoles = [{ value: 1, label: 'Kurum sahibi' }, { value: 2, label: 'Yönetici' }, { value: 3, label: 'Müdür' }];
  adminProducts = [{ value: 1, label: 'Koçluk' }, { value: 2, label: 'Hızlı Okuma' }];

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
      this.locations.getProvinces().subscribe({
        next: provinces => this.provinces.set(provinces),
        error: () => this.error.set('İl seçenekleri yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
      });
    }
  }

  load() {
    this.loading.set(true); this.error.set(null);
    this.service.getAll(this.currentPage(), this.pageSize, this.search, this.activeFilter).subscribe({
      next: response => { this.institutions.set(response.items); this.totalCount.set(response.totalCount); this.loading.set(false); },
      error: () => { this.error.set('Kurumlar yüklenemedi. Sayfayı yenileyip yeniden deneyin.'); this.loading.set(false); }
    });
  }

  applyFilters() { this.currentPage.set(1); this.load(); }
  changePage(page: number) {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page); this.load();
  }

  create() {
    if (!this.draft.name.trim() || !this.draft.provinceId || !this.draft.districtId) return;
    this.saving.set(true);
    this.service.create(this.draft).subscribe({
      next: () => { this.showCreate.set(false); this.draft = { name: '', type: 1, provinceId: '', districtId: '', email: '' }; this.createDistricts.set([]); this.saving.set(false); this.load(); },
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
      provinceId: institution.provinceId ?? '',
      districtId: institution.districtId ?? '',
      phone: institution.phone ?? '',
      email: institution.email ?? '',
      website: institution.website ?? ''
    };
    if (this.editDraft.provinceId) this.loadEditDistricts(this.editDraft.provinceId);
  }

  saveEdit() {
    const institution = this.editing();
    if (!institution || !this.editDraft.name.trim()) return;
    this.saving.set(true);
    const payload = {
      name: this.editDraft.name,
      provinceId: this.editDraft.provinceId || undefined,
      districtId: this.editDraft.districtId || undefined,
      phone: this.editDraft.phone,
      email: this.editDraft.email,
      website: this.editDraft.website
    };
    this.service.update(institution.id, payload).subscribe({
      next: () => { this.saving.set(false); this.editing.set(null); this.load(); },
      error: () => { this.saving.set(false); this.error.set('Kurum güncellenemedi.'); }
    });
  }

  onCreateProvinceChange() {
    this.draft.districtId = '';
    this.createDistricts.set([]);
    if (this.draft.provinceId) {
      this.locations.getDistricts(this.draft.provinceId).subscribe({
        next: districts => this.createDistricts.set(districts),
        error: () => this.error.set('İlçe seçenekleri yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
      });
    }
  }

  onEditProvinceChange() {
    this.editDraft.districtId = '';
    if (this.editDraft.provinceId) this.loadEditDistricts(this.editDraft.provinceId);
    else this.editDistricts.set([]);
  }

  private loadEditDistricts(provinceId: string) {
    this.locations.getDistricts(provinceId).subscribe({
      next: districts => this.editDistricts.set(districts),
      error: () => this.error.set('İlçe seçenekleri yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
    });
  }

  openAdmin(institution: InstitutionDto, product = 1) {
    this.editing.set(null);
    this.adminInstitution.set(institution);
    this.adminDraft = { userId: '', role: 2, product };
    this.adminUserSearch = '';
    this.adminCandidates.set([]);
    this.admins.set([]);
    this.loadInstitutionAdmins(institution.id);
  }

  onAdminProductChange() {
    this.adminDraft.userId = '';
    this.adminUserSearch = '';
    this.adminCandidates.set([]);
    const institution = this.adminInstitution();
    if (institution) this.loadInstitutionAdmins(institution.id);
  }

  private loadInstitutionAdmins(institutionId: string) {
    this.service.getAdmins(institutionId, this.adminDraft.product).subscribe({
      next: admins => this.admins.set(admins),
      error: () => this.error.set('Kurum yöneticileri yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
    });
  }

  searchAdminCandidates(search: string) {
    this.adminDraft.userId = '';
    const term = search.trim();
    if (term.length < 2) {
      this.adminCandidates.set([]);
      return;
    }

    this.identityService.getAllUsers(1, 20, term, undefined, true, this.adminDraft.product).subscribe({
      next: result => this.adminCandidates.set(result.items.filter(user =>
        user.roles.includes('InstitutionAdmin') || user.roles.includes('InstitutionOwner'))),
      error: () => { this.adminCandidates.set([]); this.error.set('Yönetici adayları yüklenemedi. Sayfayı yenileyip yeniden deneyin.'); }
    });
  }

  selectAdminCandidate(candidate: UserDto) {
    this.adminDraft.userId = candidate.userId;
    this.adminUserSearch = `${candidate.fullName} · ${candidate.email}`;
    this.adminCandidates.set([]);
  }

  assignAdmin() {
    const institution = this.adminInstitution();
    if (!institution || !this.adminDraft.userId.trim()) {
      this.error.set('Listeden bir kurum yöneticisi seçin.');
      return;
    }
    this.saving.set(true);
    this.service.assignAdmin(institution.id, this.adminDraft.userId.trim(), this.adminDraft.role, this.adminDraft.product).subscribe({
      next: () => { this.saving.set(false); this.adminInstitution.set(null); this.load(); },
      error: () => { this.saving.set(false); this.error.set('Kurum yöneticisi atanamadı. Kullanıcı rolünü kontrol edin.'); }
    });
  }

  toggleAdmin(admin: InstitutionAdminDto) {
    const institution = this.adminInstitution();
    if (!institution) return;
    this.service.setAdminActive(institution.id, admin.userId, !admin.isActive, this.adminDraft.product).subscribe({
      next: () => this.openAdmin(institution, this.adminDraft.product),
      error: () => this.error.set('Kurum yöneticisi durumu güncellenemedi.')
    });
  }

  adminRoleName(role: number) { return ['Bilinmiyor', 'Kurum sahibi', 'Yönetici', 'Müdür'][role] ?? role; }

  typeName(type: number) { return this.institutionTypes.find(item => item.value === type)?.label ?? type; }
}
