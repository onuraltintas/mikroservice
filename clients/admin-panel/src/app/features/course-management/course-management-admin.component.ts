import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  CourseManagementAdminOverview,
  CourseManagementAdminService,
  CourseManagementAdminSubscriptionAuditItem,
  CourseManagementAdminSubscriptionItem
} from './course-management-admin.service';

@Component({
  selector: 'app-course-management-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="course-admin" aria-labelledby="course-admin-title">
      <header class="page-header">
        <div>
          <p class="eyebrow">ÜRÜN OPERASYONLARI</p>
          <h1 id="course-admin-title">Kurs yönetimi</h1>
          <p>Çalışma alanı görünümü, plan ve etkin öğrenci limiti yönetimi.</p>
        </div>
        <button type="button" (click)="load()" [disabled]="loading()">Yenile</button>
      </header>

      @if (loading()) {
        <p class="status" aria-live="polite">Özet yükleniyor…</p>
      }
      @if (error()) {
        <div class="error" role="alert">
          <p>Ürün özeti yüklenemedi. Yeniden deneyebilirsiniz.</p>
          <button type="button" (click)="load()">Tekrar dene</button>
        </div>
      }
      @if (overview(); as data) {
        <div class="metrics" aria-label="Kurs yönetimi toplu göstergeleri">
          <article><h2>Çalışma alanı</h2><p>{{ data.workspaceCount | number }}</p></article>
          <article><h2>Kurum</h2><p>{{ data.institutionWorkspaceCount | number }}</p></article>
          <article><h2>Bağımsız öğretmen</h2><p>{{ data.independentTeacherWorkspaceCount | number }}</p></article>
          <article><h2>Etkin üyelik</h2><p>{{ data.activeMembershipCount | number }}</p></article>
          <article><h2>Etkin öğrenci kaydı</h2><p>{{ data.activeStudentCount | number }}</p></article>
          <article><h2>Kurs</h2><p>{{ data.courseCount | number }}</p></article>
          <article><h2>Planlanan ders</h2><p>{{ data.scheduledLessonCount | number }}</p></article>
          <article><h2>Bekleyen davet</h2><p>{{ data.pendingInvitationCount | number }}</p></article>
        </div>
      }

      <section class="subscriptions" aria-labelledby="course-subscriptions-title">
        <div class="section-heading">
          <div><h2 id="course-subscriptions-title">Çalışma alanı planları</h2>
            <p>Plan kaydı ve etkin öğrenci sayısı. Kişi ve ödeme kaydı gösterilmez.</p></div>
          @if (subscriptionLoading()) { <span role="status">Yükleniyor…</span> }
        </div>
        @if (subscriptionError()) {
          <div class="error" role="alert"><p>Çalışma alanı planları yüklenemedi.</p>
            <button type="button" (click)="loadSubscriptions()">Tekrar dene</button></div>
        }
        @if (subscriptions().length) {
          <div class="table-scroll">
            <table>
              <thead><tr><th>Çalışma alanı</th><th>Tür</th><th>Etkin öğrenci</th><th>Plan</th><th>Durum</th><th>Başlangıç / bitiş</th><th></th></tr></thead>
              <tbody>
                @for (item of subscriptions(); track item.workspaceId) {
                  <tr>
                    <td>{{ item.workspaceName }}</td>
                    <td>{{ workspaceTypeLabel(item.workspaceType) }}</td>
                    <td>{{ item.activeStudentCount }} / {{ item.subscription?.activeStudentLimit ?? '—' }}</td>
                    <td>{{ item.subscription?.planName ?? 'Plan atanmamış' }}</td>
                    <td>{{ item.subscription ? accessStatusLabel(item.subscription.accessStatus) : 'Atanmamış' }}</td>
                    <td>{{ item.subscription ? dateLabel(item.subscription.startsAt) + ' – ' + dateLabel(item.subscription.endsAt) : '—' }}</td>
                    <td><button type="button" (click)="editSubscription(item)">Planı yönet</button></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else if (!subscriptionLoading() && !subscriptionError()) {
          <p>Henüz çalışma alanı yok.</p>
        }
        <nav class="pagination" aria-label="Çalışma alanı sayfaları">
          <span>{{ pageStart() }}–{{ pageEnd() }} / {{ subscriptionTotal() }}</span>
          <button type="button" (click)="loadSubscriptions(subscriptionSkip() - pageSize)"
            [disabled]="subscriptionLoading() || subscriptionSkip() === 0">Önceki</button>
          <button type="button" (click)="loadSubscriptions(subscriptionSkip() + pageSize)"
            [disabled]="subscriptionLoading() || subscriptionSkip() + pageSize >= subscriptionTotal()">Sonraki</button>
        </nav>
        @if (selectedWorkspace(); as selected) {
          <form class="subscription-form" (ngSubmit)="saveSubscription()">
            <h3>{{ selected.workspaceName }} · plan atama</h3>
            @if (subscriptionSaveError()) { <p class="error-text" role="alert">Plan kaydedilemedi. Bilgileri ve erişimi kontrol edin.</p> }
            <div class="form-grid">
              <label>Plan adı<input name="planName" [(ngModel)]="planName" maxlength="80" required></label>
              <label>Etkin öğrenci sınırı<input name="activeStudentLimit" type="number" min="1" step="1" [(ngModel)]="activeStudentLimit" required></label>
              <label>Başlangıç<input name="startsAt" type="datetime-local" [(ngModel)]="startsAt" required></label>
              <label>Bitiş<input name="endsAt" type="datetime-local" [(ngModel)]="endsAt" required></label>
              <label>Plan kayıt durumu<select name="accessStatus" [(ngModel)]="accessStatus">
                <option value="1">Etkin</option><option value="2">Askıya alınmış</option>
              </select></label>
            </div>
            <p>Başlangıç öncesi, bitişte/sonrasında veya plan askıya alındığında çalışma alanı salt okunur olur. Okuma ve dışa aktarma sürer; kişisel profil etkilenmez. SystemAdmin bu ekrandan planı yeniden etkinleştirebilir.</p>
            <div class="form-actions">
              <button type="submit" [disabled]="subscriptionSaving() || !planName.trim() || activeStudentLimit < 1">Kaydet</button>
              <button type="button" (click)="selectedWorkspace.set(null)" [disabled]="subscriptionSaving()">Vazgeç</button>
            </div>
          </form>
        }
      </section>
      <section class="subscriptions" aria-labelledby="course-subscription-audit-title">
        <div class="section-heading">
          <div><h2 id="course-subscription-audit-title">Abonelik değişiklikleri</h2>
            <p>Yalnız plan atama ve güncelleme olayları; kullanıcı veya öğrenci ayrıntısı içermez.</p></div>
          @if (subscriptionAuditLoading()) { <span role="status">Yükleniyor…</span> }
        </div>
        @if (subscriptionAuditError()) {
          <div class="error" role="alert"><p>Abonelik değişiklikleri yüklenemedi.</p>
            <button type="button" (click)="loadSubscriptionAudit()">Tekrar dene</button></div>
        }
        @if (subscriptionAudit().length) {
          <div class="table-scroll">
            <table>
              <thead><tr><th>Çalışma alanı</th><th>Tür</th><th>İşlem</th><th>Zaman</th></tr></thead>
              <tbody>
                @for (entry of subscriptionAudit(); track $index) {
                  <tr>
                    <td>{{ entry.workspaceName }}</td>
                    <td>{{ workspaceTypeLabel(entry.workspaceType) }}</td>
                    <td>{{ subscriptionAuditActionLabel(entry.action) }}</td>
                    <td>{{ auditDateTimeLabel(entry.occurredAt) }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else if (!subscriptionAuditLoading() && !subscriptionAuditError()) {
          <p>Henüz abonelik değişikliği yok.</p>
        }
        <nav class="pagination" aria-label="Abonelik denetim kayıtları sayfaları">
          <span>{{ auditPageStart() }}–{{ auditPageEnd() }} / {{ subscriptionAuditTotal() }}</span>
          <button type="button" (click)="loadSubscriptionAudit(subscriptionAuditSkip() - pageSize)"
            [disabled]="subscriptionAuditLoading() || subscriptionAuditSkip() === 0">Önceki</button>
          <button type="button" (click)="loadSubscriptionAudit(subscriptionAuditSkip() + pageSize)"
            [disabled]="subscriptionAuditLoading() || subscriptionAuditSkip() + pageSize >= subscriptionAuditTotal()">Sonraki</button>
        </nav>
      </section>
      <p class="privacy-note">Admin görünümü çalışma alanı adı/türü, etkin öğrenci sayısı, plan alanları ve yalnız abonelik değişikliği eylem/zamanını içerir. Kullanıcı kimlikleri, öğrenci/ödev/finans ayrıntıları ve ödeme hareketleri açılmaz.</p>
    </section>
  `,
  styles: `
    :host { display: block; }
    .course-admin { padding: 28px; color: #172033; }
    .page-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 20px; margin-bottom: 24px; }
    .eyebrow { margin: 0 0 8px; color: #52637a; font-size: 12px; font-weight: 700; letter-spacing: .08em; }
    h1 { margin: 0; font-size: 28px; }
    .page-header p:last-child { margin: 8px 0 0; color: #52637a; }
    button { border: 1px solid #cbd5e1; border-radius: 8px; background: #fff; color: #172033; padding: 9px 14px; font: inherit; cursor: pointer; }
    button:disabled { cursor: wait; opacity: .65; }
    .metrics { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 14px; }
    article { min-height: 94px; border: 1px solid #e2e8f0; border-radius: 12px; background: #fff; padding: 18px; }
    article h2 { margin: 0; color: #52637a; font-size: 14px; font-weight: 600; }
    article p { margin: 12px 0 0; font-size: 28px; font-weight: 700; }
    .status, .privacy-note { color: #52637a; line-height: 1.6; }
    .error { margin: 16px 0; border: 1px solid #fecaca; border-radius: 8px; background: #fff7f7; padding: 12px 16px; color: #991b1b; }
    .error p { margin: 0 0 10px; }
    .subscriptions { margin-top: 30px; border: 1px solid #e2e8f0; border-radius: 12px; background: #fff; padding: 20px; }
    .section-heading, .pagination, .form-actions { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    .section-heading h2, .subscription-form h3 { margin: 0; font-size: 18px; }
    .section-heading p { margin: 6px 0 16px; color: #52637a; }
    .table-scroll { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; min-width: 820px; }
    th, td { padding: 11px 10px; border-bottom: 1px solid #e2e8f0; text-align: left; white-space: nowrap; }
    th { color: #52637a; font-size: 13px; font-weight: 600; }
    .pagination { justify-content: flex-end; margin-top: 14px; color: #52637a; }
    .subscription-form { margin-top: 22px; border-top: 1px solid #e2e8f0; padding-top: 18px; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 14px; margin: 16px 0; }
    .form-grid label { display: grid; gap: 6px; color: #52637a; font-size: 14px; }
    .form-grid input, .form-grid select { min-width: 0; border: 1px solid #cbd5e1; border-radius: 8px; padding: 9px; color: #172033; font: inherit; }
    .form-actions { justify-content: flex-start; }
    .error-text { color: #991b1b; }
    @media (max-width: 600px) { .course-admin { padding: 18px; } .page-header { flex-direction: column; } }
  `
})
export class CourseManagementAdminComponent implements OnInit {
  private readonly adminService = inject(CourseManagementAdminService);
  readonly overview = signal<CourseManagementAdminOverview | null>(null);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly subscriptions = signal<CourseManagementAdminSubscriptionItem[]>([]);
  readonly subscriptionSkip = signal(0);
  readonly subscriptionTotal = signal(0);
  readonly subscriptionLoading = signal(false);
  readonly subscriptionError = signal(false);
  readonly subscriptionAudit = signal<CourseManagementAdminSubscriptionAuditItem[]>([]);
  readonly subscriptionAuditSkip = signal(0);
  readonly subscriptionAuditTotal = signal(0);
  readonly subscriptionAuditLoading = signal(false);
  readonly subscriptionAuditError = signal(false);
  readonly subscriptionSaving = signal(false);
  readonly subscriptionSaveError = signal(false);
  readonly selectedWorkspace = signal<CourseManagementAdminSubscriptionItem | null>(null);
  readonly pageSize = 50;
  planName = '';
  activeStudentLimit = 25;
  startsAt = '';
  endsAt = '';
  accessStatus: 1 | 2 = 1;

  pageStart(): number { return this.subscriptionTotal() ? this.subscriptionSkip() + 1 : 0; }
  pageEnd(): number { return Math.min(this.subscriptionSkip() + this.subscriptions().length, this.subscriptionTotal()); }
  auditPageStart(): number { return this.subscriptionAuditTotal() ? this.subscriptionAuditSkip() + 1 : 0; }
  auditPageEnd(): number {
    return Math.min(this.subscriptionAuditSkip() + this.subscriptionAudit().length, this.subscriptionAuditTotal());
  }

  workspaceTypeLabel(type: string): string { return type === 'Institution' ? 'Kurum' : 'Bağımsız öğretmen'; }
  accessStatusLabel(status: string): string { return status === 'Active' ? 'Etkin' : 'Askıya alınmış'; }
  dateLabel(value: string): string { return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short' }).format(new Date(value)); }
  auditDateTimeLabel(value: string): string {
    return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
  }
  subscriptionAuditActionLabel(action: string): string {
    return action === 'Subscription.Assigned' ? 'Plan atandı' : 'Plan güncellendi';
  }

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.error.set(false);
    this.adminService.getOverview().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: result => this.overview.set(result),
      error: () => this.error.set(true)
    });
    this.loadSubscriptions(0);
    this.loadSubscriptionAudit(0);
  }

  loadSubscriptions(skip = this.subscriptionSkip()) {
    this.subscriptionLoading.set(true);
    this.subscriptionError.set(false);
    this.adminService.getSubscriptions(skip, this.pageSize).pipe(finalize(() => this.subscriptionLoading.set(false))).subscribe({
      next: page => {
        this.subscriptionSkip.set(page.skip);
        this.subscriptionTotal.set(page.totalCount);
        this.subscriptions.set(page.items);
      },
      error: () => this.subscriptionError.set(true)
    });
  }

  loadSubscriptionAudit(skip = this.subscriptionAuditSkip()) {
    this.subscriptionAuditLoading.set(true);
    this.subscriptionAuditError.set(false);
    this.adminService.getSubscriptionAudit(skip, this.pageSize)
      .pipe(finalize(() => this.subscriptionAuditLoading.set(false))).subscribe({
        next: page => {
          this.subscriptionAuditSkip.set(page.skip);
          this.subscriptionAuditTotal.set(page.totalCount);
          this.subscriptionAudit.set(page.items);
        },
        error: () => this.subscriptionAuditError.set(true)
      });
  }

  editSubscription(item: CourseManagementAdminSubscriptionItem) {
    this.selectedWorkspace.set(item);
    this.subscriptionSaveError.set(false);
    this.planName = item.subscription?.planName ?? '';
    this.activeStudentLimit = item.subscription?.activeStudentLimit ?? Math.max(25, item.activeStudentCount);
    this.startsAt = this.localDateTime(item.subscription?.startsAt ?? new Date().toISOString());
    const defaultEnd = new Date();
    defaultEnd.setFullYear(defaultEnd.getFullYear() + 1);
    this.endsAt = this.localDateTime(item.subscription?.endsAt ?? defaultEnd.toISOString());
    this.accessStatus = item.subscription?.accessStatus === 'Suspended' ? 2 : 1;
  }

  saveSubscription() {
    const selected = this.selectedWorkspace();
    if (!selected || !this.planName.trim() || !Number.isInteger(Number(this.activeStudentLimit))
      || Number(this.activeStudentLimit) < 1 || !this.startsAt || !this.endsAt) return;
    const startsAt = new Date(this.startsAt);
    const endsAt = new Date(this.endsAt);
    if (!Number.isFinite(startsAt.getTime()) || !Number.isFinite(endsAt.getTime()) || endsAt <= startsAt) {
      this.subscriptionSaveError.set(true);
      return;
    }
    this.subscriptionSaving.set(true);
    this.subscriptionSaveError.set(false);
    this.adminService.assignSubscription(selected.workspaceId, {
      planName: this.planName.trim(), activeStudentLimit: Number(this.activeStudentLimit),
      startsAt: startsAt.toISOString(), endsAt: endsAt.toISOString(), accessStatus: this.accessStatus
    }).pipe(finalize(() => this.subscriptionSaving.set(false))).subscribe({
      next: () => {
        this.selectedWorkspace.set(null);
        this.loadSubscriptions();
        this.loadSubscriptionAudit();
      },
      error: () => this.subscriptionSaveError.set(true)
    });
  }

  private localDateTime(value: string): string {
    const date = new Date(value);
    const pad = (part: number) => String(part).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }
}
