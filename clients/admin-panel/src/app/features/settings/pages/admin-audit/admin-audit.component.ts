import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription, catchError, finalize, forkJoin, of } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { IdentityService, UserProfileDto } from '../../../../core/services/identity.service';
import {
  AdminAuditRecord,
  AdminAuditService,
  AdminAuditServiceName
} from '../../../../core/services/settings/admin-audit.service';

@Component({
  selector: 'app-admin-audit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="p-4 md:p-6 min-h-full" aria-labelledby="audit-title">
      <header class="mb-6">
        <h1 id="audit-title" class="text-2xl font-bold text-(--ui-text)">Yönetici denetim kayıtları</h1>
        <p class="mt-1 text-sm text-(--ui-text-muted)">Yönetim işlemlerinin servis bazlı, değiştirilemez işlem ve değişen alan geçmişi.</p>
      </header>

      <nav class="ui-tab-list flex flex-wrap gap-2 mb-5" aria-label="Audit servisi">
        @for (service of services; track service.value) {
          <button type="button" (click)="selectService(service.value)"
            [attr.aria-pressed]="selectedService() === service.value"
            [class.bg-(--ui-brand)]="selectedService() === service.value"
            [class.text-white]="selectedService() === service.value"
            class="ui-tab px-4 py-2 rounded-lg border border-(--ui-border) text-sm font-medium">
            {{ service.label }}
          </button>
        }
      </nav>

      <form (ngSubmit)="applyFilters()" class="grid grid-cols-1 md:grid-cols-4 gap-3 p-4 mb-5 rounded-xl bg-(--ui-surface) border border-(--ui-border)">
        <label class="text-sm text-(--ui-text) md:col-span-2">Arama
          <input [(ngModel)]="search" name="search" maxlength="100" placeholder="Kullanıcı, endpoint veya correlation ID"
            class="mt-1 w-full px-3 py-2 rounded-lg border border-(--ui-border) bg-transparent" />
        </label>
        <label class="text-sm text-(--ui-text)">HTTP durum kodu
          <input [(ngModel)]="statusCode" name="statusCode" type="number" min="100" max="599"
            class="mt-1 w-full px-3 py-2 rounded-lg border border-(--ui-border) bg-transparent" />
        </label>
        <div class="flex items-end gap-2">
          <button type="submit" class="px-4 py-2 rounded-lg bg-(--ui-brand) text-(--ui-brand-contrast)">Filtrele</button>
          <button type="button" (click)="clearFilters()" class="px-4 py-2 rounded-lg border border-(--ui-border)">Temizle</button>
        </div>
      </form>

      @if (error()) {
        <div role="alert" class="mb-4 p-4 rounded-lg bg-(--ui-danger-soft) text-(--ui-danger)">{{ error() }}</div>
      }

      <section class="rounded-xl bg-(--ui-surface) border border-(--ui-border) overflow-hidden" [attr.aria-busy]="loading()">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[1050px] text-left text-sm">
            <thead class="bg-(--ui-surface-muted) text-xs text-(--ui-text-muted)">
              <tr><th class="px-4 py-3">Zaman</th><th class="px-4 py-3">Aktör</th><th class="px-4 py-3">İşlem</th><th class="px-4 py-3">Durum</th><th class="px-4 py-3">Tenant</th><th class="px-4 py-3">Değişen alanlar</th><th class="px-4 py-3">Correlation ID</th></tr>
            </thead>
            <tbody class="divide-y divide-(--ui-border)">
              @for (record of records(); track record.id) {
                <tr>
                  <td class="px-4 py-3 whitespace-nowrap">{{ record.occurredAt | date:'dd.MM.yyyy HH:mm:ss' }}</td>
                  <td class="px-4 py-3">
                    <div class="font-medium">{{ actorLabel(record) }}</div>
                    @if (actorEmail(record)) { <div class="text-xs text-(--ui-text-muted)">{{ actorEmail(record) }}</div> }
                    <div class="text-xs text-(--ui-text-muted)">{{ roleLabels(record.actorRoles) }}</div>
                    <details class="mt-1 text-xs text-(--ui-text-muted)"><summary class="cursor-pointer">Teknik kimlik</summary><span>{{ record.actorUserId }}</span></details>
                  </td>
                  <td class="px-4 py-3">
                    <div class="font-semibold">{{ actionLabel(record) }}</div>
                    <details class="mt-1 text-xs text-(--ui-text-muted)"><summary class="cursor-pointer">Teknik ayrıntı</summary><div>{{ record.httpMethod }} {{ record.path }}</div>@if (record.resourceId) { <div>Kayıt kimliği: {{ record.resourceId }}</div> }</details>
                  </td>
                  <td class="px-4 py-3"><span [class]="statusClass(record.statusCode)">{{ statusLabel(record.statusCode) }}</span><div class="text-xs text-(--ui-text-muted)">HTTP {{ record.statusCode }}</div></td>
                  <td class="px-4 py-3">{{ tenantLabel(record) }}</td>
                  <td class="px-4 py-3 text-xs" [title]="record.changedFieldsJson || ''">{{ changedFields(record) }}</td>
                  <td class="px-4 py-3 font-mono text-xs" [title]="record.correlationId">{{ shortId(record.correlationId) }}</td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="px-6 py-12 text-center text-(--ui-text-muted)">{{ loading() ? 'Yükleniyor…' : 'Kayıt bulunamadı.' }}</td></tr>
              }
            </tbody>
          </table>
        </div>
        <footer class="flex items-center justify-between gap-3 px-4 py-3 bg-(--ui-surface-muted) border-t border-(--ui-border)">
          <span class="text-xs text-(--ui-text-muted)">Toplam {{ totalCount() }} kayıt, sayfa {{ currentPage() }} / {{ totalPages() }}</span>
          <div class="flex gap-2">
            <button type="button" (click)="changePage(currentPage() - 1)" [disabled]="currentPage() <= 1 || loading()" class="px-3 py-2 rounded-lg border disabled:opacity-40">Önceki</button>
            <button type="button" (click)="changePage(currentPage() + 1)" [disabled]="currentPage() >= totalPages() || loading()" class="px-3 py-2 rounded-lg border disabled:opacity-40">Sonraki</button>
          </div>
        </footer>
      </section>
    </main>
  `
})
export class AdminAuditComponent implements OnInit, OnDestroy {
  private readonly auditService = inject(AdminAuditService);
  private readonly authService = inject(AuthService);
  private readonly identityService = inject(IdentityService);
  private readonly platformId = inject(PLATFORM_ID);
  private request?: Subscription;
  private readonly resolvingActors = new Set<string>();

  readonly services: ReadonlyArray<{ value: AdminAuditServiceName; label: string }> = [
    { value: 'identity', label: 'Kimlik' },
    { value: 'coaching', label: 'Koçluk' },
    { value: 'notification', label: 'Bildirim' },
    { value: 'speed-reading', label: 'Hızlı Okuma' }
  ];
  readonly selectedService = signal<AdminAuditServiceName>('identity');
  readonly records = signal<AdminAuditRecord[]>([]);
  readonly totalCount = signal(0);
  readonly currentPage = signal(1);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly actorLabels = signal<Record<string, { name: string; email?: string }>>({});
  readonly pageSize = 25;
  search = '';
  statusCode?: number;

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    const profile = this.authService.userProfile();
    if (profile?.id) {
      const name = `${profile.firstName} ${profile.lastName}`.trim() || profile.email;
      this.actorLabels.set({ [profile.id]: { name, email: profile.email } });
    }
    this.load();
  }
  ngOnDestroy(): void { this.request?.unsubscribe(); }

  selectService(service: AdminAuditServiceName): void {
    if (service === this.selectedService()) return;
    this.selectedService.set(service);
    this.currentPage.set(1);
    this.load();
  }

  applyFilters(): void { this.currentPage.set(1); this.load(); }
  clearFilters(): void { this.search = ''; this.statusCode = undefined; this.applyFilters(); }

  changePage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.load();
  }

  totalPages(): number { return Math.max(1, Math.ceil(this.totalCount() / this.pageSize)); }
  shortId(value: string): string { return value.length > 18 ? `${value.slice(0, 18)}…` : value; }
  actorLabel(record: AdminAuditRecord): string {
    return this.actorLabels()[record.actorUserId]?.name || this.fallbackActorLabel(record);
  }
  actorEmail(record: AdminAuditRecord): string {
    return this.actorLabels()[record.actorUserId]?.email || '';
  }
  roleLabels(value: string): string {
    const labels: Record<string, string> = {
      systemadmin: 'Sistem yöneticisi',
      institutionowner: 'Kurum sahibi',
      institutionadmin: 'Kurum yöneticisi',
      teacher: 'Öğretmen',
      editor: 'İçerik editörü',
      student: 'Öğrenci'
    };
    const roles = value.split(',').map(role => role.trim()).filter(Boolean);
    return roles.length ? roles.map(role => labels[role.toLowerCase()] || role).join(', ') : 'Rol belirtilmemiş';
  }
  actionLabel(record: AdminAuditRecord): string {
    const actions: Record<string, string> = {
      create: 'Oluşturma',
      update: 'Güncelleme',
      delete: 'Silme',
      submit: 'Gönderme',
      complete: 'Tamamlama',
      cancel: 'İptal',
      attendance: 'Yoklama kaydı',
      grade: 'Notlandırma',
      progress: 'İlerleme güncellemesi',
      'add-result': 'Sonuç ekleme',
      'student-note': 'Öğrenci notu',
      'upload-attachment': 'Dosya yükleme',
      get: 'Görüntüleme',
      post: 'İşlem gönderme',
      put: 'Güncelleme',
      patch: 'Güncelleme'
    };
    const rawAction = (record.action || record.httpMethod || '').toLowerCase();
    const action = actions[rawAction] || this.humanize(rawAction);
    const resource = this.resourceLabel(record.resourceType);
    return resource ? `${action} · ${resource}` : action;
  }
  resourceLabel(value?: string): string {
    if (!value) return '';
    const labels: Record<string, string> = {
      users: 'Kullanıcı',
      roles: 'Rol',
      permissions: 'İzin',
      institutions: 'Kurum',
      configurations: 'Sistem ayarı',
      auth: 'Kimlik doğrulama',
      support: 'Destek talebi',
      'email-templates': 'E-posta şablonu',
      notifications: 'Bildirim',
      assignments: 'Atama',
      exercises: 'Egzersiz',
      'reading-texts': 'Okuma metni'
    };
    return labels[value.toLowerCase()] || this.humanize(value);
  }
  statusLabel(statusCode: number): string {
    if (statusCode >= 200 && statusCode < 300) return 'Başarılı';
    const labels: Record<number, string> = {
      400: 'Geçersiz istek',
      401: 'Oturum gerekli',
      403: 'Yetki reddedildi',
      404: 'Kayıt bulunamadı',
      409: 'Çakışma',
      422: 'Doğrulama hatası',
      429: 'Çok fazla istek'
    };
    if (labels[statusCode]) return labels[statusCode];
    if (statusCode >= 500) return 'Sunucu hatası';
    return 'İşlem sonucu';
  }
  statusClass(statusCode: number): string {
    if (statusCode >= 200 && statusCode < 300) return 'text-(--ui-success)';
    if (statusCode >= 400) return 'text-(--ui-danger)';
    return 'text-(--ui-warning)';
  }
  tenantLabel(record: AdminAuditRecord): string {
    return record.tenantId ? 'Kurum kapsamı' : 'Merkezi yönetim';
  }
  changedFields(record: AdminAuditRecord): string {
    if (!record.changedFieldsJson) return '-';
    try {
      const fields = JSON.parse(record.changedFieldsJson) as unknown;
      if (!Array.isArray(fields)) return '-';
      const labels: Record<string, string> = {
        value: 'Değer',
        adminNote: 'Yönetici notu',
        replyMessage: 'Yanıt',
        isActive: 'Aktiflik durumu'
      };
      return fields.map(field => labels[String(field)] || String(field)).join(', ');
    } catch {
      return '-';
    }
  }

  private load(): void {
    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set('');
    this.request = this.auditService.getPage(this.selectedService(), {
      page: this.currentPage(), pageSize: this.pageSize,
      search: this.search.trim() || undefined, statusCode: this.statusCode || undefined
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: page => {
        this.records.set(page.items);
        this.totalCount.set(page.totalCount);
        this.resolveActorLabels(page.items);
      },
      error: () => {
        this.records.set([]); this.totalCount.set(0);
        this.error.set('Denetim kayıtları yüklenemedi. Lütfen tekrar deneyin.');
      }
    });
  }

  private fallbackActorLabel(record: AdminAuditRecord): string {
    if (record.actorUserId === 'unknown') return 'Sistem işlemi';
    const roles = this.roleLabels(record.actorRoles);
    return roles === 'Rol belirtilmemiş' ? 'Yönetici hesabı' : roles;
  }

  private resolveActorLabels(records: AdminAuditRecord[]): void {
    const ids = [...new Set(records.map(record => record.actorUserId).filter(Boolean))]
      .filter(id => !this.actorLabels()[id] && !this.resolvingActors.has(id));
    if (!ids.length) return;

    ids.forEach(id => this.resolvingActors.add(id));
    // Live test 19.11.2026 (A10): an actor whose account was deleted answers 404; the row then says so.
    forkJoin(ids.map(id => this.identityService.getUserById(id).pipe(
      catchError(error => of<UserProfileDto | 'deleted' | null>(error?.status === 404 ? 'deleted' : null)))))
      .subscribe(profiles => profiles.forEach((profile, index) => {
        const id = ids[index];
        this.resolvingActors.delete(id);
        if (profile === 'deleted') {
          this.actorLabels.update(current => ({ ...current, [id]: { name: 'Silinmiş kullanıcı' } }));
        } else if (profile) {
          const name = profile.fullName || `${profile.firstName} ${profile.lastName}`.trim() || profile.email;
          this.actorLabels.update(current => ({ ...current, [id]: { name, email: profile.email } }));
        } else {
          const record = records.find(item => item.actorUserId === id);
          if (record) this.actorLabels.update(current => ({ ...current, [id]: { name: this.fallbackActorLabel(record) } }));
        }
      }));
  }

  private humanize(value: string): string {
    if (!value) return 'Bilinmeyen işlem';
    return value.replace(/[-_]+/g, ' ').replace(/\b\w/g, character => character.toUpperCase());
  }
}
